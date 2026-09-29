// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Tests;

[TestFixture]
public sealed class ControllerTests
	{
	private static readonly DateTimeOffset Now = new (2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	private static readonly TimerIdentity Timer = new (100, 42, "Garden");
	private IrrigationController _controller;
	private FakeConnection _connection;
	private static DriverSettings Settings (string password = "test") => new ("test@example.invalid", password, "44");
	private static TimerReading Frame (long revision = 1, bool? active = false, DateTimeOffset? time = null) =>
		new (Timer, revision, true, time ?? Now, Enumerable.Range (1, 3).Select (z => new ZoneReading (z, active, "Watering", 1.4m, TimeSpan.FromMinutes (5), "No reported alarms")).ToArray (), "Normal", "-70 dBm");
	[SetUp]
	public async Task SetUp ()
		{
		_connection = new FakeConnection ();
		_controller = new IrrigationController (() => _connection, () => Now);
		await _controller.ConfigureAsync (Settings ());
		_connection.Emit (Frame ());
		}
	[TearDown]
	public async Task TearDown () => await _controller.ShutdownAsync ();

	[TestCase (1), TestCase (2), TestCase (3)]
	public async Task EveryZoneCanStartAndStop (int zone)
		{
		await _controller.StartAsync (Timer.ControllerId, zone, 5);
		await _controller.StopAsync (Timer.ControllerId, zone);
		Assert.That (_connection.Commands, Is.EqualTo (new[] { "start:42:" + zone + ":5", "stop:42:" + zone }));
		}
	[TestCase (0), TestCase (4), TestCase (-1)]
	public void RejectInvalidZones (int zone)
		{
		Assert.Throws<ArgumentOutOfRangeException> (() => _controller.StartAsync (Timer.ControllerId, zone, 5));
		Assert.Throws<ArgumentOutOfRangeException> (() => _controller.StopAsync (Timer.ControllerId, zone));
		Assert.That (_connection.Commands, Is.Empty);
		}
	[TestCase (0), TestCase (-1), TestCase (121)]
	public void RejectInvalidDuration (int minutes)
		{
		Assert.Throws<ArgumentOutOfRangeException> (() => _controller.StartAsync (Timer.ControllerId, 1, minutes));
		Assert.That (_connection.Commands, Is.Empty);
		}
	[TestCase (1), TestCase (120)]
	public async Task AcceptDurationBounds (int minutes)
		{
		await _controller.StartAsync (Timer.ControllerId, 1, minutes);
		Assert.That (_connection.Commands.Single (), Does.EndWith (":" + minutes));
		}
	[Test]
	public async Task AcknowledgementDoesNotChangeReportedState ()
		{
		int readings = 0;
		_controller.Reading += _ => readings++;
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		Assert.That (readings, Is.Zero);
		}
	[Test]
	public async Task DuplicateStartSuppressedWhileAwaitingFeedback ()
		{
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		Assert.That (_connection.Commands.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task UncertainStartNotReplayed ()
		{
		_connection.FailStart = true;
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		Assert.That (_connection.Commands.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task UnrelatedReportDoesNotClearPendingStart ()
		{
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		_connection.Emit (Frame (2, false, Now.AddSeconds (1)));
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		Assert.That (_connection.Commands.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task MatchingNewReportCompletesPendingFeedback ()
		{
		string message = null;
		_controller.CommandChanged += (_, zone, value) =>
			{
				if (zone == 1)
					{
					message = value;
					}
			};
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		_connection.Emit (Frame (2, true, Now.AddSeconds (1)));
		Assert.That (message, Is.EqualTo ("New report: Watering"));
		}
	[Test]
	public async Task AutomaticStopReplacesEarlierWateringFeedback ()
		{
		string message = null;
		_controller.CommandChanged += (_, zone, value) => { if (zone == 1) message = value; };
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		_connection.Emit (Frame (2, true, Now.AddSeconds (1)));
		_connection.Emit (Frame (3, false, Now.AddSeconds (2)));
		Assert.That (message, Is.EqualTo ("New report: Idle"));
		Assert.That (_connection.Commands.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task AuthenticationRecoveryResumesCommandsOnNewFeedback ()
		{
		_connection.EmitState ("AuthenticationRequired");
		await _controller.StopAsync (Timer.ControllerId, 1);
		Assert.That (_connection.Commands, Is.Empty);
		_connection.Emit (Frame (2));
		await _controller.StopAsync (Timer.ControllerId, 1);
		Assert.That (_connection.Commands.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task FeedbackTimeoutIsReportedWithoutRetry ()
		{
		await _controller.ShutdownAsync ();
		DateTimeOffset clock = Now;
		_controller = new IrrigationController (() => _connection, () => clock);
		await _controller.ConfigureAsync (Settings ());
		_connection.Emit (Frame ());
		string message = null;
		_controller.CommandChanged += (_, _, value) => message = value;
		await _controller.StartAsync (Timer.ControllerId, 1, 5);
		clock = clock.AddSeconds (91);
		_controller.CheckFeedbackTimeouts ();
		Assert.That (message, Is.EqualTo ("Feedback timeout; check timer status"));
		Assert.That (_connection.Commands.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task StopAllAttemptsRemainingZonesAfterFailure ()
		{
		_connection.FailFirstStop = true;
		await _controller.StopAllAsync (Timer.ControllerId);
		Assert.That (_connection.Commands, Is.EqualTo (new[] { "stop:42:1", "stop:42:2", "stop:42:3" }));
		}
	[Test]
	public async Task UnknownDeviceCannotReceiveCommands ()
		{
		await _controller.StartAsync ("rainpoint_999_42", 1, 5);
		Assert.That (_connection.Commands, Is.Empty);
		}
	[TestCase (true), TestCase (null)]
	public async Task ActiveOrUnknownZoneCannotBeStarted (bool? active)
		{
		_connection.Emit (Frame (2, active));
		await _controller.StartAsync (Timer.ControllerId, 2, 5);
		Assert.That (_connection.Commands, Is.Empty);
		}
	[Test]
	public async Task StaleStartRejectedButStopAllowed ()
		{
		_connection.Emit (Frame (2, false, Now.AddMinutes (-10)));
		await _controller.StartAsync (Timer.ControllerId, 3, 5);
		await _controller.StopAsync (Timer.ControllerId, 3);
		Assert.That (_connection.Commands, Is.EqualTo (new[] { "stop:42:3" }));
		}
	[Test]
	public void OutOfOrderAndDuplicateRevisionsIgnored ()
		{
		var revisions = new List<long> ();
		_controller.Reading += r => revisions.Add (r.Revision);
		_connection.Emit (Frame (3));
		_connection.Emit (Frame (2));
		_connection.Emit (Frame (3));
		Assert.That (revisions, Is.EqualTo (new long[] { 3 }));
		}
	[Test]
	public async Task SameConfigurationDoesNotReconnect ()
		{
		await _controller.ConfigureAsync (Settings ());
		Assert.That (_connection.ConnectCount, Is.EqualTo (1));
		}
	[Test]
	public async Task ClearStopsConnectionWithoutWatering ()
		{
		await _controller.ConfigureAsync (null);
		Assert.That (_connection.Closed, Is.True);
		Assert.That (_connection.Commands, Is.Empty);
		}
	[Test]
	public async Task OldCallbacksCannotAffectReplacement ()
		{
		FakeConnection old = _connection;
		_connection = new FakeConnection ();
		await _controller.ConfigureAsync (Settings ("replacement"));
		int readings = 0;
		_controller.Reading += _ => readings++;
		old.Emit (Frame (100));
		_connection.Emit (Frame (1));
		Assert.That (readings, Is.EqualTo (1));
		Assert.That (old.Closed, Is.True);
		}
	[Test]
	public async Task ShutdownCancelsAndWaitsForActiveCommand ()
		{
		_connection.BlockStart = true;
		Task command = _controller.StartAsync (Timer.ControllerId, 1, 5);
		await _connection.Started.Task;
		await _controller.ShutdownAsync ();
		await command;
		Assert.That (_connection.CommandFinished, Is.True);
		Assert.That (_connection.ClosedWhileCommandActive, Is.False);
		}
	[Test]
	public async Task QueuedCommandsDoNotRunOnReplacement ()
		{
		FakeConnection old = _connection;
		old.BlockStart = true;
		Task first = _controller.StartAsync (Timer.ControllerId, 1, 5);
		await old.Started.Task;
		Task queued = _controller.StartAsync (Timer.ControllerId, 2, 5);
		_connection = new FakeConnection ();
		await _controller.ConfigureAsync (Settings ("new"));
		await Task.WhenAll (first, queued);
		Assert.That (old.Commands.Count, Is.EqualTo (1));
		Assert.That (_connection.Commands, Is.Empty);
		}
	[Test]
	public async Task PlanRefreshReadsOnly ()
		{
		string[] plans = null;
		_controller.PlansChanged += (_, p) => plans = p;
		await _controller.RefreshAsync (Timer.ControllerId, true);
		Assert.That (plans, Has.Length.EqualTo (3));
		Assert.That (_connection.Commands, Is.Empty);
		}

	[Test]
	public async Task MetadataFromCurrentSessionUpdatesCatalogAndPlansWithoutCommands ()
		{
		var names = new List<string> ();
		var plans = new List<string> ();
		_controller.CatalogChanged += timers => names.Add (timers[0].ZoneName (1));
		_controller.PlansChanged += (_, value) => plans.Add (value[0]);
		FakeConnection old = _connection;
		old.EmitMetadata ("Lawn");
		Assert.That (names, Is.EqualTo (new[] { "Lawn" }));
		Assert.That (plans, Is.EqualTo (new[] { "Lawn" }));
		_connection = new FakeConnection ();
		await _controller.ConfigureAsync (Settings ("new"));
		names.Clear ();
		plans.Clear ();
		old.EmitMetadata ("Stale");
		_connection.EmitMetadata ("Beds");
		Assert.That (names, Is.EqualTo (new[] { "Beds" }));
		Assert.That (plans, Is.EqualTo (new[] { "Beds" }));
		Assert.That (_connection.Commands, Is.Empty);
		}
	[Test]
	public void IdentityCopiesAssignedNamesAndFallsBackPerZone ()
		{
		string[] names = ["Lawn", " ", "Tap"];
		var timer = new TimerIdentity (1, 2, "Timer", "Hub", names);
		names[0] = "Changed";
		Assert.That (timer.ZoneName (1), Is.EqualTo ("Lawn"));
		Assert.That (timer.ZoneName (2), Is.EqualTo ("Zone 2"));
		Assert.That (timer.ZoneName (3), Is.EqualTo ("Tap"));
		Assert.Throws<ArgumentOutOfRangeException> (() => timer.ZoneName (0));
		Assert.Throws<ArgumentOutOfRangeException> (() => timer.ZoneName (4));
		}

	[Test]
	public void HistoryRefreshRequiresReportedActiveToIdleTransition ()
		{
		_connection.Emit (Frame (2, true));
		_connection.Emit (Frame (3, false));
		_connection.Emit (Frame (3, false));
		_connection.Emit (Frame (4, false));
		Assert.That (_connection.HistoryRequests, Is.EquivalentTo (new[] { Timer.Address + ":1:True", Timer.Address + ":2:True", Timer.Address + ":3:True" }));
		Assert.That (_connection.HistoryRequests[0], Does.EndWith (":1:True"));
		Assert.That (_connection.Commands, Is.Empty);
		}

	[Test]
	public async Task EarlyHistoryIsDeliveredAfterCatalogAndOldAccountCallbacksAreIgnored ()
		{
		var calls = new List<string> ();
		_controller.CatalogChanged += _ => calls.Add ("catalog");
		_controller.HistoryChanged += (_, _, record, _) => calls.Add (record.Id);
		FakeConnection old = _connection;
		_connection = new FakeConnection { EarlyHistory = true };
		await _controller.ConfigureAsync (Settings ("history replacement"));
		Assert.That (calls, Is.EqualTo (new[] { "catalog", "fixture" }));
		old.EmitHistory ();
		Assert.That (calls, Has.Count.EqualTo (2));
		_connection.EmitHistory ();
		Assert.That (calls, Has.Count.EqualTo (3));
		}

	private sealed class FakeConnection : IRainPointConnection
		{
#pragma warning disable CS0067
		public event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
		public event Action<int, string[]> PlansChanged;
#pragma warning restore CS0067
#pragma warning disable CS0067
		public event Action<int, int, RecordedUsage, bool> HistoryChanged;
#pragma warning restore CS0067
		internal readonly List<string> HistoryRequests = [];
		public void RequestHistory (int address, int zone, bool completion) => HistoryRequests.Add (address + ":" + zone + ":" + completion);
		public event Action<TimerReading> Reading;
#pragma warning disable CS0067
		public event Action<string> ConnectionState;
#pragma warning restore CS0067
		internal readonly List<string> Commands = [];
		internal readonly TaskCompletionSource<bool> Started = new (TaskCreationOptions.RunContinuationsAsynchronously);
		internal int ConnectCount;
		internal bool EarlyHistory;
		internal void EmitHistory () => HistoryChanged?.Invoke (Timer.Address, 1, new RecordedUsage ("fixture", Now, 1.4m), true);
		internal bool Closed, FailStart, FailFirstStop, BlockStart, CommandFinished, ClosedWhileCommandActive;
		internal void EmitMetadata (string name)
			{
			CatalogChanged?.Invoke (new[] { new TimerIdentity (Timer.HubId, Timer.Address, Timer.Name, "Hub", new[] { name }) });
			PlansChanged?.Invoke (Timer.Address, new[] { name, "", "" });
			}
		internal void Emit (TimerReading reading) => Reading?.Invoke (reading);
		internal void EmitState (string state) => ConnectionState?.Invoke (state);
		public Task<IReadOnlyList<TimerIdentity>> ConnectAsync (DriverSettings settings, CancellationToken token)
			{
			ConnectCount++;
			if (EarlyHistory)
				EmitHistory ();
			return Task.FromResult<IReadOnlyList<TimerIdentity>> (new[] { Timer });
			}
		public async Task StartAsync (int address, int zone, int minutes, CancellationToken token)
			{
			Commands.Add ($"start:{address}:{zone}:{minutes}");
			Started.TrySetResult (true);
			try
				{
				if (FailStart)
					{
					throw new TimeoutException ();
					}
				if (BlockStart)
					{
					await Task.Delay (Timeout.Infinite, token);
					}
				}
			finally
				{
				CommandFinished = true;
				}
			}
		public Task StopAsync (int address, int zone, CancellationToken token)
			{
			Commands.Add ($"stop:{address}:{zone}");
			if (FailFirstStop && zone == 1)
				{
				throw new TimeoutException ();
				}
			return Task.CompletedTask;
			}
		public Task RefreshAsync (CancellationToken token) => Task.CompletedTask;
		public Task<string[]> ReadPlansAsync (int address, CancellationToken token) => Task.FromResult (new[] { "No plans", "No plans", "No plans" });
		public Task CloseAsync ()
			{
			ClosedWhileCommandActive = BlockStart && !CommandFinished;
			Closed = true;
			return Task.CompletedTask;
			}
		}
	}