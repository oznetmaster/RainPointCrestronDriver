// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

using NUnit.Framework;

using RainPoint.CrestronDriver;
using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Lifecycle.Tests;

public sealed partial class ExtensionTests
	{
	private static TimerReading ProgrammingFrame (long revision, bool? one = false, bool? two = false, bool? three = false,
		int? alarm = 0, bool? battery = false, bool fresh = true) => new (new TimerIdentity (1, 2, "Garden timer"), revision, true, DateTimeOffset.UtcNow,
		new[] { one, two, three }.Select ((active, index) => new ZoneReading (index + 1, active, "Watering", 1.4m, TimeSpan.FromMinutes (5),
			alarm == 0 ? "No reported alarms" : "Reported alarm", DateTimeOffset.UtcNow.AddSeconds (60), alarm)).ToArray (),
		battery == true ? "Battery low" : "Battery normal", "RF -70 dBm", DateTimeOffset.UtcNow.AddMinutes (fresh ? 0 : -10), battery);

	[Test]
	public void ProgrammableDefinitionsHaveSdkBindingsAndTranslations ()
		{
		var definition = _timer.GetState ().Definition;
		var labels = JsonSerializer.Deserialize<Dictionary<string, string>> (File.ReadAllText (Path.Combine (Data, "Translations", "en-US.json")));
		Type type = typeof (RainPointTimerEntity);
		var events = type.GetEvents ().Where (e => e.GetCustomAttribute<EntityEventMetadataAttribute> ()?.Programmable == true).ToArray ();
		Assert.That (events, Has.Length.EqualTo (18));
		foreach (EventInfo item in events)
			{
			var attribute = item.GetCustomAttribute<EntityEventAttribute> ();
			Assert.That (definition.Events.ContainsKey (attribute.Id), Is.True);
			Assert.That (labels.ContainsKey (attribute.NameLocalizationKey), Is.True);
			}
		var properties = type.GetProperties ().Where (p => p.GetCustomAttribute<EntityPropertyMetadataAttribute> ()?.Programmable == true).ToArray ();
		Assert.That (properties, Has.Length.EqualTo (26));
		foreach (PropertyInfo item in properties)
			{
			var attribute = item.GetCustomAttribute<EntityPropertyAttribute> ();
			Assert.That (definition.Properties.ContainsKey (attribute.Id), Is.True);
			Assert.That (labels.ContainsKey (attribute.NameLocalizationKey), Is.True);
			}
		var commands = type.GetMethods ().Where (m => m.GetCustomAttribute<EntityCommandMetadataAttribute> ()?.Programmable == true).ToArray ();
		Assert.That (commands, Has.Length.EqualTo (10));
		foreach (MethodInfo item in commands)
			{
			var attribute = item.GetCustomAttribute<EntityCommandAttribute> ();
			Assert.That (definition.Commands.ContainsKey (attribute.Id), Is.True);
			Assert.That (labels.ContainsKey (attribute.NameLocalizationKey), Is.True);
			}
		}

	[TestCase (1), TestCase (2), TestCase (3)]
	public void ZoneEventsRequireTransitionsAndIgnoreAcknowledgementsAndDuplicateReports (int zone)
		{
		int starts = 0, stops = 0, timerStarts = 0, timerStops = 0;
		EventHandler started = (_, _) => starts++;
		EventHandler stopped = (_, _) => stops++;
		typeof (RainPointTimerEntity).GetEvent ("Zone" + zone + "Started").AddEventHandler (_timer, started);
		typeof (RainPointTimerEntity).GetEvent ("Zone" + zone + "Stopped").AddEventHandler (_timer, stopped);
		_timer.IrrigationStarted += (_, _) => timerStarts++;
		_timer.AllZonesStopped += (_, _) => timerStops++;
		_timer.Update (ProgrammingFrame (1));
		_timer.CommandResult (zone, "Accepted; awaiting timer report");
		Assert.That (starts + stops + timerStarts + timerStops, Is.Zero);
		_timer.Update (ProgrammingFrame (2, zone == 1, zone == 2, zone == 3));
		_timer.Update (ProgrammingFrame (3, zone == 1, zone == 2, zone == 3));
		_timer.Update (ProgrammingFrame (2));
		Assert.That (starts, Is.EqualTo (1));
		Assert.That (timerStarts, Is.EqualTo (1));
		Assert.That (_timer.ActiveZoneCount, Is.EqualTo (1));
		_timer.Update (ProgrammingFrame (4));
		Assert.That (stops, Is.EqualTo (1));
		Assert.That (timerStops, Is.EqualTo (1));
		Assert.That (_timer.ActivityState, Is.EqualTo ("Idle"));
		}

	[Test]
	public void UnknownStartupAndRecoveryDoNotInventWateringTransitions ()
		{
		int starts = 0, stops = 0, lost = 0, restored = 0;
		_timer.Zone1Started += (_, _) => starts++;
		_timer.Zone1Stopped += (_, _) => stops++;
		_timer.StatusUnavailable += (_, _) => lost++;
		_timer.StatusRestored += (_, _) => restored++;
		_timer.SetConnection ("Connecting");
		_timer.Update (ProgrammingFrame (1, true));
		Assert.That (starts + restored, Is.Zero);
		_timer.SetConnection ("AuthenticationRequired");
		Assert.That (_timer.Zone1State, Is.EqualTo ("Unknown"));
		Assert.That (_timer.Zone1RemainingSeconds, Is.EqualTo (-1));
		_timer.Update (ProgrammingFrame (2));
		Assert.That (lost, Is.EqualTo (1));
		Assert.That (restored, Is.EqualTo (1));
		_timer.Update (ProgrammingFrame (3, null));
		Assert.That (_timer.ActiveZoneCount, Is.EqualTo (-1));
		_timer.Update (ProgrammingFrame (4, true));
		Assert.That (starts + stops, Is.Zero);
		}

	[Test]
	public void AggregateStopWaitsForAllZonesAndUnknownCannotMeanIdle ()
		{
		int stops = 0;
		_timer.AllZonesStopped += (_, _) => stops++;
		_timer.Update (ProgrammingFrame (1, true, true));
		_timer.Update (ProgrammingFrame (2, false, true));
		Assert.That (stops, Is.Zero);
		_timer.Update (ProgrammingFrame (3, false, null));
		Assert.That (_timer.ActivityState, Is.EqualTo ("Unknown"));
		_timer.Update (ProgrammingFrame (4));
		Assert.That (stops, Is.Zero, "Recovery to idle is a baseline, not an observed stop.");
		}

	[Test]
	public void AlarmsAndBatteryUseTypedReportsAndKnownTransitions ()
		{
		int raised = 0, cleared = 0, low = 0, normal = 0;
		_timer.Zone1AlarmRaised += (_, _) => raised++;
		_timer.Zone2AlarmRaised += (_, _) => raised++;
		_timer.Zone3AlarmRaised += (_, _) => raised++;
		_timer.Zone1AlarmCleared += (_, _) => cleared++;
		_timer.Zone2AlarmCleared += (_, _) => cleared++;
		_timer.Zone3AlarmCleared += (_, _) => cleared++;
		_timer.BatteryLowReported += (_, _) => low++;
		_timer.BatteryRestored += (_, _) => normal++;
		_timer.Update (ProgrammingFrame (1));
		_timer.Update (ProgrammingFrame (2, alarm: 8, battery: true));
		_timer.Update (ProgrammingFrame (3, alarm: 8, battery: true));
		Assert.That (raised, Is.EqualTo (3));
		Assert.That (low, Is.EqualTo (1));
		Assert.That (_timer.Zone1AlarmState, Is.EqualTo ("Active"));
		_timer.Update (ProgrammingFrame (4));
		Assert.That (cleared, Is.EqualTo (3));
		Assert.That (normal, Is.EqualTo (1));
		_timer.Update (ProgrammingFrame (5, alarm: null, battery: null));
		_timer.Update (ProgrammingFrame (6, alarm: 8, battery: true));
		Assert.That (raised, Is.EqualTo (3));
		Assert.That (low, Is.EqualTo (1));
		}

	[Test]
	public void NumericPropertiesUseDeviceEndTimeAndReportedUsage ()
		{
		Assert.That (_timer.Zone1LastUsageLitres, Is.EqualTo (-1));
		_timer.Update (ProgrammingFrame (1, true));
		Assert.That (_timer.Zone1RemainingSeconds, Is.InRange (58, 60));
		Assert.That (_timer.Zone2RemainingSeconds, Is.Zero);
		Assert.That (_timer.Zone1LastUsageLitres, Is.EqualTo (1.4));
		_timer.Update (ProgrammingFrame (2, true, fresh: false));
		Assert.That (_timer.Zone1RemainingSeconds, Is.EqualTo (-1));
		Assert.That (_timer.Zone1LastUsageLitres, Is.EqualTo (1.4), "Historical volume remains historical.");
		}

	[TestCase (1), TestCase (2), TestCase (3)]
	public async Task ExplicitDurationCommandsTargetOnlySelectedZone (int zone)
		{
		var connection = new RecordingConnection ();
		await _controller.ShutdownAsync ();
		var controller = _controller = new IrrigationController (() => connection);
		using var entity = new RainPointTimerEntity (new TimerIdentity (1, 2, "Garden"), controller, _args, _resources);
		await controller.ConfigureAsync (new DriverSettings ("offline@example.invalid", "test", "44"));
		if (zone == 1)
			entity.StartZone1For (7);
		else if (zone == 2)
			entity.StartZone2For (7);
		else
			entity.StartZone3For (7);
		var command = await connection.Started.Task.WaitAsync (TimeSpan.FromSeconds (5));
		Assert.That (command, Is.EqualTo ((2, zone, 7)));
		Assert.That (connection.Count, Is.EqualTo (1));
		Assert.That (entity.Zone1Minutes, Is.EqualTo (5), "Programming does not edit UI duration.");
		await controller.ShutdownAsync ();
		}

	[TestCase (1), TestCase (2), TestCase (3)]
	public async Task ReflectedSdkDurationCommandDispatchesNamedArgument (int zone)
		{
		var connection = new RecordingConnection ();
		await _controller.ShutdownAsync ();
		var controller = _controller = new IrrigationController (() => connection);
		using var entity = new RainPointTimerEntity (new TimerIdentity (1, 2, "Garden"), controller, _args, _resources);
		await controller.ConfigureAsync (new DriverSettings ("offline@example.invalid", "test", "44"));
		entity.ExecuteCommand ("startZone" + zone + "For", new System.Collections.Generic.Dictionary<string, DriverEntityValue> { ["minutes"] = new DriverEntityValue (7) }, _ => { });
		Assert.That (await connection.Started.Task.WaitAsync (TimeSpan.FromSeconds (5)), Is.EqualTo ((2, zone, 7)));
		Assert.That (connection.Count, Is.EqualTo (1));
		await controller.ShutdownAsync ();
		}

	[TestCase (0), TestCase (121)]
	public void ExplicitDurationRejectsInvalidMinutes (int minutes)
		{
		Assert.Throws<ArgumentOutOfRangeException> (() => _timer.StartZone1For (minutes));
		Assert.Throws<ArgumentOutOfRangeException> (() => _timer.StartZone2For (minutes));
		Assert.Throws<ArgumentOutOfRangeException> (() => _timer.StartZone3For (minutes));
		}

	[Test]
	public void StopAllDisablesOnlyForConfirmedIdleWithoutPendingCommands ()
		{
		_timer.Update (ProgrammingFrame (1));
		Assert.That (_timer.CanStop, Is.False);
		_timer.CommandResult (1, "Sending start");
		Assert.That (_timer.CanStop, Is.True, "A pending start must remain stoppable.");
		_timer.CommandResult (1, "New report: Idle");
		Assert.That (_timer.CanStop, Is.False);
		_timer.Update (ProgrammingFrame (2, true));
		Assert.That (_timer.CanStop, Is.True);
		_timer.Update (ProgrammingFrame (3));
		Assert.That (_timer.CanStop, Is.False);
		_timer.Update (ProgrammingFrame (4, null));
		Assert.That (_timer.CanStop, Is.True, "Unknown is not confirmed idle.");
		_timer.Update (ProgrammingFrame (5, fresh: false));
		Assert.That (_timer.CanStop, Is.True, "A stale idle report does not prevent stopping.");
		_timer.SetConnection ("AuthenticationRequired");
		Assert.That (_timer.CanStop, Is.False);
		}
	private sealed class RecordingConnection : IRainPointConnection
		{
#pragma warning disable CS0067
		public event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
		public event Action<int, string[]> PlansChanged;
#pragma warning restore CS0067
#pragma warning disable CS0067
		public event Action<int, int, RecordedUsage, bool> HistoryChanged;
#pragma warning restore CS0067
		public void RequestHistory (int address, int zone, bool completion)
			{
			}
		public event Action<TimerReading> Reading;
#pragma warning disable CS0067
		public event Action<string> ConnectionState;
#pragma warning restore CS0067
		public readonly TaskCompletionSource<(int, int, int)> Started = new (TaskCreationOptions.RunContinuationsAsynchronously);
		public int Count;
		public Task<IReadOnlyList<TimerIdentity>> ConnectAsync (DriverSettings settings, CancellationToken token) =>
			Task.FromResult<IReadOnlyList<TimerIdentity>> (new[] { new TimerIdentity (1, 2, "Garden") });
		public Task RefreshAsync (CancellationToken token)
			{
			Reading?.Invoke (ProgrammingFrame (1));
			return Task.CompletedTask;
			}
		public Task StartAsync (int address, int zone, int minutes, CancellationToken token)
			{
			Count++;
			Started.TrySetResult ((address, zone, minutes));
			return Task.CompletedTask;
			}
		public Task StopAsync (int address, int zone, CancellationToken token) => throw new AssertionException ("Unexpected stop");
		public Task<string[]> ReadPlansAsync (int address, CancellationToken token) => Task.FromResult (new[] { "", "", "" });
		public Task CloseAsync () => Task.CompletedTask;
		}
	}