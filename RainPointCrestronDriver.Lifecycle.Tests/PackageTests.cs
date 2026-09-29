// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace RainPointCrestronDriver.Lifecycle.Tests;

[TestFixture, Category ("Package")]
public sealed class PackageTests
	{
	private static Assembly Package ()
		{
		string path = Environment.GetEnvironmentVariable ("RAINPOINT_PACKAGED_ASSEMBLY");
		if (string.IsNullOrWhiteSpace (path))
			{
			Assert.Ignore ("Run tools/Build-Package.ps1 to test the merged package.");
			}
		Assert.That (File.Exists (path), Is.True);
		return Assembly.LoadFrom (path);
		}
	[Test]
	public async Task MergedClientPreservesAttributedJsonModels ()
		{
		Type type = Package ().GetType ("RainPointClient.RainPointCloudClient", true);
		using var transport = new FixtureTransport ();
		using var http = new HttpClient (transport);
		using var client = (IDisposable)Activator.CreateInstance (type, http, new Uri ("https://fixture.invalid/"));
		await (Task)type.GetMethod ("LoginAsync").Invoke (client, new object[] { "test@example.invalid", "synthetic", "44", CancellationToken.None });
		var task = (Task)type.GetMethod ("GetHomesAsync").Invoke (client, new object[] { CancellationToken.None });
		await task;
		var homes = (IEnumerable)task.GetType ().GetProperty ("Result").GetValue (task);
		var iterator = homes.GetEnumerator ();
		Assert.That (iterator.MoveNext (), Is.True);
		Assert.That (iterator.Current.GetType ().GetProperty ("Id").GetValue (iterator.Current), Is.EqualTo (42L));
		Assert.That (iterator.Current.GetType ().GetProperty ("Name").GetValue (iterator.Current), Is.EqualTo ("Garden"));
		Assert.That (transport.Requests, Is.EqualTo (2));
		var hubTask = (Task)type.GetMethod ("GetHubsAsync").Invoke (client, new object[] { 42L, CancellationToken.None });
		await hubTask;
		var hubs = ((IEnumerable)hubTask.GetType ().GetProperty ("Result").GetValue (hubTask)).GetEnumerator ();
		Assert.That (hubs.MoveNext (), Is.True);
		var devices = ((IEnumerable)hubs.Current.GetType ().GetProperty ("Devices").GetValue (hubs.Current)).GetEnumerator ();
		Assert.That (devices.MoveNext (), Is.True);
		Assert.That ((IEnumerable)devices.Current.GetType ().GetProperty ("ZoneNames").GetValue (devices.Current), Is.EqualTo (new[] { "Lawn", "Beds", "Tap" }));
		}
	[Test]
	public void MergedClientCanLoadItsMqttTrustAnchor ()
		{
		Type transport = Package ().GetType ("RainPointClient.Protocol.MqttObserverTransport", true);
		using var certificate = (X509Certificate2)transport.GetMethod ("LoadRoot", BindingFlags.Static | BindingFlags.NonPublic).Invoke (null, null);
		Assert.That (certificate.Subject, Is.Not.Empty);
		Assert.That (certificate.HasPrivateKey, Is.False);
		}
	[TestCase ("HTV145FRF", 1), TestCase ("HTV245FRF", 2), TestCase ("HTV345FRF", 3)]
	public void MergedClientRetainsAllRecognizedTimerVariants (string model, int count)
		{
		Type deviceType = Package ().GetType ("RainPointClient.RainPointDevice", true);
		object device = Activator.CreateInstance (deviceType);
		deviceType.GetProperty ("Model").SetValue (device, model);
		Assert.That (deviceType.GetProperty ("SupportedZoneCount").GetValue (device), Is.EqualTo (count));
		}

	private sealed class FixtureTransport : HttpMessageHandler
		{
		internal int Requests;
		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken token)
			{
			Requests++;
			return Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK)
				{
				Content = new StringContent (Requests == 1
					? "{\"code\":0,\"data\":{\"token\":\"fixture-session\",\"tokenExpired\":3600}}"
					: Requests == 2 ? "{\"code\":0,\"data\":[{\"hid\":42,\"homeName\":\"Garden\"}]}"
					: """{"code":0,"data":[{"mid":101,"deviceName":"fixture","productKey":"fixture","subDevices":[{"addr":2,"model":"HTV345FRF","portDescribe":"Lawn|Beds|Tap"}]}]}""")
				});
			}
		}
	}