// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.EntityModel.Logging;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

using RainPoint.CrestronDriver.Core;

namespace RainPoint.CrestronDriver;

internal sealed partial class RainPointTimerEntity : ReflectedAttributeDriverEntity
	{
	private readonly object _sync = new ();
	private readonly int _zoneCount;
	private readonly IrrigationController _controller;
	private readonly Timer _ageTimer;
	private TimerReading _reading;
	private readonly RecordedUsage[] _usage = new RecordedUsage[3];
	private bool _available;
	private bool _liveUpdates;
	private bool _disposed;
	private readonly bool[] _pending = new bool[3];
	private readonly bool[] _pendingStart = new bool[3];
	internal int Address
		{
		get;
		}
	[EntityProperty (Id = "tileIcon")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string TileIcon
		{
		get;
		private set
			{
			if (field != value)
				{
				field = value;
				NotifyPropertyChanged ("tileIcon", new DriverEntityValue (value));
				}
			}
		} = "icSprinklersOffDisabled";
	internal RainPointTimerEntity (TimerIdentity identity, IrrigationController controller,
		DriverControllerCreationArgs args, DriverImplementationResources resources) : base (identity.ControllerId)
		{
		Address = identity.Address;
		_zoneCount = identity.ZoneCount;
		_controller = controller;
		UpdateIdentity (identity);
		var state = GetState ();
		var definition = state.Definition;
		bool AbsentZone (string id) => Enumerable.Range (_zoneCount + 1, 3 - _zoneCount)
			.Any (zone => id.IndexOf ("zone" + zone, StringComparison.OrdinalIgnoreCase) >= 0);
		foreach (string id in definition.Properties.Keys.Where (AbsentZone).ToArray ())
			{
			RemoveProperty (id);
			// Hidden UI controls still need valid bindings, but no programmable property.
			if (definition.PropertyMetadata.TryGetValue (id, out var metadata) && metadata.ExtensionUiProperty)
				AddProperty (this, id, new CachedPropertyInstance (definition.Properties[id], new DriverEntityPropertyMetadata (false, true))
					{ Value = state.PropertyValues[id] });
			}
		foreach (string id in definition.Commands.Keys.Where (AbsentZone).ToArray ())
			if (!id.StartsWith ("operateZone", StringComparison.Ordinal) && !id.StartsWith ("setZone", StringComparison.Ordinal))
				RemoveCommand (id);
		foreach (string id in definition.Events.Keys.Where (AbsentZone).ToArray ())
			RemoveEvent (id);
		var ui = UiDefinitionProperty.LoadFromDirectoryIfExists (args.DriverDataDirectoryPath, resources.InitLogger, LogEntryLevel.Error);
		if (ui != null)
			{
			AddProperty (this, UiDefinitionProperty.Name, ui);
			}
		AddCommand (this, ExtensionDoCommandExecutor.CommandName, new ExtensionDoCommandExecutor (GetCommand, resources.Logger));
		AddCommand (this, ExtensionSetPropertyValueExecutor.CommandName, new ExtensionSetPropertyValueExecutor (GetCommand, resources.Logger));
		_ageTimer = new Timer (_ =>
			{
				_controller.CheckFeedbackTimeouts ();
				Render ();
			}, null, TimeSpan.FromSeconds (1), TimeSpan.FromSeconds (1));
		}

	[EntityProperty (Id = "hasZone2")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool HasZone2 => _zoneCount >= 2;

	[EntityProperty (Id = "hasZone3")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool HasZone3 => _zoneCount >= 3;

	[EntityProperty (Id = "hubLabel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string HubLabel
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("hubLabel", new DriverEntityValue (value));
			}
		} = "RainPoint hub";

	[EntityProperty (Id = "zone1Name")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1Name
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1Name", new DriverEntityValue (value));
			}
		} = "Zone 1";

	[EntityProperty (Id = "zone2Name")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2Name
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2Name", new DriverEntityValue (value));
			}
		} = "Zone 2";

	[EntityProperty (Id = "zone3Name")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3Name
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3Name", new DriverEntityValue (value));
			}
		} = "Zone 3";

	internal void UpdateIdentity (TimerIdentity identity)
		{
		lock (_sync)
			{
			if (_disposed || identity.Address != Address || identity.ControllerId != ControllerId)
				return;
			DeviceLabel = identity.Name;
			DeviceModel = identity.Model;
			HubLabel = string.IsNullOrWhiteSpace (identity.HubName) ? "RainPoint hub" : identity.HubName;
			Zone1Name = identity.ZoneName (1);
			if (_zoneCount >= 2)
				Zone2Name = identity.ZoneName (2);
			if (_zoneCount >= 3)
				Zone3Name = identity.ZoneName (3);
			}
		}

	[EntityProperty (Id = "deviceModel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string DeviceModel { get; private set; } = "HTV345FRF";

	[EntityProperty (Id = "deviceLabel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string DeviceLabel
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("deviceLabel", new DriverEntityValue (value));
			}
		} = "RainPoint";

	[EntityProperty (Id = "tileStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string TileStatus
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("tileStatus", new DriverEntityValue (value));
			}
		} = "Waiting for feedback";

	[EntityProperty (Id = "connection", FriendlyName = "Connection state", NameLocalizationKey = "Programming_connection")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Connection
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("connection", new DriverEntityValue (value));
			}
		} = "Connecting";

	[EntityProperty (Id = "battery", FriendlyName = "Battery status", NameLocalizationKey = "Programming_battery")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Battery
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("battery", new DriverEntityValue (value));
			}
		} = "Battery unknown";

	[EntityProperty (Id = "signal")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Signal
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("signal", new DriverEntityValue (value));
			}
		} = "RF unknown";

	[EntityProperty (Id = "reportTime")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ReportTime
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("reportTime", new DriverEntityValue (value));
			}
		} = "Report time unknown";

	[EntityProperty (Id = "onlineIndicator:isOnline", FriendlyName = "Status available", NameLocalizationKey = "Programming_onlineIndicator_isOnline")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public bool IsOnline
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("onlineIndicator:isOnline", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "readyIndicator:isReady")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool IsReady
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("readyIndicator:isReady", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "canStop")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool CanStop
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("canStop", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "zone1Status")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1Status
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Status", new DriverEntityValue (value));
			}
		} = "Unknown";

	[EntityProperty (Id = "zone1Usage")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1Usage
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Usage", new DriverEntityValue (value));
			}
		} = "Last usage unknown";

	[EntityProperty (Id = "zone1Details")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1Details
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Details", new DriverEntityValue (value));
			}
		} = "Configured duration unknown";

	[EntityProperty (Id = "zone1Alarms", FriendlyName = "Zone 1: Reported alarms", NameLocalizationKey = "Programming_zone1Alarms")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Zone1Alarms
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Alarms", new DriverEntityValue (value));
			}
		} = "Alarms unknown";

	[EntityProperty (Id = "zone1Command", FriendlyName = "Zone 1: Command feedback", NameLocalizationKey = "Programming_zone1Command")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Zone1Command
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Command", new DriverEntityValue (value));
			}
		} = "No command sent";

	[EntityProperty (Id = "zone1Plans")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1Plans
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Plans", new DriverEntityValue (value));
			}
		} = "Refresh plans to load";

	[EntityProperty (Id = "zone1CanStart")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool Zone1CanStart
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1CanStart", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "zone1Minutes", RangeMinimum = 1, RangeMaximum = 120, RangeStepSize = 1, FriendlyName = "Zone 1: Selected duration (minutes)", NameLocalizationKey = "Programming_zone1Minutes")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public int Zone1Minutes
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone1Minutes", new DriverEntityValue (value));
			}
		} = 5;

	[EntityProperty (Id = "zone2Status")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2Status
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Status", new DriverEntityValue (value));
			}
		} = "Unknown";

	[EntityProperty (Id = "zone2Usage")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2Usage
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Usage", new DriverEntityValue (value));
			}
		} = "Last usage unknown";

	[EntityProperty (Id = "zone2Details")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2Details
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Details", new DriverEntityValue (value));
			}
		} = "Configured duration unknown";

	[EntityProperty (Id = "zone2Alarms", FriendlyName = "Zone 2: Reported alarms", NameLocalizationKey = "Programming_zone2Alarms")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Zone2Alarms
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Alarms", new DriverEntityValue (value));
			}
		} = "Alarms unknown";

	[EntityProperty (Id = "zone2Command", FriendlyName = "Zone 2: Command feedback", NameLocalizationKey = "Programming_zone2Command")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Zone2Command
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Command", new DriverEntityValue (value));
			}
		} = "No command sent";

	[EntityProperty (Id = "zone2Plans")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2Plans
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Plans", new DriverEntityValue (value));
			}
		} = "Refresh plans to load";

	[EntityProperty (Id = "zone2CanStart")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool Zone2CanStart
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2CanStart", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "zone2Minutes", RangeMinimum = 1, RangeMaximum = 120, RangeStepSize = 1, FriendlyName = "Zone 2: Selected duration (minutes)", NameLocalizationKey = "Programming_zone2Minutes")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public int Zone2Minutes
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone2Minutes", new DriverEntityValue (value));
			}
		} = 5;

	[EntityProperty (Id = "zone3Status")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3Status
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Status", new DriverEntityValue (value));
			}
		} = "Unknown";

	[EntityProperty (Id = "zone3Usage")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3Usage
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Usage", new DriverEntityValue (value));
			}
		} = "Last usage unknown";

	[EntityProperty (Id = "zone3Details")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3Details
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Details", new DriverEntityValue (value));
			}
		} = "Configured duration unknown";

	[EntityProperty (Id = "zone3Alarms", FriendlyName = "Zone 3: Reported alarms", NameLocalizationKey = "Programming_zone3Alarms")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Zone3Alarms
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Alarms", new DriverEntityValue (value));
			}
		} = "Alarms unknown";

	[EntityProperty (Id = "zone3Command", FriendlyName = "Zone 3: Command feedback", NameLocalizationKey = "Programming_zone3Command")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public string Zone3Command
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Command", new DriverEntityValue (value));
			}
		} = "No command sent";

	[EntityProperty (Id = "zone3Plans")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3Plans
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Plans", new DriverEntityValue (value));
			}
		} = "Refresh plans to load";

	[EntityProperty (Id = "zone3CanStart")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool Zone3CanStart
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3CanStart", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "zone3Minutes", RangeMinimum = 1, RangeMaximum = 120, RangeStepSize = 1, FriendlyName = "Zone 3: Selected duration (minutes)", NameLocalizationKey = "Programming_zone3Minutes")]
	[EntityPropertyMetadata (Programmable = true, ExtensionUiProperty = true)]
	public int Zone3Minutes
		{
		get;
		private set
			{
			if (field == value)
				{
				return;
				}
			field = value;
			NotifyPropertyChanged ("zone3Minutes", new DriverEntityValue (value));
			}
		} = 5;

	[EntityProperty (Id = "zone1ActionLabel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1ActionLabel
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1ActionLabel", new DriverEntityValue (value));
			}
		} = "Start timed watering";
	[EntityProperty (Id = "zone1CanControl")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool Zone1CanControl
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1CanControl", new DriverEntityValue (value));
			}
		} = false;
	[EntityProperty (Id = "zone2ActionLabel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2ActionLabel
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2ActionLabel", new DriverEntityValue (value));
			}
		} = "Start timed watering";
	[EntityProperty (Id = "zone2CanControl")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool Zone2CanControl
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2CanControl", new DriverEntityValue (value));
			}
		} = false;
	[EntityProperty (Id = "zone3ActionLabel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3ActionLabel
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3ActionLabel", new DriverEntityValue (value));
			}
		} = "Start timed watering";
	[EntityProperty (Id = "zone3CanControl")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public bool Zone3CanControl
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3CanControl", new DriverEntityValue (value));
			}
		} = false;

	[EntityProperty (Id = "minutesFormat")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string MinutesFormat => "%s min";

	internal void SetConnection (string state)
		{
		lock (_sync)
			{
			Connection = state;
			_liveUpdates = state == "PushConnected";
			if (state == "Connecting")
				{
				_reading = null;
				Array.Clear (_pending, 0, _pending.Length);
				Array.Clear (_pendingStart, 0, _pendingStart.Length);
				}
			if (state != "Reconnecting")
				{
				_available = state == "PushConnected" || state == "Polling" || state == "Waiting for feedback";
				}
			Render ();
			}
		}
	internal void Update (TimerReading reading)
		{
		lock (_sync)
			{
			if (_disposed || _reading != null && reading.Revision <= _reading.Revision)
				{
				return;
				}
			_reading = reading;
			_available = true;
			Render ();
			}
		}
	private void Render ()
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return;
				}
			bool fresh = _available && _reading?.IsFresh (DateTimeOffset.UtcNow, _liveUpdates) == true;
			TileStatus = !_available ? "Connection unavailable" : _reading?.Tile (DateTimeOffset.UtcNow, _liveUpdates) ?? "Waiting for feedback";
			TileIcon = !fresh ? "icSprinklersOffDisabled" : _reading.Zones.Any (z => z.Active == true) ? "icSprinklersOn" : "icSprinklersOff";
			IsOnline = fresh;
			IsReady = fresh;
			CanStop = _available && (!fresh || _reading.Zones.Count != _zoneCount || _reading.Zones.Any (z => z.Active != false) || _pending.Any (pending => pending));
			Battery = _reading?.Battery ?? "Battery unknown";
			Signal = _reading?.Signal ?? "RF unknown";
			ReportTime = _reading?.ReportTime?.ToUniversalTime ().ToString ("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "Report time unknown";
			ZoneReading zone1 = _reading?.Zones.FirstOrDefault (x => x.Zone == 1);
			Zone1Status = zone1 == null ? "Unknown" : (fresh ? "" : "Last reported: ") + zone1.Status;
			// Zone 1 history volume and date are updated together by UpdateHistory.
			Zone1Details = zone1?.TimeLeft (DateTimeOffset.UtcNow, fresh) ?? "Time left unavailable";
			Zone1Alarms = zone1?.Alarms ?? "Alarms unknown";
			Zone1CanStart = fresh && zone1?.Active == false && !_pending[0];
			ZoneControl control1 = Control (1);
			Zone1ActionLabel = control1.Label;
			Zone1CanControl = control1.Enabled;
			if (_zoneCount >= 2)
				{
				ZoneReading zone2 = _reading?.Zones.FirstOrDefault (x => x.Zone == 2);
				Zone2Status = zone2 == null ? "Unknown" : (fresh ? "" : "Last reported: ") + zone2.Status;
				// Zone 2 history volume and date are updated together by UpdateHistory.
				Zone2Details = zone2?.TimeLeft (DateTimeOffset.UtcNow, fresh) ?? "Time left unavailable";
				Zone2Alarms = zone2?.Alarms ?? "Alarms unknown";
				Zone2CanStart = fresh && zone2?.Active == false && !_pending[1];
				ZoneControl control2 = Control (2);
				Zone2ActionLabel = control2.Label;
				Zone2CanControl = control2.Enabled;
				}
			if (_zoneCount >= 3)
				{
				ZoneReading zone3 = _reading?.Zones.FirstOrDefault (x => x.Zone == 3);
				Zone3Status = zone3 == null ? "Unknown" : (fresh ? "" : "Last reported: ") + zone3.Status;
				// Zone 3 history volume and date are updated together by UpdateHistory.
				Zone3Details = zone3?.TimeLeft (DateTimeOffset.UtcNow, fresh) ?? "Time left unavailable";
				Zone3Alarms = zone3?.Alarms ?? "Alarms unknown";
				Zone3CanStart = fresh && zone3?.Active == false && !_pending[2];
				ZoneControl control3 = Control (3);
				Zone3ActionLabel = control3.Label;
				Zone3CanControl = control3.Enabled;
				}
			RenderProgramming (fresh);
			}
		}
	internal void CommandResult (int zone, string message)
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return;
				}
			if (message == "Sending start")
				_pendingStart[zone - 1] = true;
			else if (message == "Sending stop")
				_pendingStart[zone - 1] = false;
			switch (zone)
				{
				case 1:
					Zone1Command = message;
					break;
				case 2:
					Zone2Command = message;
					break;
				case 3:
					Zone3Command = message;
					break;
				}
			_pending[zone - 1] = message.StartsWith ("Sending", StringComparison.Ordinal)
				|| message.StartsWith ("Accepted", StringComparison.Ordinal) || message.StartsWith ("Result uncertain", StringComparison.Ordinal);
			Render ();
			}
		}
	[EntityProperty (Id = "zone1UsageWhen")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1UsageWhen
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1UsageWhen", new DriverEntityValue (value));
			}
		} = "Date unavailable";

	[EntityProperty (Id = "zone1HistoryStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone1HistoryStatus
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone1HistoryStatus", new DriverEntityValue (value));
			}
		} = "Waiting for history";

	[EntityProperty (Id = "zone2UsageWhen")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2UsageWhen
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2UsageWhen", new DriverEntityValue (value));
			}
		} = "Date unavailable";

	[EntityProperty (Id = "zone2HistoryStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone2HistoryStatus
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone2HistoryStatus", new DriverEntityValue (value));
			}
		} = "Waiting for history";

	[EntityProperty (Id = "zone3UsageWhen")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3UsageWhen
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3UsageWhen", new DriverEntityValue (value));
			}
		} = "Date unavailable";

	[EntityProperty (Id = "zone3HistoryStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string Zone3HistoryStatus
		{
		get;
		private set
			{
			if (field == value)
				return;
			field = value;
			NotifyPropertyChanged ("zone3HistoryStatus", new DriverEntityValue (value));
			}
		} = "Waiting for history";

	internal void UpdateHistory (int zone, RecordedUsage record, bool success)
		{
		lock (_sync)
			{
			if (_disposed || zone < 1 || zone > _zoneCount)
				return;
			var old = _usage[zone - 1];
			if (success && record != null && (old == null || record.CloudTimestamp >= old.CloudTimestamp))
				_usage[zone - 1] = record;
			var retained = _usage[zone - 1];
			string volume = retained?.VolumeLabel ?? "Usage unavailable";
			string time = retained?.TimeLabel ?? "Date unavailable";
			string status = !success ? "History refresh unavailable" : record == null ? "No usage record in recent history" : string.Empty;
			switch (zone)
				{
				case 1:
					Zone1Usage = volume;
					Zone1UsageWhen = time;
					Zone1HistoryStatus = status;
					break;
				case 2:
					Zone2Usage = volume;
					Zone2UsageWhen = time;
					Zone2HistoryStatus = status;
					break;
				case 3:
					Zone3Usage = volume;
					Zone3UsageWhen = time;
					Zone3HistoryStatus = status;
					break;
				}
			}
		}

	internal void UpdatePlans (string[] plans)
		{
		lock (_sync)
			{
			Zone1Plans = plans[0];
			if (_zoneCount >= 2)
				Zone2Plans = plans[1];
			if (_zoneCount >= 3)
				Zone3Plans = plans[2];
			}
		}

	private ZoneControl Control (int zone) => ZoneControl.For (_reading?.Zones.FirstOrDefault (z => z.Zone == zone)?.Active,
		_reading?.IsFresh (DateTimeOffset.UtcNow, _liveUpdates) == true, _available, _pending[zone - 1], _pendingStart[zone - 1]);
	private void OperateZone (int zone)
		{
		if (zone > _zoneCount)
			return;
		ZoneControl control;
		int minutes;
		lock (_sync)
			{
			if (_disposed)
				return;
			control = Control (zone);
			minutes = zone == 1 ? Zone1Minutes : zone == 2 ? Zone2Minutes : Zone3Minutes;
			}
		if (!control.Enabled)
			return;
		if (control.Stop)
			_ = _controller.StopAsync (ControllerId, zone);
		else
			_ = _controller.StartAsync (ControllerId, zone, minutes);
		}
	[EntityCommand (Id = "operateZone1")]
	public void OperateZone1 () => OperateZone (1);
	[EntityCommand (Id = "operateZone2")]
	public void OperateZone2 () => OperateZone (2);
	[EntityCommand (Id = "operateZone3")]
	public void OperateZone3 () => OperateZone (3);

	[EntityCommand (Id = "setZone1Minutes")]
	public void SetZone1Minutes ([EntityParameter (RangeMinimum = 1, RangeMaximum = 120)] int value)
		{
		lock (_sync)
			{
			Render ();
			if (!_disposed && Zone1CanStart && value >= 1 && value <= 120)
				{
				Zone1Minutes = value;
				}
			}
		}
	[EntityCommand (Id = "startZone1", FriendlyName = "Start zone 1", NameLocalizationKey = "Programming_startZone1")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartZone1 () => _ = _controller.StartAsync (ControllerId, 1, Zone1Minutes);
	[EntityCommand (Id = "stopZone1", FriendlyName = "Stop zone 1", NameLocalizationKey = "Programming_stopZone1")]
	[EntityCommandMetadata (Programmable = true)]
	public void StopZone1 () => _ = _controller.StopAsync (ControllerId, 1);

	[EntityCommand (Id = "setZone2Minutes")]
	public void SetZone2Minutes ([EntityParameter (RangeMinimum = 1, RangeMaximum = 120)] int value)
		{
		lock (_sync)
			{
			Render ();
			if (!_disposed && Zone2CanStart && value >= 1 && value <= 120)
				{
				Zone2Minutes = value;
				}
			}
		}
	[EntityCommand (Id = "startZone2", FriendlyName = "Start zone 2", NameLocalizationKey = "Programming_startZone2")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartZone2 () => _ = _controller.StartAsync (ControllerId, 2, Zone2Minutes);
	[EntityCommand (Id = "stopZone2", FriendlyName = "Stop zone 2", NameLocalizationKey = "Programming_stopZone2")]
	[EntityCommandMetadata (Programmable = true)]
	public void StopZone2 () => _ = _controller.StopAsync (ControllerId, 2);

	[EntityCommand (Id = "setZone3Minutes")]
	public void SetZone3Minutes ([EntityParameter (RangeMinimum = 1, RangeMaximum = 120)] int value)
		{
		lock (_sync)
			{
			Render ();
			if (!_disposed && Zone3CanStart && value >= 1 && value <= 120)
				{
				Zone3Minutes = value;
				}
			}
		}
	[EntityCommand (Id = "startZone3", FriendlyName = "Start zone 3", NameLocalizationKey = "Programming_startZone3")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartZone3 () => _ = _controller.StartAsync (ControllerId, 3, Zone3Minutes);
	[EntityCommand (Id = "stopZone3", FriendlyName = "Stop zone 3", NameLocalizationKey = "Programming_stopZone3")]
	[EntityCommandMetadata (Programmable = true)]
	public void StopZone3 () => _ = _controller.StopAsync (ControllerId, 3);

	[EntityCommand (Id = "stopAll", FriendlyName = "Stop all zones", NameLocalizationKey = "Programming_stopAll")]
	[EntityCommandMetadata (Programmable = true)]
	public void StopAll () => _ = _controller.StopAllAsync (ControllerId);
	[EntityCommand (Id = "refresh")]
	public void Refresh () => _ = _controller.RefreshAsync (ControllerId, false);
	[EntityCommand (Id = "refreshPlans")]
	public void RefreshPlans () => _ = _controller.RefreshAsync (ControllerId, true);
	public override void Dispose ()
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return;
				}
			_disposed = true;
			_ageTimer.Dispose ();
			base.Dispose ();
			}
		}
	}