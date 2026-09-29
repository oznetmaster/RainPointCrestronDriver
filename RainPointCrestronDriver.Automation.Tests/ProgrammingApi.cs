// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RainPointCrestronDriver.Automation.Tests;

// Test-only implementation of the configuration protocol. No vendor binaries are required.
internal sealed class ProgrammingApi : IAsyncDisposable
	{
	private readonly ClientWebSocket _socket = new ();
	private readonly CancellationTokenSource _stop = new ();
	private HttpClient _http;
	private Task _drain;
	internal static async Task<ProgrammingApi> ConnectAsync (string host, string pin, NetworkCredential credential, CancellationToken token)
		{
		var api = new ProgrammingApi ();
		try
			{
			bool Trusted (System.Security.Cryptography.X509Certificates.X509Certificate certificate) => certificate != null
				&& string.Equals (Convert.ToHexString (SHA256.HashData (certificate.GetRawCertData ())), pin, StringComparison.OrdinalIgnoreCase);
			api._socket.Options.RemoteCertificateValidationCallback = (_, certificate, _, _) => Trusted (certificate);
			await api._socket.ConnectAsync (new Uri ("wss://" + host + ":49000/"), token);
			byte[] login = JsonSerializer.SerializeToUtf8Bytes (new
				{
				UserName = credential.UserName,
				Password = credential.Password
				});
			try
				{
				await api._socket.SendAsync (new ArraySegment<byte> (login), WebSocketMessageType.Text, true, token);
				}
			finally { CryptographicOperations.ZeroMemory (login); }
			using JsonDocument reply = JsonDocument.Parse (await api.ReadMessage (token));
			JsonElement auth = reply.RootElement;
			if (!auth.GetProperty ("Authenticated").GetBoolean ())
				throw new InvalidOperationException ("Processor authentication failed.");
			int port = auth.TryGetProperty ("HttpPort", out JsonElement p) ? p.GetInt32 () : 443;
			if (port < 1 || port > 65535)
				throw new InvalidDataException ("Invalid configuration port.");
			var handler = new HttpClientHandler { AllowAutoRedirect = false };
			handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) => Trusted (certificate);
			api._http = new HttpClient (handler) { BaseAddress = new UriBuilder ("https", host, port, "/cws/api/").Uri, Timeout = TimeSpan.FromSeconds (30) };
			api._http.DefaultRequestHeaders.Add ("Crestron-RestAPI-AuthToken", auth.GetProperty ("WebApiToken").GetString ());
			api._http.DefaultRequestHeaders.Add ("Crestron-RestAPI-AuthKey", auth.GetProperty ("RestV2Token").GetString ());
			api._drain = api.Drain ();
			return api;
			}
		catch { await api.DisposeAsync (); throw; }
		}
	private async Task<byte[]> ReadMessage (CancellationToken token)
		{
		using var stream = new MemoryStream ();
		byte[] buffer = new byte[16384];
		WebSocketReceiveResult frame;
		do
			{
			frame = await _socket.ReceiveAsync (new ArraySegment<byte> (buffer), token);
			if (frame.MessageType != WebSocketMessageType.Text)
				throw new IOException ("Configuration event socket closed.");
			stream.Write (buffer, 0, frame.Count);
			if (stream.Length > 8 * 1024 * 1024)
				throw new InvalidDataException ("Oversized configuration message.");
			} while (!frame.EndOfMessage);
		return stream.ToArray ();
		}
	private async Task Drain ()
		{
		try
			{
			while (!_stop.IsCancellationRequested)
				await ReadMessage (_stop.Token);
			}
		catch (Exception) when (_stop.IsCancellationRequested) { }
		}
	internal async Task<JsonElement> Command (string command, object parameters, CancellationToken token)
		{
		if (_drain.IsCompleted)
			await _drain;
		using HttpResponseMessage session = await _http.GetAsync ("v2/login", token);
		session.EnsureSuccessStatusCode ();
		if (session.Headers.TryGetValues ("Crestron-RestAPI-AuthKey", out var keys))
			{
			_http.DefaultRequestHeaders.Remove ("Crestron-RestAPI-AuthKey");
			_http.DefaultRequestHeaders.Add ("Crestron-RestAPI-AuthKey", keys);
			}
		using var content = new StringContent (JsonSerializer.Serialize (new
			{
			CommandName = command,
			Parameters = parameters
			}), Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await _http.PostAsync ("v2/Programming/Command", content, token);
		response.EnsureSuccessStatusCode ();
		using JsonDocument result = JsonDocument.Parse (await response.Content.ReadAsStringAsync (token));
		if (result.RootElement.TryGetProperty ("Error", out JsonElement error) && error.ValueKind != JsonValueKind.Null)
			throw new InvalidOperationException ("Home rejected " + command + "; no operation was retried.");
		return result.RootElement.GetProperty ("Result").Clone ();
		}
	public async ValueTask DisposeAsync ()
		{
		_stop.Cancel ();
		if (_drain != null)
			{
			try
				{
				await _drain;
				}
			catch (Exception) { }
			}
		_socket.Dispose ();
		_http?.Dispose ();
		_stop.Dispose ();
		}
	}