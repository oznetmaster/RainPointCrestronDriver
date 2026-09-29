// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using RainPointClient;

namespace RainPoint.CrestronDriver.Core;

public sealed class RecordedUsage (string id, DateTimeOffset cloudTimestamp, decimal litres, DateTime? localTime = null, string timeZone = null)
	{
	public string Id { get; } = id;
	public DateTimeOffset CloudTimestamp { get; } = cloudTimestamp;
	public decimal Litres { get; } = litres;
	public DateTime? LocalTime { get; } = localTime;
	public string TimeZone { get; } = timeZone;
	public string VolumeLabel => Litres.ToString ("0.#", CultureInfo.InvariantCulture) + " L";
	public string TimeLabel => LocalTime.HasValue
		? LocalTime.Value.ToString ("dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " (" + (string.IsNullOrWhiteSpace (TimeZone) ? "home time" : TimeZone) + ")"
		: CloudTimestamp.ToUniversalTime ().ToString ("dd MMM yyyy HH:mm:ss 'UTC (cloud record)'", CultureInfo.InvariantCulture);
	internal static RecordedUsage Latest (IEnumerable<RainPointEvent> records, long hub, int address, int zone)
		{
		var row = records.Where (r => r.HubId == hub && r.Address == address && r.Zone == zone
			&& r.Kind is RainPointEventKind.Watering or RainPointEventKind.WaterUsage && r.WaterUsedLitres >= 0)
			.OrderByDescending (r => r.CloudTimestamp).ThenByDescending (r => r.Id, StringComparer.Ordinal).FirstOrDefault ();
		return row == null ? null : new RecordedUsage (row.Id, row.CloudTimestamp, row.WaterUsedLitres.Value, row.ReportedLocalTime, row.ReportedTimeZone);
		}
	}