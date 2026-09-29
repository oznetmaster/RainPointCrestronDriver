// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;

using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

using RainPoint.CrestronDriver.Core;

namespace RainPoint.CrestronDriver;

/// <summary>
/// Presents one timer's reported state, zone controls and Crestron Home programming entities.
/// </summary>
internal sealed partial class RainPointTimerEntity
	{
	private readonly bool?[] _programZone = new bool?[3];
	private readonly bool?[] _programAlarm = new bool?[3];
	private bool? _programActive;
	private bool? _programBattery;
	private bool? _programAvailable;

	/// <summary>
	/// Gets Active, Idle or Unknown from fresh reported zone activity.
	/// </summary>
	[EntityProperty (Id = "activityState", FriendlyName = "Timer activity", NameLocalizationKey = "Programming_activityState")]
	[EntityPropertyMetadata (Programmable = true)]
	public string ActivityState
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("activityState", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Gets the reported active-zone count, or -1 when a complete fresh count is unavailable.
	/// </summary>
	[EntityProperty (Id = "activeZoneCount", FriendlyName = "Active zone count (-1 unknown)", NameLocalizationKey = "Programming_activeZoneCount")]
	[EntityPropertyMetadata (Programmable = true)]
	public int ActiveZoneCount
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("activeZoneCount", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets Active, Idle or Unknown for zone 1; stale feedback is Unknown.
	/// </summary>
	[EntityProperty (Id = "zone1State", FriendlyName = "Zone 1: activity", NameLocalizationKey = "Programming_zone1State")]
	[EntityPropertyMetadata (Programmable = true)]
	public string Zone1State
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1State", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Gets the estimated remaining seconds for zone 1, zero when idle, or -1 when unavailable.
	/// </summary>
	[EntityProperty (Id = "zone1RemainingSeconds", FriendlyName = "Zone 1: time left (seconds; -1 unknown)", NameLocalizationKey = "Programming_zone1RemainingSeconds")]
	[EntityPropertyMetadata (Programmable = true)]
	public int Zone1RemainingSeconds
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1RemainingSeconds", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets the last status-reported usage for zone 1 in litres, or -1 when unknown.
	/// </summary>
	[EntityProperty (Id = "zone1LastUsageLitres", FriendlyName = "Zone 1: last usage (litres; -1 unknown)", NameLocalizationKey = "Programming_zone1LastUsageLitres")]
	[EntityPropertyMetadata (Programmable = true)]
	public double Zone1LastUsageLitres
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1LastUsageLitres", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets Active, Clear or Unknown for zone 1 from fresh reported alarm feedback.
	/// </summary>
	[EntityProperty (Id = "zone1AlarmState", FriendlyName = "Zone 1: alarm state", NameLocalizationKey = "Programming_zone1AlarmState")]
	[EntityPropertyMetadata (Programmable = true)]
	public string Zone1AlarmState
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1AlarmState", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Gets Active, Idle or Unknown for zone 2; stale feedback is Unknown.
	/// </summary>
	[EntityProperty (Id = "zone2State", FriendlyName = "Zone 2: activity", NameLocalizationKey = "Programming_zone2State")]
	[EntityPropertyMetadata (Programmable = true)]
	public string Zone2State
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2State", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Gets the estimated remaining seconds for zone 2, zero when idle, or -1 when unavailable.
	/// </summary>
	[EntityProperty (Id = "zone2RemainingSeconds", FriendlyName = "Zone 2: time left (seconds; -1 unknown)", NameLocalizationKey = "Programming_zone2RemainingSeconds")]
	[EntityPropertyMetadata (Programmable = true)]
	public int Zone2RemainingSeconds
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2RemainingSeconds", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets the last status-reported usage for zone 2 in litres, or -1 when unknown.
	/// </summary>
	[EntityProperty (Id = "zone2LastUsageLitres", FriendlyName = "Zone 2: last usage (litres; -1 unknown)", NameLocalizationKey = "Programming_zone2LastUsageLitres")]
	[EntityPropertyMetadata (Programmable = true)]
	public double Zone2LastUsageLitres
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2LastUsageLitres", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets Active, Clear or Unknown for zone 2 from fresh reported alarm feedback.
	/// </summary>
	[EntityProperty (Id = "zone2AlarmState", FriendlyName = "Zone 2: alarm state", NameLocalizationKey = "Programming_zone2AlarmState")]
	[EntityPropertyMetadata (Programmable = true)]
	public string Zone2AlarmState
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2AlarmState", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Gets Active, Idle or Unknown for zone 3; stale feedback is Unknown.
	/// </summary>
	[EntityProperty (Id = "zone3State", FriendlyName = "Zone 3: activity", NameLocalizationKey = "Programming_zone3State")]
	[EntityPropertyMetadata (Programmable = true)]
	public string Zone3State
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3State", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Gets the estimated remaining seconds for zone 3, zero when idle, or -1 when unavailable.
	/// </summary>
	[EntityProperty (Id = "zone3RemainingSeconds", FriendlyName = "Zone 3: time left (seconds; -1 unknown)", NameLocalizationKey = "Programming_zone3RemainingSeconds")]
	[EntityPropertyMetadata (Programmable = true)]
	public int Zone3RemainingSeconds
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3RemainingSeconds", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets the last status-reported usage for zone 3 in litres, or -1 when unknown.
	/// </summary>
	[EntityProperty (Id = "zone3LastUsageLitres", FriendlyName = "Zone 3: last usage (litres; -1 unknown)", NameLocalizationKey = "Programming_zone3LastUsageLitres")]
	[EntityPropertyMetadata (Programmable = true)]
	public double Zone3LastUsageLitres
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3LastUsageLitres", new DriverEntityValue (value));
			}
		} = -1;

	/// <summary>
	/// Gets Active, Clear or Unknown for zone 3 from fresh reported alarm feedback.
	/// </summary>
	[EntityProperty (Id = "zone3AlarmState", FriendlyName = "Zone 3: alarm state", NameLocalizationKey = "Programming_zone3AlarmState")]
	[EntityPropertyMetadata (Programmable = true)]
	public string Zone3AlarmState
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3AlarmState", new DriverEntityValue (value));
			}
		} = "Unknown";

	/// <summary>
	/// Occurs when known aggregate activity changes from idle to active; initial or unknown state does not synthesize a transition.
	/// </summary>
	[EntityEvent (Id = "irrigationStarted", FriendlyName = "Timer became active", NameLocalizationKey = "Programming_irrigationStarted")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler IrrigationStarted;

	/// <summary>
	/// Occurs when known aggregate activity changes from active to all zones idle.
	/// </summary>
	[EntityEvent (Id = "allZonesStopped", FriendlyName = "All zones became idle", NameLocalizationKey = "Programming_allZonesStopped")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler AllZonesStopped;

	/// <summary>
	/// Occurs when a known normal battery state changes to a reported low battery.
	/// </summary>
	[EntityEvent (Id = "batteryLowReported", FriendlyName = "Battery became low", NameLocalizationKey = "Programming_batteryLowReported")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler BatteryLowReported;

	/// <summary>
	/// Occurs when a known low battery state changes to a reported normal battery.
	/// </summary>
	[EntityEvent (Id = "batteryRestored", FriendlyName = "Battery returned to normal", NameLocalizationKey = "Programming_batteryRestored")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler BatteryRestored;

	/// <summary>
	/// Occurs when previously available timer feedback becomes unavailable.
	/// </summary>
	[EntityEvent (Id = "statusUnavailable", FriendlyName = "Status became unavailable", NameLocalizationKey = "Programming_statusUnavailable")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler StatusUnavailable;

	/// <summary>
	/// Occurs when previously unavailable timer feedback becomes available again.
	/// </summary>
	[EntityEvent (Id = "statusRestored", FriendlyName = "Status became available again", NameLocalizationKey = "Programming_statusRestored")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler StatusRestored;

	/// <summary>
	/// Occurs when known zone 1 activity changes from idle to active.
	/// </summary>
	[EntityEvent (Id = "zone1Started", FriendlyName = "Zone 1 became active", NameLocalizationKey = "Programming_zone1Started")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone1Started;

	/// <summary>
	/// Occurs when known zone 1 activity changes from active to idle.
	/// </summary>
	[EntityEvent (Id = "zone1Stopped", FriendlyName = "Zone 1 became idle", NameLocalizationKey = "Programming_zone1Stopped")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone1Stopped;

	/// <summary>
	/// Occurs when a known clear alarm state for zone 1 changes to an active alarm.
	/// </summary>
	[EntityEvent (Id = "zone1AlarmRaised", FriendlyName = "Zone 1 alarm reported", NameLocalizationKey = "Programming_zone1AlarmRaised")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone1AlarmRaised;

	/// <summary>
	/// Occurs when a known active alarm state for zone 1 changes to clear.
	/// </summary>
	[EntityEvent (Id = "zone1AlarmCleared", FriendlyName = "Zone 1 alarms cleared", NameLocalizationKey = "Programming_zone1AlarmCleared")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone1AlarmCleared;

	/// <summary>
	/// Occurs when known zone 2 activity changes from idle to active.
	/// </summary>
	[EntityEvent (Id = "zone2Started", FriendlyName = "Zone 2 became active", NameLocalizationKey = "Programming_zone2Started")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone2Started;

	/// <summary>
	/// Occurs when known zone 2 activity changes from active to idle.
	/// </summary>
	[EntityEvent (Id = "zone2Stopped", FriendlyName = "Zone 2 became idle", NameLocalizationKey = "Programming_zone2Stopped")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone2Stopped;

	/// <summary>
	/// Occurs when a known clear alarm state for zone 2 changes to an active alarm.
	/// </summary>
	[EntityEvent (Id = "zone2AlarmRaised", FriendlyName = "Zone 2 alarm reported", NameLocalizationKey = "Programming_zone2AlarmRaised")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone2AlarmRaised;

	/// <summary>
	/// Occurs when a known active alarm state for zone 2 changes to clear.
	/// </summary>
	[EntityEvent (Id = "zone2AlarmCleared", FriendlyName = "Zone 2 alarms cleared", NameLocalizationKey = "Programming_zone2AlarmCleared")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone2AlarmCleared;

	/// <summary>
	/// Occurs when known zone 3 activity changes from idle to active.
	/// </summary>
	[EntityEvent (Id = "zone3Started", FriendlyName = "Zone 3 became active", NameLocalizationKey = "Programming_zone3Started")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone3Started;

	/// <summary>
	/// Occurs when known zone 3 activity changes from active to idle.
	/// </summary>
	[EntityEvent (Id = "zone3Stopped", FriendlyName = "Zone 3 became idle", NameLocalizationKey = "Programming_zone3Stopped")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone3Stopped;

	/// <summary>
	/// Occurs when a known clear alarm state for zone 3 changes to an active alarm.
	/// </summary>
	[EntityEvent (Id = "zone3AlarmRaised", FriendlyName = "Zone 3 alarm reported", NameLocalizationKey = "Programming_zone3AlarmRaised")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone3AlarmRaised;

	/// <summary>
	/// Occurs when a known active alarm state for zone 3 changes to clear.
	/// </summary>
	[EntityEvent (Id = "zone3AlarmCleared", FriendlyName = "Zone 3 alarms cleared", NameLocalizationKey = "Programming_zone3AlarmCleared")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler Zone3AlarmCleared;

	/// <summary>
	/// Requests timed watering for zone 1 for the supplied whole-minute duration.
	/// </summary>
	/// <param name="minutes">The watering duration in whole minutes, from 1 through 120.</param>
	[EntityCommand (Id = "startZone1For", FriendlyName = "Start zone 1 for minutes", NameLocalizationKey = "Programming_startZone1For")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartZone1For ([EntityParameter (RangeMinimum = 1, RangeMaximum = 120)] int minutes) =>
		_ = _controller.StartAsync (ControllerId, 1, minutes);

	/// <summary>
	/// Requests timed watering for zone 2 for the supplied whole-minute duration.
	/// </summary>
	/// <param name="minutes">The watering duration in whole minutes, from 1 through 120.</param>
	[EntityCommand (Id = "startZone2For", FriendlyName = "Start zone 2 for minutes", NameLocalizationKey = "Programming_startZone2For")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartZone2For ([EntityParameter (RangeMinimum = 1, RangeMaximum = 120)] int minutes) =>
		_ = _controller.StartAsync (ControllerId, 2, minutes);

	/// <summary>
	/// Requests timed watering for zone 3 for the supplied whole-minute duration.
	/// </summary>
	/// <param name="minutes">The watering duration in whole minutes, from 1 through 120.</param>
	[EntityCommand (Id = "startZone3For", FriendlyName = "Start zone 3 for minutes", NameLocalizationKey = "Programming_startZone3For")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartZone3For ([EntityParameter (RangeMinimum = 1, RangeMaximum = 120)] int minutes) =>
		_ = _controller.StartAsync (ControllerId, 3, minutes);

	// Only consecutive known reports generate transitions. Startup, unknown data and
	// loss of availability reset report baselines; command acknowledgements do not.
	private void RenderProgramming (bool fresh)
		{
		var events = new List<EventHandler> ();
		void Transition (ref bool? previous, bool? current, EventHandler on, EventHandler off)
			{
			if (previous.HasValue && current.HasValue && previous != current)
				events.Add (current.Value ? on : off);
			previous = current;
			}
		if (fresh || _programAvailable.HasValue)
			Transition (ref _programAvailable, fresh, StatusRestored, StatusUnavailable);
		ZoneReading[] zones = Enumerable.Range (1, _zoneCount).Select (z => _reading?.Zones.FirstOrDefault (x => x.Zone == z)).ToArray ();
		bool complete = fresh && zones.All (z => z?.Active.HasValue == true);
		bool? active = !fresh ? null : zones.Any (z => z?.Active == true) ? true : complete ? false : null;
		ActivityState = active == true ? "Active" : active == false ? "Idle" : "Unknown";
		ActiveZoneCount = complete ? zones.Count (z => z.Active == true) : -1;
		Transition (ref _programActive, active, IrrigationStarted, AllZonesStopped);
		Transition (ref _programBattery, fresh ? _reading?.BatteryLow : null, BatteryLowReported, BatteryRestored);

		ZoneReading zone1 = zones[0];
		bool? active1 = fresh ? zone1?.Active : null;
		bool? alarm1 = fresh && zone1?.AlarmCode.HasValue == true ? zone1.AlarmCode != 0 : (bool?)null;
		Zone1State = active1 == true ? "Active" : active1 == false ? "Idle" : "Unknown";
		Zone1RemainingSeconds = zone1?.RemainingSeconds (DateTimeOffset.UtcNow, fresh) ?? -1;
		Zone1LastUsageLitres = zone1?.UsageLitres is decimal usage1 ? (double)usage1 : -1;
		Zone1AlarmState = alarm1 == true ? "Active" : alarm1 == false ? "Clear" : "Unknown";
		Transition (ref _programZone[0], active1, Zone1Started, Zone1Stopped);
		Transition (ref _programAlarm[0], alarm1, Zone1AlarmRaised, Zone1AlarmCleared);

		if (_zoneCount >= 2)
			{
			ZoneReading zone2 = zones[1];
			bool? active2 = fresh ? zone2?.Active : null;
			bool? alarm2 = fresh && zone2?.AlarmCode.HasValue == true ? zone2.AlarmCode != 0 : (bool?)null;
			Zone2State = active2 == true ? "Active" : active2 == false ? "Idle" : "Unknown";
			Zone2RemainingSeconds = zone2?.RemainingSeconds (DateTimeOffset.UtcNow, fresh) ?? -1;
			Zone2LastUsageLitres = zone2?.UsageLitres is decimal usage2 ? (double)usage2 : -1;
			Zone2AlarmState = alarm2 == true ? "Active" : alarm2 == false ? "Clear" : "Unknown";
			Transition (ref _programZone[1], active2, Zone2Started, Zone2Stopped);
			Transition (ref _programAlarm[1], alarm2, Zone2AlarmRaised, Zone2AlarmCleared);
			}

		if (_zoneCount >= 3)
			{
			ZoneReading zone3 = zones[2];
			bool? active3 = fresh ? zone3?.Active : null;
			bool? alarm3 = fresh && zone3?.AlarmCode.HasValue == true ? zone3.AlarmCode != 0 : (bool?)null;
			Zone3State = active3 == true ? "Active" : active3 == false ? "Idle" : "Unknown";
			Zone3RemainingSeconds = zone3?.RemainingSeconds (DateTimeOffset.UtcNow, fresh) ?? -1;
			Zone3LastUsageLitres = zone3?.UsageLitres is decimal usage3 ? (double)usage3 : -1;
			Zone3AlarmState = alarm3 == true ? "Active" : alarm3 == false ? "Clear" : "Unknown";
			Transition (ref _programZone[2], active3, Zone3Started, Zone3Stopped);
			Transition (ref _programAlarm[2], alarm3, Zone3AlarmRaised, Zone3AlarmCleared);
			}

		foreach (EventHandler handler in events)
			handler?.Invoke (this, EventArgs.Empty);
		}
	}