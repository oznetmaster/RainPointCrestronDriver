// Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
using System;
using NUnit.Framework;
using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Lifecycle.Tests;

public sealed partial class ExtensionTests
	{
	[TestCase (30, true), TestCase (91, false)]
	public void QuietPushRenewalUsesSuccessfulContactWithoutExtendingStaleFeedback (int contactAgeSeconds, bool expectedFresh)
		{
		TimerReading frame = Frame (1);
		_timer.SetConnection ("PushConnected");
		_timer.Update (new TimerReading (frame.Timer, frame.Revision, true, DateTimeOffset.UtcNow.AddHours (-2),
			frame.Zones, frame.Battery, frame.Signal, DateTimeOffset.UtcNow.AddSeconds (-contactAgeSeconds)));
		Assert.That (_timer.IsOnline && _timer.IsReady, Is.True, "A quiet live observer may retain an old data-change timestamp.");
		_timer.SetConnection ("Reconnecting");
		Assert.That (_timer.IsOnline, Is.EqualTo (expectedFresh));
		Assert.That (_timer.IsReady, Is.EqualTo (expectedFresh));
		if (!expectedFresh)
			Assert.That (_timer.Zone1CanStart || _timer.Zone2CanStart || _timer.Zone3CanStart, Is.False);
		_timer.SetConnection ("AuthenticationRequired");
		Assert.That (_timer.IsOnline || _timer.IsReady || _timer.Zone1CanStart, Is.False,
			"Recent contact cannot override expired authentication.");
		}
	}
