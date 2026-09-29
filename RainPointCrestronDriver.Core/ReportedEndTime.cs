// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;

namespace RainPoint.CrestronDriver.Core;

/// <summary>Resolves the timer's local end time using the cloud home's reported offset table.</summary>
public static class ReportedEndTime
	{
	public static DateTimeOffset? Resolve (DateTime? local, TimeSpan baseOffset, TimeSpan daylightAdjustment, IReadOnlyList<DateTimeOffset> transitions)
		{
		if (!local.HasValue || transitions == null)
			return null;
		DateTimeOffset? result = null;
		foreach (TimeSpan offset in daylightAdjustment == TimeSpan.Zero ? new[] { baseOffset } : new[] { baseOffset, baseOffset + daylightAdjustment })
			{
			if (offset < TimeSpan.FromHours (-14) || offset > TimeSpan.FromHours (14))
				return null;
			DateTimeOffset candidate;
			try
				{
				candidate = new DateTimeOffset (DateTime.SpecifyKind (local.Value, DateTimeKind.Unspecified), offset);
				}
			catch (ArgumentException) { return null; }
			if (transitions.Count > 0 && (candidate < transitions[0] || candidate >= transitions[transitions.Count - 1]))
				continue;
			int index = 0;
			while (index < transitions.Count && candidate >= transitions[index])
				index++;
			TimeSpan expected = baseOffset + (index % 2 == 1 ? daylightAdjustment : TimeSpan.Zero);
			if (expected != offset)
				continue;
			if (result.HasValue)
				return null; // An ambiguous clock-change time cannot support a reliable countdown.
			result = candidate.ToUniversalTime ();
			}
		return result;
		}
	}