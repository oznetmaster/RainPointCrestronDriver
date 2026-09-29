// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RainPointClient;

using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Tests;

[TestFixture]
public sealed class MetadataTests
	{
	private FixtureHandler _handler;
	private HttpClient _http;
	private CloudConnection _connection;
	private readonly List<IReadOnlyList<TimerIdentity>> _catalogs = [];
	private readonly List<string[]> _plans = [];
	[SetUp]
	public async Task SetUp ()
		{
		_catalogs.Clear ();
		_plans.Clear ();
		_handler = new FixtureHandler ();
		_http = new HttpClient (_handler);
		var client = new RainPointCloudClient (_http, new Uri ("https://fixture.invalid/"));
		await client.LoginAsync ("fixture@example.invalid", "fixture", "44");
		var hub = (await client.GetHubsAsync (42)).Single ();
		_connection = new CloudConnection (client);
		_connection.InitializeMetadata (hub);
		_connection.CatalogChanged += value => _catalogs.Add (value);
		_connection.PlansChanged += (_, value) => _plans.Add (value);
		_handler.Reads = 0;
		}
	[TearDown]
	public async Task TearDown ()
		{
		await _connection.CloseAsync ();
		_http.Dispose ();
		_handler.Dispose ();
		}
	[Test]
	public async Task RefreshReadsNamesAndPlansWithoutChangingCommissionedIdentity ()
		{
		_handler.Name = "Renamed";
		await _connection.RefreshMetadataAsync (CancellationToken.None);
		var timer = _catalogs.Single ().Single ();
		Assert.That (timer.ControllerId, Is.EqualTo ("rainpoint_101_2"));
		Assert.That (timer.HubName, Is.EqualTo ("Renamed hub"));
		Assert.That (timer.Name, Is.EqualTo ("Renamed timer"));
		Assert.That (new[] { timer.ZoneName (1), timer.ZoneName (2), timer.ZoneName (3) }, Is.EqualTo (new[] { "Lawn", "Beds", "Tap" }));
		Assert.That (_plans.Single (), Has.Length.EqualTo (3));
		Assert.That (_handler.Reads, Is.EqualTo (4));
		Assert.That (_handler.Writes, Is.EqualTo (1), "Only the fixture login may write.");
		}
	[Test]
	public async Task TransportIdentityChangeDoesNotPublishOrReplaceMetadata ()
		{
		_handler.TransportName = "different-device";
		await Assert.ThrowsAsync<InvalidOperationException> (() => _connection.RefreshMetadataAsync (CancellationToken.None));
		Assert.That (_catalogs, Is.Empty);
		Assert.That (_plans, Is.Empty);
		_handler.TransportName = "fixture-device";
		await _connection.RefreshMetadataAsync (CancellationToken.None);
		Assert.That (_catalogs, Has.Count.EqualTo (1));
		}
	[Test]
	public async Task NotificationBurstIsCoalescedAndCloseDrainsWorker ()
		{
		var done = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		_connection.PlansChanged += (_, _) => done.TrySetResult (true);
		_connection.StartMetadata (CancellationToken.None);
		for (int i = 0; i < 10; i++)
			_connection.RequestMetadata ();
		Assert.That (await Task.WhenAny (done.Task, Task.Delay (5000)), Is.SameAs (done.Task));
		await _connection.CloseAsync ();
		Assert.That (_handler.Reads, Is.EqualTo (4));
		Assert.That (_catalogs, Has.Count.EqualTo (1));
		}
	[Test]
	public async Task FailedReadKeepsStatusAndCancellationInterruptsRetryDelay ()
		{
		int states = 0;
		_connection.ConnectionState += _ => states++;
		_handler.FailReads = true;
		_connection.StartMetadata (CancellationToken.None);
		_connection.RequestMetadata ();
		Assert.That (await Task.WhenAny (_handler.Failed.Task, Task.Delay (5000)), Is.SameAs (_handler.Failed.Task));
		Task close = _connection.CloseAsync ();
		Assert.That (await Task.WhenAny (close, Task.Delay (5000)), Is.SameAs (close));
		await close;
		Assert.That (_catalogs, Is.Empty);
		Assert.That (states, Is.Zero, "A configuration read failure must not take watering feedback offline.");
		}
	[Test]
	public async Task LastUsageSelectsMatchingNewestCompleteRecordIncludingZero ()
		{
		_handler.Events = """[{"eid":"older","mid":101,"addr":2,"port":1,"code":1,"timestamp":1790668800000,"time":"2026-09-29T09:00:00","timezone":"GMT+01:00","rule":[{"type":"3","value":"14"}]},{"eid":"latest","mid":101,"addr":2,"port":1,"code":2,"timestamp":1790668860000,"time":"2026-09-29T09:01:00","timezone":"GMT+01:00","rule":[{"type":"3","value":"0"}]},{"eid":"unrelated","mid":101,"addr":2,"port":1,"code":128,"timestamp":1790668890000,"rule":[{"type":"3","value":"999"}]},{"eid":"incomplete","mid":101,"addr":2,"port":1,"code":1,"timestamp":1790668880000,"rule":[]}]""";
		var value = await _connection.ReadLastUsageAsync (2, 1, CancellationToken.None);
		Assert.That (value.Id, Is.EqualTo ("latest"));
		Assert.That (value.Litres, Is.Zero);
		Assert.That (value.LocalTime, Is.EqualTo (new DateTime (2026, 9, 29, 9, 1, 0)));
		Assert.That (_handler.LastHistoryQuery, Is.EqualTo ("?hid=42&size=50&mid=101&addr=2&port=1"));
		Assert.That (_handler.Writes, Is.EqualTo (1));
		}
	[Test]
	public async Task EmptyHistoryDoesNotInventAUsageDateOrVolume ()
		{
		Assert.That (await _connection.ReadLastUsageAsync (2, 3, CancellationToken.None), Is.Null);
		Assert.That (_handler.LastHistoryQuery, Does.Contain ("port=3"));
		}

	[TestCase ("HTV145FRF", 1), TestCase ("HTV245FRF", 2)]
	public async Task VariantDiscoveryRefreshAndPlansUseActualCount (string model, int count)
		{
		await _connection.CloseAsync ();
		_handler.Model = model;
		_handler.ZoneCount = count;
		var client = new RainPointCloudClient (_http, new Uri ("https://fixture.invalid/"));
		await client.LoginAsync ("fixture@example.invalid", "fixture", "44");
		var hub = (await client.GetHubsAsync (42)).Single ();
		_connection = new CloudConnection (client);
		_connection.InitializeMetadata (hub);
		_connection.CatalogChanged += value => _catalogs.Add (value);
		_connection.PlansChanged += (_, value) => _plans.Add (value);
		_handler.Reads = 0;
		await _connection.RefreshMetadataAsync (CancellationToken.None);
		Assert.That (_catalogs.Single ().Single ().Model, Is.EqualTo (model));
		Assert.That (_catalogs.Single ().Single ().ZoneCount, Is.EqualTo (count));
		Assert.That (_plans.Single (), Has.Length.EqualTo (count));
		Assert.That (_plans.Single ().All (p => p.StartsWith ("No saved plans")), Is.True);
		Assert.That (_handler.Reads, Is.EqualTo (count + 1));
		_handler.Model = "HTV345FRF";
		_handler.ZoneCount = 3;
		await Assert.ThrowsAsync<InvalidOperationException> (() => _connection.RefreshMetadataAsync (CancellationToken.None));
		Assert.That (_catalogs, Has.Count.EqualTo (1));
		}

	private sealed class FixtureHandler : HttpMessageHandler
		{
		internal string Name = "Original", TransportName = "fixture-device";
		internal int Reads, Writes;
		internal string Model = "HTV345FRF";
		internal int ZoneCount = 3;
		internal string Events = "[]", LastHistoryQuery;
		internal bool FailReads;
		internal readonly TaskCompletionSource<bool> Failed = new (TaskCreationOptions.RunContinuationsAsynchronously);
		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken token)
			{
			string body;
			if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath.EndsWith ("login"))
				{
				Writes++;
				body = """{"code":0,"data":{"token":"fixture","tokenExpired":3600}}""";
				}
			else if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith ("getDeviceByHid"))
				{
				Reads++;
				if (FailReads)
					{
					Failed.TrySetResult (true);
					throw new HttpRequestException ("Fixture failure");
					}
				body = "{\"code\":0,\"data\":[{\"mid\":101,\"name\":\"" + Name + " hub\",\"model\":\"HWG023WBRF\",\"deviceName\":\"" + TransportName + "\",\"productKey\":\"fixture-key\",\"subDevices\":[{\"sid\":201,\"addr\":2,\"model\":\"HTV345FRF\",\"name\":\"" + Name + " timer\",\"portDescribe\":\"Lawn|Beds|Tap\",\"portNumber\":3,\"softVer\":\"130\",\"param\":\"settings,/,|settings,/,|settings,/,\"}]}]}";
				body = body.Replace ("HTV345FRF", Model).Replace ("\"portNumber\":3", "\"portNumber\":" + ZoneCount)
					.Replace ("settings,/,|settings,/,|settings,/,", string.Join ("|", Enumerable.Repeat ("settings,/,", ZoneCount)));

				}
			else if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith ("/event/list"))
				{
				LastHistoryQuery = request.RequestUri.Query;
				body = "{\"code\":0,\"data\":" + Events + "}";
				}
			else
				throw new InvalidOperationException ("Unexpected fixture request: " + request.Method + " " + request.RequestUri.AbsolutePath);
			return Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StringContent (body) });
			}
		}
	}