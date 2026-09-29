// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RainPointClient;

namespace RainPoint.CrestronDriver.Core;

public sealed class CloudConnection : IRainPointConnection
	{
	private readonly RainPointCloudClient _client;
	public CloudConnection () : this (new RainPointCloudClient ()) { }
	internal CloudConnection (RainPointCloudClient client) => _client = client;
	private readonly SemaphoreSlim _metadataSignal = new (0, 1);
	private CancellationTokenSource _metadataStop;
	private Task _metadataTask = Task.CompletedTask;
	public event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
	public event Action<int, string[]> PlansChanged;
	private UsageHistoryWorker _history;
	public event Action<int, int, RecordedUsage, bool> HistoryChanged;
	public void RequestHistory (int address, int zone, bool completion) => _history?.Request (address, zone, completion);
	internal async Task<RecordedUsage> ReadLastUsageAsync (int address, int zone, CancellationToken token)
		{
		RainPointHub hub = _hub;
		var page = await _client.GetEventsAsync (hub.HomeId,
			new RainPointEventQuery { HubId = hub.Id, Address = address, Zone = zone, Limit = 50 }, token).ConfigureAwait (false);
		return RecordedUsage.Latest (page.Events, hub.Id, address, zone);
		}
	private void RequestAllHistory ()
		{
		foreach (var timer in _timers)
			for (int zone = 1; zone <= 3; zone++)
				RequestHistory (timer.Address, zone, false);
		}
	private RainPointHub _hub;
	private RainPointCalendarTimeZone _timeZone;
	private RainPointMonitor _monitor;
	private RainPointSessionRecovery _recovery;
	private Task _monitorTask = Task.CompletedTask;
	private Task _recoveryTask = Task.CompletedTask;
	private IReadOnlyList<TimerIdentity> _timers = Array.Empty<TimerIdentity> ();
	public event Action<TimerReading> Reading;
	public event Action<string> ConnectionState;

	public async Task<IReadOnlyList<TimerIdentity>> ConnectAsync (DriverSettings settings, CancellationToken token)
		{
		await _client.LoginAsync (settings.Email, settings.Password, settings.AreaCode, token).ConfigureAwait (false);
		var homes = await _client.GetHomesAsync (token).ConfigureAwait (false);
		RainPointHome home = Selection.Unique (homes, h => h.Id, settings.HomeId, "home");
		try
			{
			_timeZone = (await _client.GetHomeAsync (home.Id, token).ConfigureAwait (false)).CalendarTimeZone;
			}
		catch (RainPointException) { } // Countdown is optional; unavailable calendar data must not prevent control.
		catch (System.Net.Http.HttpRequestException) { }
		var hubs = await _client.GetHubsAsync (home.Id, token).ConfigureAwait (false);
		_hub = Selection.Unique (hubs.Where (h => h.Model == "HWG023WBRF" || h.Model == "HWG023WBRF-V2"), h => h.Id, settings.HubId, "hub");
		InitializeMetadata (_hub);
		_monitor = new RainPointMonitor (_client, _hub, new RainPointMonitorOptions { PollWhilePushConnected = false });
		_monitor.StatusReceived += OnStatus;
		StartMetadata (token);
		_history = new UsageHistoryWorker (ReadLastUsageAsync, (address, zone, record, success) => HistoryChanged?.Invoke (address, zone, record, success), token);
		RequestAllHistory ();
		_monitor.ConfigurationChanged += (_, _) => RequestMetadata ();
		_monitor.StateChanged += (_, e) =>
			{
				ConnectionState?.Invoke (e.State.ToString ());
				if (e.State == RainPointMonitorState.PushConnected)
					{
					RequestMetadata ();
					RequestAllHistory ();
					}
			};
		_recovery = new RainPointSessionRecovery (_client, ct =>
			{
				ct.ThrowIfCancellationRequested ();
				return Task.FromResult (new RainPointCredentials (settings.Email, settings.Password, settings.AreaCode));
			});
		_recovery.StateChanged += (_, e) =>
			{
				if (e.State == RainPointSessionState.AuthenticationRequired)
					{
					ConnectionState?.Invoke ("AuthenticationRequired");
					}
			};
		// Coordinator publishes the catalog before it requests the first refresh. Events are
		// also retained there, so a quick monitor callback cannot lose the initial status.
		_monitorTask = _monitor.RunAsync (token);
		_recoveryTask = _recovery.RunAsync (token);
		return _timers;
		}

	internal void InitializeMetadata (RainPointHub hub)
		{
		_hub = hub;
		_timers = hub.Devices.Where (d => d.SupportedZoneCount == 3)
			.Select (d => new TimerIdentity (hub.Id, d.Address, d.Name, hub.Name, d.ZoneNames)).ToArray ();
		if (_timers.Count == 0 || _timers.Select (t => t.Address).Distinct ().Count () != _timers.Count)
			{
			throw new InvalidOperationException ("No uniquely addressed supported timers were discovered.");
			}
		}
	internal void StartMetadata (CancellationToken token)
		{
		_metadataStop = CancellationTokenSource.CreateLinkedTokenSource (token);
		_metadataTask = MetadataLoopAsync (_metadataStop.Token);
		}
	internal async Task RefreshMetadataAsync (CancellationToken token)
		{
		RainPointHub old = _hub;
		var hubs = await _client.GetHubsAsync (old.HomeId, token).ConfigureAwait (false);
		RainPointHub fresh = hubs.Single (h => h.Id == old.Id);
		if (fresh.DeviceName != old.DeviceName || fresh.ProductKey != old.ProductKey)
			throw new InvalidOperationException ("Hub transport identity changed.");
		// Metadata updates preserve the commissioned timer identities. Pairing changes require rediscovery.
		var timers = _timers.Select (timer =>
			{
				var device = fresh.Devices.Single (d => d.Address == timer.Address && d.SupportedZoneCount == 3);
				return new TimerIdentity (fresh.Id, device.Address, device.Name, fresh.Name, device.ZoneNames);
			}).ToArray ();
		token.ThrowIfCancellationRequested ();
		_hub = fresh;
		_timers = timers;
		CatalogChanged?.Invoke (timers);
		foreach (TimerIdentity timer in timers)
			{
			string[] plans = await ReadPlansAsync (timer.Address, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			PlansChanged?.Invoke (timer.Address, plans);
			}
		}

	internal void RequestMetadata ()
		{
		try
			{
			_metadataSignal.Release ();
			}
		catch (SemaphoreFullException) { }
		}

	private async Task MetadataLoopAsync (CancellationToken token)
		{
		try
			{
			while (true)
				{
				await _metadataSignal.WaitAsync (token).ConfigureAwait (false);
				await Task.Delay (250, token).ConfigureAwait (false);
				// Coalesce a burst of home revision notifications into one configuration read.
				_metadataSignal.Wait (0);
				try
					{
					await RefreshMetadataAsync (token).ConfigureAwait (false);
					}
				catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
				catch (Exception error) when (error is RainPointException or System.Net.Http.HttpRequestException or OperationCanceledException or InvalidOperationException)
					{
					// Retain displayed metadata on a failed read; a metadata failure does not make watering feedback offline.
					await Task.Delay (TimeSpan.FromSeconds (30), token).ConfigureAwait (false);
					RequestMetadata ();
					}
				}
			}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		}

	private void OnStatus (object sender, RainPointStatusUpdate update)
		{
		foreach (TimerIdentity timer in _timers)
			{
			RainPointTimerObservation observation = update.Timers.FirstOrDefault (t => t.Status.Address == timer.Address);
			RainPointTimerStatus status = observation?.Status;
			DateTimeOffset? contact = update.LastSuccessfulPollAt;
			if (observation != null && (!contact.HasValue || observation.ReceivedAt > contact))
				{
				contact = observation.ReceivedAt;
				}
			var zones = Enumerable.Range (1, 3).Select (number =>
				{
					RainPointZoneStatus z = status?.Zones.FirstOrDefault (x => x.Zone == number);
					string mode = z?.WorkMode switch
						{
							RainPointWateringMode.Normal => "Watering",
							RainPointWateringMode.Misting => "Misting",
							RainPointWateringMode.CycleAndSoak => "Cycle watering",
							RainPointWateringMode.CycleAndSoakPause => "Soaking (cycle active)",
							_ => "Unknown"
							};
					string alarms = z?.AlarmCode == null ? "Alarms unknown" : z.AlarmCode == 0 ? "No reported alarms"
						: string.Join (", ", new[] { z.WaterLeakReported == true ? "Leak" : null,
						z.WaterShortageReported == true ? "Water shortage" : null,
						z.FreezeReported == true ? "Freeze" : null,
						z.UnknownAlarmBits > 0 ? "Other alarm" : null }.Where (x => x != null));
					return new ZoneReading (number, z?.IsOpen, mode, z?.LastWaterUsageLitres, z?.ConfiguredRunDuration, alarms,
						_timeZone == null ? null : ReportedEndTime.Resolve (z?.EventTimeLocal, _timeZone.BaseOffset, _timeZone.DaylightAdjustment, _timeZone.Transitions), z?.AlarmCode);
				}).ToArray ();
			Reading?.Invoke (new TimerReading (timer, update.Revision, update.Status.IsConnected, status?.LastDataChange, zones,
				status?.IsBatteryLow == true ? "Battery low" : status?.IsBatteryLow == false ? "Battery normal" : "Battery unknown",
				status?.SignalStrengthDbm is int dbm ? "RF " + dbm + " dBm" : "RF signal unknown", contact, status?.IsBatteryLow));
			}
		}

	public async Task StartAsync (int address, int zone, int minutes, CancellationToken token) =>
		await _client.StartWateringAsync (_hub, address, zone, TimeSpan.FromMinutes (minutes), token).ConfigureAwait (false);
	public async Task StopAsync (int address, int zone, CancellationToken token) =>
		await _client.StopWateringAsync (_hub, address, zone, token).ConfigureAwait (false);
	public async Task RefreshAsync (CancellationToken token)
		{
		await _monitor.RefreshAsync (token).ConfigureAwait (false);
		ConnectionState?.Invoke (_monitor.LiveUpdatesAvailable ? "PushConnected" : _monitor.State.ToString ());
		}
	public async Task<string[]> ReadPlansAsync (int address, CancellationToken token)
		{
		var result = new string[3];
		for (int zone = 1; zone <= 3; zone++)
			{
			RainPointScheduleSnapshot snapshot = await _client.GetTimerSchedulesAsync (_hub, address, zone, token).ConfigureAwait (false);
			result[zone - 1] = snapshot.Availability != TimerReadingAvailability.Decoded ? "Plans unavailable"
				: snapshot.Schedules.Count == 0 ? "No saved plans" : string.Join ("; ", snapshot.Schedules.Select (p =>
					(p.Enabled ? "On " : "Off ") + p.StartTime.ToString (@"hh\:mm", CultureInfo.InvariantCulture) + " "
					+ p.Repeat + " / " + p.Mode + " / " + p.Duration.TotalMinutes.ToString ("0.#", CultureInfo.InvariantCulture) + " min"));
			result[zone - 1] += snapshot.RainDelayAvailability != TimerReadingAvailability.Decoded ? " | Rain delay unknown"
				: snapshot.RainDelayUntil.HasValue ? " | Rain delay end " + snapshot.RainDelayUntil.Value.ToString ("g", CultureInfo.InvariantCulture) + " (home time)"
				: " | No rain delay configured";
			}
		return result;
		}

	private readonly object _closeSync = new ();
	private Task _closing;
	public Task CloseAsync ()
		{
		lock (_closeSync)
			return _closing ??= CloseCoreAsync ();
		}

	private async Task CloseCoreAsync ()
		{
		try
			{
			_metadataStop?.Cancel ();
			try
				{
				await _metadataTask.ConfigureAwait (false);
				}
			finally { _metadataStop?.Dispose (); }
			}
		finally
			{
			try
				{
				if (_history != null)
					await _history.StopAsync ().ConfigureAwait (false);
				}
			finally { await CloseTransportAsync ().ConfigureAwait (false); }
			}
		}

	private async Task CloseTransportAsync ()
		{
		try
			{
			if (_monitor != null)
				{
				await _monitor.StopAsync ().ConfigureAwait (false);
				}
			}
		finally
			{
			try
				{
				if (_recovery != null)
					{
					await _recovery.StopAsync ().ConfigureAwait (false);
					}
				await Task.WhenAll (_monitorTask, _recoveryTask).ConfigureAwait (false);
				}
			finally
				{
				_client.Dispose ();
				}
			}
		}
	}