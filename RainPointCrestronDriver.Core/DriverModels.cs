// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RainPoint.CrestronDriver.Core;

/// <summary>
/// Holds validated account credentials and optional selectors for one cloud home and hub.
/// </summary>
public sealed class DriverSettings
	{
	/// <summary>
	/// Validates credentials, the numeric country calling code and optional positive resource selectors.
	/// </summary>
	/// <param name="email">The account email address.</param>
	/// <param name="password">The account password; never include it in logs.</param>
	/// <param name="areaCode">The country calling code as digits without a plus sign.</param>
	/// <param name="homeId">The positive cloud home ID, or null for unique discovery.</param>
	/// <param name="hubId">The cloud hub ID; null selects unique discovery where permitted.</param>
	/// <exception cref="System.ArgumentException">Email and password are required. Country calling code must contain digits only. Home and hub IDs must be positive when supplied.</exception>
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
	/// <summary>
	/// Gets the trimmed account email address.
	/// </summary>
	public string Email
		{
		get;
		}
	/// <summary>
	/// Gets the account password retained for the configured session; do not log this value.
	/// </summary>
	public string Password
		{
		get;
		}
	/// <summary>
	/// Gets the numeric country calling code without a plus sign.
	/// </summary>
	public string AreaCode
		{
		get;
		}
	/// <summary>
	/// Gets the selected home ID, or null to require unique discovery.
	/// </summary>
	public long? HomeId
		{
		get;
		}
	/// <summary>
	/// Gets the selected hub ID, or null to require unique discovery.
	/// </summary>
	public long? HubId
		{
		get;
		}
	/// <summary>
	/// Compares all credentials and resource selectors to avoid unnecessary session replacement.
	/// </summary>
	/// <param name="other">The settings to compare, or null.</param>
	/// <returns>True only when all stored credentials and selectors match.</returns>
	public bool Matches (DriverSettings other) => other != null && Email == other.Email && Password == other.Password
		&& AreaCode == other.AreaCode && HomeId == other.HomeId && HubId == other.HubId;
	}

/// <summary>
/// Identifies a timer by hub ID and RF address and describes its supported zones.
/// </summary>
/// <param name="hubId">The cloud identifier of the timer's parent hub.</param>
/// <param name="address">The timer RF address within the configured hub.</param>
/// <param name="name">The assigned display name.</param>
/// <param name="hubName">The assigned hub name, or null when unavailable.</param>
/// <param name="zoneNames">Assigned zone names in one-based zone order, or null to use numbered defaults.</param>
/// <param name="model">The reported timer model identifier.</param>
public sealed class TimerIdentity (long hubId, int address, string name, string hubName = null, IReadOnlyList<string> zoneNames = null, string model = "HTV345FRF")
	{
	/// <summary>
	/// Gets the reported timer model identifier.
	/// </summary>
	public string Model { get; } = model;
	/// <summary>
	/// Gets the recognized model's zone count, from one through three.
	/// </summary>
	public int ZoneCount { get; } = model?.ToUpperInvariant () switch
		{
			"HTV145FRF" => 1,
			"HTV245FRF" => 2,
			"HTV345FRF" => 3,
			_ => throw new ArgumentException ("Unsupported timer model.", nameof (model))
		};
	/// <summary>
	/// Gets the assigned hub name, or an empty string if none was supplied.
	/// </summary>
	public string HubName { get; } = hubName ?? string.Empty;
	private readonly string[] _zoneNames = Enumerable.Range (1, 3).Select (zone =>
		zoneNames != null && zone <= zoneNames.Count && !string.IsNullOrWhiteSpace (zoneNames[zone - 1]) ? zoneNames[zone - 1] : "Zone " + zone).ToArray ();
	/// <summary>
	/// Returns the assigned name for an existing zone, with a numbered fallback for missing names.
	/// </summary>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <returns>The assigned zone name or its numbered fallback.</returns>
	/// <exception cref="System.ArgumentOutOfRangeException">An argument is outside the supported range described above.</exception>
	public string ZoneName (int zone) => zone >= 1 && zone <= ZoneCount ? _zoneNames[zone - 1] : throw new ArgumentOutOfRangeException (nameof (zone));
	/// <summary>
	/// Gets the owning cloud hub ID.
	/// </summary>
	public long HubId { get; } = hubId;
	/// <summary>
	/// Gets the timer's RF address within the hub.
	/// </summary>
	public int Address { get; } = address;
	/// <summary>
	/// Gets the assigned timer name, with an address-based fallback.
	/// </summary>
	public string Name { get; } = string.IsNullOrWhiteSpace (name) ? "RainPoint timer " + address : name;
	/// <summary>
	/// Gets the stable Crestron child identity derived from hub ID and RF address.
	/// </summary>
	public string ControllerId => "rainpoint_" + HubId.ToString (CultureInfo.InvariantCulture) + "_" + Address.ToString (CultureInfo.InvariantCulture);
	}

