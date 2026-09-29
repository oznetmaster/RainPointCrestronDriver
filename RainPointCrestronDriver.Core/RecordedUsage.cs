// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using RainPointClient;

namespace RainPoint.CrestronDriver.Core;

/// <summary>
/// Keeps the volume and timestamp of one completed cloud usage record together.
/// </summary>
/// <param name="id">The cloud event identifier.</param>
/// <param name="cloudTimestamp">The cloud record timestamp used for ordering.</param>
/// <param name="litres">The recorded volume in litres.</param>
/// <param name="localTime">The device-reported local timestamp, or null when absent.</param>
/// <param name="timeZone">The reported time-zone label, or null when absent.</param>
public sealed class RecordedUsage (string id, DateTimeOffset cloudTimestamp, decimal litres, DateTime? localTime = null, string timeZone = null)
	{
	/// <summary>
	/// Gets the cloud event identifier used to identify this usage record.
	/// </summary>
	public string Id { get; } = id;
	/// <summary>
	/// Gets the cloud record timestamp used for ordering, not necessarily the local watering time.
	/// </summary>
	public DateTimeOffset CloudTimestamp { get; } = cloudTimestamp;
	/// <summary>
	/// Gets the recorded water volume in litres.
	/// </summary>
	public decimal Litres { get; } = litres;
	/// <summary>
	/// Gets the device-reported local time, or null when absent.
	/// </summary>
	public DateTime? LocalTime { get; } = localTime;
	/// <summary>
	/// Gets the reported time-zone label, or null when absent.
	/// </summary>
	public string TimeZone { get; } = timeZone;
	/// <summary>
	/// Gets the recorded volume formatted in litres.
	/// </summary>
	public string VolumeLabel => Litres.ToString ("0.#", CultureInfo.InvariantCulture) + " L";
	/// <summary>
	/// Gets the reported local timestamp and zone, falling back to a clearly labelled UTC cloud-record time.
	/// </summary>
	public string TimeLabel => LocalTime.HasValue
		? LocalTime.Value.ToString ("dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " (" + (string.IsNullOrWhiteSpace (TimeZone) ? "home time" : TimeZone) + ")"
		: CloudTimestamp.ToUniversalTime ().ToString ("dd MMM yyyy HH:mm:ss 'UTC (cloud record)'", CultureInfo.InvariantCulture);
	/// <summary>
	/// Selects the newest nonnegative watering or usage event matching the hub, RF address and zone.
	/// </summary>
	/// <param name="records">Cloud history events from which to select a usage record.</param>
	/// <param name="hub">The cloud hub ID to match.</param>
	/// <param name="address">The timer RF address within the configured hub.</param>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <returns>The newest matching usage record, or null when no valid record exists.</returns>
	internal static RecordedUsage Latest (IEnumerable<RainPointEvent> records, long hub, int address, int zone)
		{
		var row = records.Where (r => r.HubId == hub && r.Address == address && r.Zone == zone
			&& r.Kind is RainPointEventKind.Watering or RainPointEventKind.WaterUsage && r.WaterUsedLitres >= 0)
			.OrderByDescending (r => r.CloudTimestamp).ThenByDescending (r => r.Id, StringComparer.Ordinal).FirstOrDefault ();
		return row == null ? null : new RecordedUsage (row.Id, row.CloudTimestamp, row.WaterUsedLitres.Value, row.ReportedLocalTime, row.ReportedTimeZone);
		}
	}