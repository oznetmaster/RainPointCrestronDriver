// Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
using System;
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
public sealed class PollingContinuityTests
	{
	[TestCase (false), TestCase (true)]
	public async Task PeriodicReadRetainsQuietTimerContactAndFailedReadDoesNotRefreshIt (bool failSecondRead)
		{
		using var handler = new QuietTimerHandler { FailSecondRead = failSecondRead };
		using var http = new HttpClient (handler);
		var client = new RainPointCloudClient (http, new Uri ("https://fixture.invalid/"));
		await client.LoginAsync ("fixture@example.invalid", "fixture", "44");
		RainPointHub hub = (await client.GetHubsAsync (42)).Single ();
		var connection = new CloudConnection (client);
		connection.InitializeMetadata (hub);
		RainPointMonitorOptions options = CloudConnection.CreateMonitorOptions ();
		Assert.That (options.PollWhilePushConnected, Is.True, "Production must retain periodic reads alongside push.");
		// The public monitor supplies real automatic REST reads. No broker or external network is used.
		options.EnablePush = false;
		options.PollInterval = TimeSpan.FromSeconds (5);
		var monitor = new RainPointMonitor (client, hub, options);
		var first = new TaskCompletionSource<TimerReading> (TaskCreationOptions.RunContinuationsAsynchronously);
		var second = new TaskCompletionSource<TimerReading> (TaskCreationOptions.RunContinuationsAsynchronously);
		var failed = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		int reports = 0;
		connection.Reading += reading =>
			{
			if (Interlocked.Increment (ref reports) == 1) first.TrySetResult (reading);
			else second.TrySetResult (reading);
			};
		monitor.StatusReceived += connection.OnStatus;
		monitor.StateChanged += (_, value) =>
			{
			if (value.State == RainPointMonitorState.Reconnecting) failed.TrySetResult (true);
			};
		Task running = monitor.RunAsync ();
		try
			{
			await Within (first.Task);
			TimerReading initial = await first.Task;
			Assert.That (initial.ReportTime, Is.LessThan (DateTimeOffset.UtcNow.AddHours (-1)), "Quiet device data-change time stays old.");
			Assert.That (initial.IsFresh (DateTimeOffset.UtcNow, false), Is.True, "An actual successful read supplies recent contact.");
			if (failSecondRead)
				{
				await Within (failed.Task);
				Assert.That (reports, Is.EqualTo (1), "A failed HTTP read must not publish a new successful contact.");
				Assert.That (initial.IsFresh (initial.ContactTime.Value.AddSeconds (91), false), Is.False);
				Assert.That (second.Task.IsCompleted, Is.False);
				}
			else
				{
				await Within (second.Task);
				TimerReading periodic = await second.Task;
				Assert.That (periodic.ContactTime, Is.GreaterThan (initial.ContactTime));
				Assert.That (periodic.ReportTime, Is.EqualTo (initial.ReportTime), "Polling must not invent a new physical report.");
				Assert.That (periodic.IsFresh (periodic.ContactTime.Value.AddSeconds (89), false), Is.True,
					"During reconnect, recent successful polling remains fresh without a live-push exemption.");
				Assert.That (periodic.IsFresh (periodic.ContactTime.Value.AddSeconds (91), false), Is.False,
					"An actual prolonged contact gap still becomes unknown under the unchanged90-second policy.");
				}
			Assert.That (handler.StatusReads, Is.GreaterThanOrEqualTo (2));
			Assert.That (handler.WateringWrites, Is.Zero);
			}
		finally
			{
			await monitor.StopAsync ();
			await running;
			await connection.CloseAsync ();
			}
		}

	private static async Task Within (Task task)
		{
		Assert.That (await Task.WhenAny (task, Task.Delay (TimeSpan.FromSeconds (15))), Is.SameAs (task));
		await task;
		}

	private sealed class QuietTimerHandler : HttpMessageHandler
		{
		internal bool FailSecondRead;
		internal int StatusReads, WateringWrites;
		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken token)
			{
			string path = request.RequestUri.AbsolutePath;
			string reply;
			if (path.EndsWith ("/login", StringComparison.Ordinal))
				reply = """{"code":0,"data":{"token":"fixture","tokenExpired":3600}}""";
			else if (path.EndsWith ("/getDeviceByHid", StringComparison.Ordinal))
				reply = """{"code":0,"data":[{"mid":101,"name":"Fixture hub","model":"HWG023WBRF","deviceName":"fixture-hub","productKey":"fixture-key","subDevices":[{"addr":2,"model":"HTV345FRF","name":"Quiet timer","portNumber":3}]}]}""";
			else if (path.EndsWith ("/multipleDeviceStatus", StringComparison.Ordinal))
				{
				if (Interlocked.Increment (ref StatusReads) >= 2 && FailSecondRead)
					throw new HttpRequestException ("Synthetic read failure.");
				reply = """{"code":0,"data":[{"mid":101,"status":[{"id":"connected","value":"1"},{"id":"D02","value":"11#17E1D70018DC0219D8011AD80025AD5802","time":1700000000000}]}]}""";
				}
			else
				{
				Interlocked.Increment (ref WateringWrites);
				throw new InvalidOperationException ("Unexpected fixture request.");
				}
			return Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StringContent (reply) });
			}
		}
	}