/// <summary>
/// Captures one zone's reported activity, usage, timing and alarm state without inferring command success.
/// </summary>
/// <param name="zone">The one-based zone number on the selected timer.</param>
/// <param name="active">Reported zone activity, or null when unknown.</param>
/// <param name="mode">The decoded watering or soaking mode.</param>
/// <param name="usageLitres">Reported usage in litres, or null when unknown.</param>
/// <param name="duration">The reported configured duration, or null when unknown.</param>
/// <param name="alarms">The decoded alarm description.</param>
/// <param name="reportedEnd">The resolved watering or phase-end instant, or null when unavailable.</param>
/// <param name="alarmCode">The reported alarm code, zero for clear, or null when unknown.</param>
public sealed class ZoneReading (int zone, bool? active, string mode, decimal? usageLitres, TimeSpan? duration, string alarms, DateTimeOffset? reportedEnd = null, int? alarmCode = null)
	{
	/// <summary>
	/// Gets the reported alarm code, or null when unavailable; zero means no reported alarm.
	/// </summary>
	public int? AlarmCode { get; } = alarmCode;
	/// <summary>
	/// Gets the one-based zone number.
	/// </summary>
	public int Zone { get; } = zone;
	/// <summary>
	/// Gets reported activity, including an active cycle's soaking phase, or null when unknown.
	/// </summary>
	public bool? Active { get; } = active;
	/// <summary>
	/// Gets the decoded watering or soaking mode description.
	/// </summary>
	public string Mode { get; } = mode;
	/// <summary>
	/// Gets status-reported water usage in litres, or null when unavailable.
	/// </summary>
	public decimal? UsageLitres { get; } = usageLitres;
	/// <summary>
	/// Gets the reported configured duration, or null when unavailable.
	/// </summary>
	public TimeSpan? Duration { get; } = duration;
	/// <summary>
	/// Gets the decoded alarm description.
	/// </summary>
	public string Alarms { get; } = alarms;
	/// <summary>
	/// Gets the resolved end instant for the reported watering operation or cycle phase, or null when ambiguous.
	/// </summary>
	public DateTimeOffset? ReportedEnd { get; } = reportedEnd;
	/// <summary>
	/// Estimates remaining seconds only from fresh, plausible reported timing; does not control the valve.
	/// </summary>
	/// <param name="now">The current instant used to evaluate time and freshness.</param>
	/// <param name="fresh">Whether the underlying device feedback is considered fresh.</param>
	/// <returns>Nonnegative remaining seconds, zero when idle, or null for unknown, stale or implausible timing.</returns>
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
	/// <summary>
	/// Formats a fresh countdown, phase countdown or explicit unavailable/awaiting-feedback message.
	/// </summary>
	/// <param name="now">The current instant used to evaluate time and freshness.</param>
	/// <param name="fresh">Whether the underlying device feedback is considered fresh.</param>
	/// <returns>A countdown or a message explaining why a countdown cannot be shown.</returns>
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
	/// <summary>
	/// Gets the activity mode, Idle or Unknown according to reported activity.
	/// </summary>
	public string Status => Active == true ? Mode : Active == false ? "Idle" : "Unknown";
	/// <summary>
	/// Gets a litres label or an explicit unknown-usage message.
	/// </summary>
	public string Usage => UsageLitres.HasValue ? UsageLitres.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " L last usage" : "Last usage unknown";
	/// <summary>
	/// Gets the configured-duration label in minutes, or an unknown-duration message.
	/// </summary>
	public string DurationDisplay => Duration.HasValue ? Duration.Value.TotalMinutes.ToString ("0.#", CultureInfo.InvariantCulture) + " min configured" : "Configured duration unknown";
	}

