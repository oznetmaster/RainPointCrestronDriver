// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

namespace RainPoint.CrestronDriver.Core;

public sealed class ZoneControl
	{
	private ZoneControl (string label, bool enabled, bool stop)
		{
		Label = label;
		Enabled = enabled;
		Stop = stop;
		}
	public string Label
		{
		get;
		}
	public bool Enabled
		{
		get;
		}
	public bool Stop
		{
		get;
		}
	public static ZoneControl For (bool? active, bool fresh, bool available, bool pending, bool pendingStart)
		{
		if (pending && !pendingStart)
			return new ZoneControl ("Stopping...", false, true);
		if (active == true || pending && pendingStart)
			return new ZoneControl ("Stop this zone", available, true);
		return new ZoneControl ("Start timed watering", available && fresh && active == false && !pending, false);
		}
	}