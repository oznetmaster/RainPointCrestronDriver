// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.EntityModel.Logging;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

using RainPoint.CrestronDriver.Core;

namespace RainPoint.CrestronDriver;

public sealed class RainPointPlatformDriver : ReflectedAttributeDriverEntity
	{
	private readonly object _sync = new ();
	private readonly DriverControllerCreationArgs _args;
	private readonly DriverImplementationResources _resources;
	private readonly IrrigationController _controller;
	private readonly Dictionary<int, RainPointTimerEntity> _timers = [];
	private Dictionary<string, string> _configuration = [];
	private DriverSettings _settings;
	private bool _disposed;
	internal Task Shutdown { get; private set; } = Task.CompletedTask;
	internal DataDrivenConfigurationController ConfigurationController
		{
		get;
		}

	[EntityProperty (Id = "platform:managedDevices", Type = DriverEntityValueType.DeviceDictionary, ItemTypeRef = "platform:ManagedDevice")]
	public IDictionary<string, PlatformManagedDevice> ManagedDevices { get; private set; } = new Dictionary<string, PlatformManagedDevice> ();
	[EntityProperty (Id = "onlineIndicator:isOnline")]
	public bool IsOnline
		{
		get; private set;
		}
	[EntityProperty (Id = "readyIndicator:isReady")]
	public bool IsReady
		{
		get; private set;
		}
	[EntityProperty (Id = "status")]
	public string Status { get; private set; } = "Not configured";

	public RainPointPlatformDriver (DriverControllerCreationArgs args, DriverImplementationResources resources)
		: this (args, resources, new IrrigationController ()) { }
	internal RainPointPlatformDriver (DriverControllerCreationArgs args, DriverImplementationResources resources, IrrigationController controller)
		: base (DriverController.RootControllerId)
		{
		_args = args;
		_resources = resources;
		_controller = controller;
		ConfigurationController = new DelegateDataDrivenConfigurationController (
			DataDrivenConfigurationControllerArgs.FromResources (args, resources, ControllerId), ApplyConfiguration, null, null);
		_controller.CatalogChanged += PublishCatalog;
		_controller.Reading += reading => WithTimer (reading.Timer.Address, t => t.Update (reading));
		_controller.CommandChanged += (address, zone, message) => WithTimer (address, t => t.CommandResult (zone, message));
		_controller.HistoryChanged += (address, zone, record, success) => WithTimer (address, t => t.UpdateHistory (zone, record, success));
		_controller.PlansChanged += (address, plans) => WithTimer (address, t => t.UpdatePlans (plans));
		_controller.StateChanged += SetState;
		_controller.Diagnostic += message => _args.Logger?.Log (ControllerId, LogEntryLevel.Warning, message);
		}

	private void WithTimer (int address, Action<RainPointTimerEntity> action)
		{
		lock (_sync)
			{
			if (!_disposed && _timers.TryGetValue (address, out RainPointTimerEntity entity))
				{
				action (entity);
				}
			}
		}

	private void PublishCatalog (IReadOnlyList<TimerIdentity> timers)
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return;
				}
			var removed = _timers.Values.Where (old => !timers.Any (t => t.ControllerId == old.ControllerId)).ToArray ();
			if (removed.Length > 0)
				{
				UpdateSubControllers (null, removed.Select (t => t.ControllerId).ToArray ());
				foreach (RainPointTimerEntity old in removed)
					{
					_timers.Remove (old.Address);
					old.Dispose ();
					}
				}
			var added = new List<ConfigurableDriverEntity> ();
			var devices = new Dictionary<string, PlatformManagedDevice> ();
			foreach (TimerIdentity identity in timers)
				{
				if (!_timers.TryGetValue (identity.Address, out RainPointTimerEntity entity))
					{
					entity = new RainPointTimerEntity (identity, _controller, _args, _resources);
					_timers[identity.Address] = entity;
					added.Add (new ConfigurableDriverEntity (identity.ControllerId, entity, null));
					}
				entity.UpdateIdentity (identity);
				devices[identity.ControllerId] = new PlatformManagedDevice (DeviceUxCategory.IrrigationSystem,
					identity.Name, "RainPoint", "HTV345FRF", identity.ControllerId);
				}
			if (added.Count > 0)
				{
				UpdateSubControllers (added, null);
				}
			ManagedDevices = devices;
			NotifyPropertyChanged ("platform:managedDevices", CreateValueForEntries (ManagedDevices));
			}
		}

	private void SetState (string state)
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return;
				}
			Status = state;
			if (state != "Reconnecting")
				{
				IsOnline = state == "PushConnected" || state == "Polling" || state == "Waiting for feedback";
				}
			IsReady = IsOnline && _timers.Count > 0;
			NotifyPropertyChanged ("status", new DriverEntityValue (Status));
			NotifyPropertyChanged ("onlineIndicator:isOnline", new DriverEntityValue (IsOnline));
			NotifyPropertyChanged ("readyIndicator:isReady", new DriverEntityValue (IsReady));
			foreach (RainPointTimerEntity timer in _timers.Values)
				{
				timer.SetConnection (state);
				}
			}
		}

	internal ConfigurationItemErrors ApplyConfiguration (DataDrivenConfigurationController.ApplyConfigurationAction action,
		string step, IDictionary<string, DriverEntityValue?> values)
		{
		if (action == DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues)
			{
			_configuration.Clear ();
			_settings = null;
			_ = _controller.ConfigureAsync (null);
			return null;
			}
		var next = new Dictionary<string, string> (_configuration);
		if (values != null)
			{
			foreach (var pair in values.Where (p => p.Value.HasValue))
				{
				next[pair.Key] = pair.Value.Value.ToString ();
				}
			}
		string Read (string key, string fallback = "") => next.TryGetValue (key, out string value) ? value : fallback;
		var errors = new Dictionary<string, string> ();
		long? Id (string key)
			{
			string value = Read (key);
			if (string.IsNullOrWhiteSpace (value))
				{
				return null;
				}
			if (long.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 0)
				{
				return id;
				}
			errors[key] = "Enter a positive numeric ID or leave blank for unique discovery.";
			return null;
			}
		long? home = Id ("HomeId");
		long? hub = Id ("HubId");
		if (string.IsNullOrWhiteSpace (Read ("Email")))
			{
			errors["Email"] = "Email is required.";
			}
		if (string.IsNullOrEmpty (Read ("Password")))
			{
			errors["Password"] = "Password is required.";
			}
		if (_settings != null && !string.Equals (_settings.Email, Read ("Email").Trim (), StringComparison.Ordinal)
			&& (values == null || !values.TryGetValue ("Password", out DriverEntityValue? password) || !password.HasValue))
			{
			errors["Password"] = "Enter the password for the changed account.";
			}
		string area = Read ("AreaCode", "44");
		if (area.Length == 0 || !area.All (c => c >= '0' && c <= '9'))
			{
			errors["AreaCode"] = "Enter the account's numeric country calling code.";
			}
		if (errors.Count > 0)
			{
			return new ConfigurationItemErrors (errors, "Correct the configuration values.");
			}
		_configuration = next;
		_settings = new DriverSettings (Read ("Email"), Read ("Password"), area, home, hub);
		_ = _controller.ConfigureAsync (_settings);
		return null;
		}

	[EntityCommand (Id = "reconnect")]
	public void Reconnect ()
		{
		_ = ReconnectAsync ();
		}
	private async Task ReconnectAsync ()
		{
		await _controller.ConfigureAsync (null).ConfigureAwait (false);
		if (!_disposed)
			{
			await _controller.ConfigureAsync (_settings).ConfigureAwait (false);
			}
		}
	public override void Dispose ()
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return;
				}
			_disposed = true;
			foreach (RainPointTimerEntity timer in _timers.Values)
				{
				timer.Dispose ();
				}
			}
		Shutdown = _controller.ShutdownAsync ();
		base.Dispose ();
		}
	}