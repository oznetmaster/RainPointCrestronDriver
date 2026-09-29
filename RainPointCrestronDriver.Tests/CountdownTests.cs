// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;

using NUnit.Framework;

using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Tests;

[TestFixture]
public sealed class CountdownTests
	{
	private static readonly DateTimeOffset Now = new (2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	[TestCase (225, true, true, 225)]
	[TestCase (0, true, true, 0)]
	[TestCase (-5, true, true, 0)]
	[TestCase (225, false, true, 0)]
	[TestCase (225, true, false, null)]
	[TestCase (3600, true, true, null)]
	[TestCase (225, null, true, null)]
	public void NumericCountdownPreservesUnknownAndDoesNotInferStop (int seconds, bool? active, bool fresh, int? expected)
		{
		var zone = new ZoneReading (1, active, "Watering", null, TimeSpan.FromMinutes (5), "", Now.AddSeconds (seconds));
		Assert.That (zone.RemainingSeconds (Now, fresh), Is.EqualTo (expected));
		Assert.That (zone.Active, Is.EqualTo (active));
		}
	[TestCase (225, true, true, "Time left: 03:45")]
	[TestCase (0, true, true, "Awaiting stop confirmation")]
	[TestCase (-5, true, true, "Awaiting stop confirmation")]
	[TestCase (225, false, true, "Not running")]
	[TestCase (225, true, false, "Time left unavailable")]
	[TestCase (3600, true, true, "Time left unavailable")]
	public void DisplayUsesReportedEndAndNeverInfersIdle (int seconds, bool active, bool fresh, string expected)
		{
		var zone = new ZoneReading (1, active, "Watering", null, TimeSpan.FromMinutes (5), "", Now.AddSeconds (seconds));
		Assert.That (zone.TimeLeft (Now, fresh), Is.EqualTo (expected));
		Assert.That (zone.Active, Is.EqualTo (active));
		}
	[Test]
	public void MissingEndTimeCannotUseConfiguredDurationAsCountdown ()
		{
		var zone = new ZoneReading (1, true, "Watering", null, TimeSpan.FromMinutes (5), "");
		Assert.That (zone.TimeLeft (Now, true), Is.EqualTo ("Time left unavailable"));
		}
	[TestCase ("Cycle watering"), TestCase ("Soaking (cycle active)")]
	public void CyclicEndTimeDescribesPhase (string mode)
		{
		var zone = new ZoneReading (3, true, mode, null, TimeSpan.FromMinutes (5), "", Now.AddSeconds (30));
		Assert.That (zone.TimeLeft (Now, true), Is.EqualTo ("Phase time left: 00:30"));
		Assert.That (zone.TimeLeft (Now.AddSeconds (30), true), Is.EqualTo ("Awaiting phase update"));
		}
	[Test]
	public void FixedOffsetUsesHomeRatherThanProcessorTimezone () => Assert.That (
		ReportedEndTime.Resolve (new DateTime (2026, 9, 28, 15, 0, 0), TimeSpan.FromHours (3), TimeSpan.Zero, Array.Empty<DateTimeOffset> ()), Is.EqualTo (Now));
	private static DateTimeOffset[] Transitions => new[]
		{
		new DateTimeOffset (2026, 3, 29, 1, 0, 0, TimeSpan.Zero), new DateTimeOffset (2026, 10, 25, 1, 0, 0, TimeSpan.Zero),
		new DateTimeOffset (2027, 3, 28, 1, 0, 0, TimeSpan.Zero), new DateTimeOffset (2027, 10, 31, 1, 0, 0, TimeSpan.Zero)
		};
	[Test]
	public void DaylightOffsetResolvesReportedSummerTime () => Assert.That (
		ReportedEndTime.Resolve (new DateTime (2026, 9, 28, 13, 0, 0), TimeSpan.Zero, TimeSpan.FromHours (1), Transitions), Is.EqualTo (Now));
	[TestCase (2026, 10, 25, 1, 30)]
	[TestCase (2027, 3, 28, 1, 30)]
	[TestCase (2030, 9, 28, 13, 0)]
	public void AmbiguousMissingOrUncoveredClockTimesRemainUnknown (int year, int month, int day, int hour, int minute) => Assert.That (
		ReportedEndTime.Resolve (new DateTime (year, month, day, hour, minute, 0), TimeSpan.Zero, TimeSpan.FromHours (1), Transitions), Is.Null);
	}