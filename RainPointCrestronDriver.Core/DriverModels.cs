// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RainPoint.CrestronDriver.Core;

public sealed class DriverSettings
	{
	public DriverSettings (string email, string password, string areaCode, long? homeId = null, long? hubId = null)
		{
		if (string.IsNullOrWhiteSpace (email) || string.IsNullOrEmpty (password))
			{
			throw new ArgumentException ("Email and password are required.");
			}
		if (string.IsNullOrWhiteSpace (areaCode) || !areaCode.All (c => c >= '0' && c <= '9'))
			{
			throw new ArgumentException ("Country calling code must contain digits only.");
			}
		if (homeId <= 0 || hubId <= 0)
			{
			throw new ArgumentException ("Home and hub IDs must be positive when supplied.");
			}
		Email = email.Trim ();
		Password = password;
		AreaCode = areaCode;
		HomeId = homeId;
		HubId = hubId;
		}
	public string Email
		{
		get;
		}
	public string Password
		{
		get;
		}
	public string AreaCode
		{
		get;
		}
	public long? HomeId
		{
		get;
		}
	public long? HubId
		{
		get;
		}
	public bool Matches (DriverSettings other) => other != null && Email == other.Email && Password == other.Password
		&& AreaCode == other.AreaCode && HomeId == other.HomeId && HubId == other.HubId;
	}

public sealed class TimerIdentity (long hubId, int address, string name, string hubName = null, IReadOnlyList<string> zoneNames = null)
	{
	public string HubName { get; } = hubName ?? string.Empty;
	private readonly string[] _zoneNames = Enumerable.Range (1, 3).Select (zone =>
		zoneNames != null && zone <= zoneNames.Count && !string.IsNullOrWhiteSpace (zoneNames[zone - 1]) ? zoneNames[zone - 1] : "Zone " + zone).ToArray ();
	public string ZoneName (int zone) => zone is >= 1 and <= 3 ? _zoneNames[zone - 1] : throw new ArgumentOutOfRangeException (nameof (zone));
	public long HubId { get; } = hubId;
	public int Address { get; } = address;
	public string Name { get; } = string.IsNullOrWhiteSpace (name) ? "RainPoint timer " + address : name;
	public string ControllerId => "rainpoint_" + HubId.ToString (CultureInfo.InvariantCulture) + "_" + Address.ToString (CultureInfo.InvariantCulture);
	}

public sealed class ZoneReading (int zone, bool? active, string mode, decimal? usageLitres, TimeSpan? duration, string alarms, DateTimeOffset? reportedEnd = null, int? alarmCode = null)
	{
	public int? AlarmCode { get; } = alarmCode;
	public int Zone { get; } = zone;
	public bool? Active { get; } = active;
	public string Mode { get; } = mode;
	public decimal? UsageLitres { get; } = usageLitres;
	public TimeSpan? Duration { get; } = duration;
	public string Alarms { get; } = alarms;
	public DateTimeOffset? ReportedEnd { get; } = reportedEnd;
	public int? RemainingSeconds (DateTimeOffset now, bool fresh)
		{
		if (!fresh || !Active.HasValue)
			return null;
		if (Active == false)
			return 0;
		if (!ReportedEnd.HasValue)
			return null;
		TimeSpan left = ReportedEnd.Value - now;
		if (left > TimeSpan.FromHours (12) || Duration.HasValue && left > Duration.Value + TimeSpan.FromSeconds (30))
			return null;
		return left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling (left.TotalSeconds);
		}
	public string TimeLeft (DateTimeOffset now, bool fresh)
		{
		if (!fresh || !Active.HasValue)
			return "Time left unavailable";
		if (Active == false)
			return "Not running";
		if (!ReportedEnd.HasValue)
			return "Time left unavailable";
		TimeSpan left = ReportedEnd.Value - now;
		bool phase = Mode == "Cycle watering" || Mode == "Soaking (cycle active)";
		if (left <= TimeSpan.Zero)
			return phase ? "Awaiting phase update" : "Awaiting stop confirmation";
		if (left > TimeSpan.FromHours (12) || Duration.HasValue && left > Duration.Value + TimeSpan.FromSeconds (30))
			return "Time left unavailable";
		int seconds = (int)Math.Ceiling (left.TotalSeconds);
		return (phase ? "Phase time left: " : "Time left: ") + (seconds / 60).ToString ("00", CultureInfo.InvariantCulture)
			+ ":" + (seconds % 60).ToString ("00", CultureInfo.InvariantCulture);
		}
	public string Status => Active == true ? Mode : Active == false ? "Idle" : "Unknown";
	public string Usage => UsageLitres.HasValue ? UsageLitres.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " L last usage" : "Last usage unknown";
	public string DurationDisplay => Duration.HasValue ? Duration.Value.TotalMinutes.ToString ("0.#", CultureInfo.InvariantCulture) + " min configured" : "Configured duration unknown";
	}

public sealed class TimerReading (TimerIdentity timer, long revision, bool? hubOnline, DateTimeOffset? reportTime,
	IReadOnlyList<ZoneReading> zones, string battery, string signal, DateTimeOffset? contactTime = null, bool? batteryLow = null)
	{
	public bool? BatteryLow { get; } = batteryLow;
	public TimerIdentity Timer { get; } = timer;
	public long Revision { get; } = revision;
	public bool? HubOnline { get; } = hubOnline;
	public DateTimeOffset? ReportTime { get; } = reportTime;
	/// <summary>Last successful cloud read or accepted timer push; distinct from the timer's data-change time.</summary>
	public DateTimeOffset? ContactTime { get; } = contactTime ?? reportTime;
	public IReadOnlyList<ZoneReading> Zones { get; } = Array.AsReadOnly (zones.ToArray ());
	public string Battery { get; } = battery;
	public string Signal { get; } = signal;
	public bool IsFresh (DateTimeOffset now, bool liveUpdates = false) => HubOnline == true && ReportTime.HasValue
		&& ContactTime <= now.AddSeconds (30) && (liveUpdates || now - ContactTime <= TimeSpan.FromSeconds (90));
	public string Tile (DateTimeOffset now, bool liveUpdates = false)
		{
		if (HubOnline == false)
			{
			return "Hub offline";
			}
		if (!IsFresh (now, liveUpdates))
			{
			return "Status unavailable";
			}
		int[] active = Zones.Where (z => z.Active == true).Select (z => z.Zone).ToArray ();
		string running = active.Length == 1 ? "Zone " + active[0] + " active" : "Zones " + string.Join (", ", active) + " active";
		bool complete = Zones.Count == 3 && Zones.All (z => z.Active.HasValue);
		return active.Length > 0 ? running + (complete ? "" : " / others unknown") : complete ? "All zones idle" : "Status unknown";
		}
	}

public static class Selection
	{
	public static T Unique<T> (IEnumerable<T> items, Func<T, long> id, long? selected, string label)
		{
		T[] matches = items.Where (x => !selected.HasValue || id (x) == selected.Value).ToArray ();
		if (matches.Length != 1)
			{
			throw new InvalidOperationException ("Select an unambiguous " + label + " ID in driver configuration.");
			}
		return matches[0];
		}
	}