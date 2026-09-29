// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

namespace RainPoint.CrestronDriver.Core;

/// <summary>
/// Describes the context-sensitive start or stop button for one zone.
/// </summary>
public sealed class ZoneControl
	{
	private ZoneControl (string label, bool enabled, bool stop)
		{
		Label = label;
		Enabled = enabled;
		Stop = stop;
		}
	/// <summary>
	/// Gets the action label appropriate to reported activity and pending commands.
	/// </summary>
	public string Label
		{
		get;
		}
	/// <summary>
	/// Gets whether the current start or stop action is available.
	/// </summary>
	public bool Enabled
		{
		get;
		}
	/// <summary>
	/// Gets whether activating the control should request a stop rather than a start.
	/// </summary>
	public bool Stop
		{
		get;
		}
	/// <summary>
	/// Chooses the start/stop presentation from freshness, connection availability and pending command state.
	/// </summary>
	/// <param name="active">Reported zone activity, or null when unknown.</param>
	/// <param name="fresh">Whether the underlying device feedback is considered fresh.</param>
	/// <param name="available">Whether commands can currently be submitted.</param>
	/// <param name="pending">Whether a command is awaiting device feedback.</param>
	/// <param name="pendingStart">Whether the pending command requested a start.</param>
	/// <returns>The selected action label, enabled state and start/stop intent.</returns>
	public static ZoneControl For (bool? active, bool fresh, bool available, bool pending, bool pendingStart)
		{
		if (pending && !pendingStart)
			return new ZoneControl ("Stopping...", false, true);
		if (active == true || pending && pendingStart)
			return new ZoneControl ("Stop this zone", available, true);
		return new ZoneControl ("Start timed watering", available && fresh && active == false && !pending, false);
		}
	}