/// <summary>
/// Captures timer readings and cloud-contact freshness for status presentation.
/// </summary>
/// <param name="timer">The commissioned timer identity.</param>
/// <param name="revision">The observation revision used to order updates.</param>
/// <param name="hubOnline">Cloud-reported hub connectivity, or null when unknown.</param>
/// <param name="reportTime">The timer data-change timestamp, or null when unavailable.</param>
/// <param name="zones">The zone observations to copy into this snapshot.</param>
/// <param name="battery">The battery display description.</param>
/// <param name="signal">The RF signal display description.</param>
/// <param name="contactTime">The last successful read or accepted push time; defaults to the report time.</param>
/// <param name="batteryLow">The low-battery flag, or null when unknown.</param>
public sealed class TimerReading (TimerIdentity timer, long revision, bool? hubOnline, DateTimeOffset? reportTime,
	IReadOnlyList<ZoneReading> zones, string battery, string signal, DateTimeOffset? contactTime = null, bool? batteryLow = null)
	{
	/// <summary>
	/// Gets the reported low-battery flag, or null when unavailable.
	/// </summary>
	public bool? BatteryLow { get; } = batteryLow;
	/// <summary>
	/// Gets the commissioned timer identity for this reading.
	/// </summary>
	public TimerIdentity Timer { get; } = timer;
	/// <summary>
	/// Gets the observation revision used to order feedback.
	/// </summary>
	public long Revision { get; } = revision;
	/// <summary>
	/// Gets cloud-reported hub connectivity, or null when unknown.
	/// </summary>
	public bool? HubOnline { get; } = hubOnline;
	/// <summary>
	/// Gets the timer data-change timestamp, or null when unavailable.
	/// </summary>
	public DateTimeOffset? ReportTime { get; } = reportTime;
	/// <summary>Last successful cloud read or accepted timer push; distinct from the timer's data-change time.</summary>
	public DateTimeOffset? ContactTime { get; } = contactTime ?? reportTime;
	/// <summary>
	/// Gets a read-only copy of the reported zone observations.
	/// </summary>
	public IReadOnlyList<ZoneReading> Zones { get; } = Array.AsReadOnly (zones.ToArray ());
	/// <summary>
	/// Gets the battery description used by the UI.
	/// </summary>
	public string Battery { get; } = battery;
	/// <summary>
	/// Gets the RF signal description used by the UI.
	/// </summary>
	public string Signal { get; } = signal;
	/// <summary>
	/// Checks hub connectivity, report presence and contact time; an active push connection relaxes the contact-age limit.
	/// </summary>
	/// <param name="now">The current instant used to evaluate time and freshness.</param>
	/// <param name="liveUpdates">Whether an active push observer maintains freshness.</param>
	/// <returns>True when the observation satisfies the current cloud-contact freshness policy.</returns>
	public bool IsFresh (DateTimeOffset now, bool liveUpdates = false) => HubOnline == true && ReportTime.HasValue
		&& ContactTime <= now.AddSeconds (30) && (liveUpdates || now - ContactTime <= TimeSpan.FromSeconds (90));
	/// <summary>
	/// Summarizes reported active zones, incomplete feedback or hub unavailability for the room tile.
	/// </summary>
	/// <param name="now">The current instant used to evaluate time and freshness.</param>
	/// <param name="liveUpdates">Whether an active push observer maintains freshness.</param>
	/// <returns>The room-tile status text.</returns>
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
		bool complete = Enumerable.Range (1, Timer.ZoneCount).All (zone => Zones.Count (z => z.Zone == zone && z.Active.HasValue) == 1);
		return active.Length > 0 ? running + (complete ? "" : " / others unknown") : complete ? "All zones idle" : "Status unknown";
		}
	}

/// <summary>
/// Selects an unambiguous cloud resource, optionally constrained by its identifier.
/// </summary>
public static class Selection
	{
	/// <summary>
	/// Selects exactly one item, filtering by the requested ID when supplied.
	/// </summary>
	/// <param name="items">The candidate resources.</param>
	/// <param name="id">Selects the numeric identifier of each candidate.</param>
	/// <param name="selected">The requested resource ID, or null to require a single candidate.</param>
	/// <param name="label">The resource kind used in ambiguity errors.</param>
	/// <typeparam name="T">The resource type being selected.</typeparam>
	/// <returns>The single matching item.</returns>
	/// <exception cref="System.InvalidOperationException">The supplied arguments do not satisfy the operation requirements.</exception>
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