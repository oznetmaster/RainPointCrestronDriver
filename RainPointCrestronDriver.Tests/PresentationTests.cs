// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Linq;

using NUnit.Framework;

using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Tests;

[TestFixture]
public sealed class PresentationTests
	{
	[TestCase (0, "All zones idle"), TestCase (1, "Zone 1 active"), TestCase (2, "Zone 2 active"), TestCase (3, "Zone 3 active"), TestCase (4, "Zones 1, 2, 3 active")]
	public void TileNamesRunningZones (int active, string expected)
		{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var zones = Enumerable.Range (1, 3).Select (z => new ZoneReading (z, active == z || active == 4, "Watering", 0, null, ""));
		var frame = new TimerReading (new TimerIdentity (1, 2, "Garden"), 1, true, now, zones.ToArray (), "", "");
		Assert.That (frame.Tile (now), Is.EqualTo (expected));
		}
	[Test]
	public void PartialReportNeverImpliesAllIdle ()
		{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var frame = new TimerReading (new TimerIdentity (1, 2, "Garden"), 1, true, now,
			new[] { new ZoneReading (1, false, "", null, null, "") }, "", "");
		Assert.That (frame.Tile (now), Is.EqualTo ("Status unknown"));
		}
	[Test]
	public void StaleFeedbackNeverClaimsCurrentlyRunning ()
		{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var frame = new TimerReading (new TimerIdentity (1, 2, "Garden"), 1, true, now.AddMinutes (-10),
			new[] { new ZoneReading (1, true, "Watering", null, null, "") }, "", "");
		Assert.That (frame.Tile (now), Is.EqualTo ("Status unavailable"));
		}
	[Test]
	public void FreshReadRetainsDataChangeTimeOfIdleTimer ()
		{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var frame = new TimerReading (new TimerIdentity (1, 2, "Garden"), 1, true, now.AddDays (-1),
			Enumerable.Range (1, 3).Select (z => new ZoneReading (z, false, "", null, null, "")).ToArray (), "", "", now);
		Assert.That (frame.IsFresh (now), Is.True);
		Assert.That (frame.ReportTime, Is.EqualTo (now.AddDays (-1)));
		}
	[Test]
	public void UsageAndConfiguredDurationAreNotFlowOrCountdown ()
		{
		var zone = new ZoneReading (1, true, "Watering", 1.4m, TimeSpan.FromMinutes (5), "");
		Assert.That (zone.Usage, Is.EqualTo ("1.4 L last usage"));
		Assert.That (zone.DurationDisplay, Is.EqualTo ("5 min configured"));
		}
	[Test]
	public void MissingUsageIsNotZero () => Assert.That (new ZoneReading (1, null, "", null, null, "").Usage, Is.EqualTo ("Last usage unknown"));
	[Test]
	public void IdentityDoesNotDependOnLabel () => Assert.That (new TimerIdentity (1, 2, "Renamed").ControllerId, Is.EqualTo (new TimerIdentity (1, 2, "Old").ControllerId));
	[Test]
	public void AmbiguousDiscoveryRequiresSelection () => Assert.Throws<InvalidOperationException> (() => Selection.Unique (new[] { 1L, 2L }, n => n, null, "home"));
	[Test]
	public void ExplicitSelectionIsExact () => Assert.That (Selection.Unique (new[] { 1L, 2L }, n => n, 2, "home"), Is.EqualTo (2));
	[Test]
	public void MissingSelectionNeverFallsBackToFirst () => Assert.Throws<InvalidOperationException> (() => Selection.Unique (new[] { 1L, 2L }, n => n, 3, "hub"));
	[TestCase (""), TestCase ("+44"), TestCase ("UK")]
	public void CallingCodeValidated (string area) => Assert.Throws<ArgumentException> (() => new DriverSettings ("test@example.invalid", "test", area));
	}