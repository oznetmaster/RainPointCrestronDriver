// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using NUnit.Framework;

using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Tests;

[TestFixture]
public sealed class ZoneControlTests
	{
	[TestCase (false, true, true, false, false, "Start timed watering", true, false)]
	[TestCase (true, true, true, false, false, "Stop this zone", true, true)]
	[TestCase (true, false, true, false, false, "Stop this zone", true, true)]
	[TestCase (false, false, true, false, false, "Start timed watering", false, false)]
	[TestCase (null, true, true, false, false, "Start timed watering", false, false)]
	[TestCase (false, true, true, true, true, "Stop this zone", true, true)]
	[TestCase (true, true, true, true, false, "Stopping...", false, true)]
	[TestCase (true, true, false, false, false, "Stop this zone", false, true)]
	public void ActionFollowsReportedStateAndPendingCommand (bool? active, bool fresh, bool available, bool pending, bool start, string label, bool enabled, bool stop)
		{
		ZoneControl control = ZoneControl.For (active, fresh, available, pending, start);
		Assert.That (control.Label, Is.EqualTo (label));
		Assert.That (control.Enabled, Is.EqualTo (enabled));
		Assert.That (control.Stop, Is.EqualTo (stop));
		}
	}