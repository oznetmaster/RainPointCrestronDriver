// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RainPoint.CrestronDriver.Core;

/// <summary>Owns one hub session. Reconfiguration cancels old work before disposing it; writes are never replayed.</summary>
public sealed class IrrigationController
	{
	private sealed class PendingCommand (DateTimeOffset sent, bool active)
		{
		/// <summary>
		/// The instant the pending command was submitted.
		/// </summary>
		internal readonly DateTimeOffset Sent = sent;
		/// <summary>
		/// The activity expected from the pending command.
		/// </summary>
		internal readonly bool Active = active;
		}
	private sealed class HistoryUpdate (int address, int zone, RecordedUsage record, bool success)
		{
		/// <summary>
		/// The RF address and one-based zone to which the history update belongs.
		/// </summary>
		internal readonly int Address = address, Zone = zone;
		/// <summary>
		/// The latest matching cloud usage record, or null when none was returned.
		/// </summary>
		internal readonly RecordedUsage Record = record;
		/// <summary>
		/// Whether the history request completed successfully.
		/// </summary>
		internal readonly bool Success = success;
		}
	private sealed class Session (DriverSettings settings)
		{
		/// <summary>
		/// The configuration captured for this session.
		/// </summary>
		internal readonly DriverSettings Settings = settings;
		/// <summary>
		/// Cancellation shared by the session and its background work.
		/// </summary>
		internal readonly CancellationTokenSource Stop = new ();
		/// <summary>
		/// The connection owned by this session.
		/// </summary>
		internal IRainPointConnection Connection;
		/// <summary>
		/// Whether the session has completed discovery.
		/// </summary>
		internal bool Ready;
		/// <summary>
		/// Whether the session currently has an active push observer.
		/// </summary>
		internal bool LiveUpdates;
		/// <summary>
		/// The commissioned timers belonging to this session.
		/// </summary>
		internal IReadOnlyList<TimerIdentity> Timers = Array.Empty<TimerIdentity> ();
		/// <summary>
		/// Latest accepted observations keyed by timer RF address.
		/// </summary>
		internal readonly Dictionary<int, TimerReading> Readings = [];
		/// <summary>
		/// Latest usage updates keyed by timer address and zone.
		/// </summary>
		internal readonly Dictionary<string, HistoryUpdate> History = [];
		/// <summary>
		/// Commands awaiting device feedback, keyed by timer address and zone.
		/// </summary>
		internal readonly Dictionary<string, PendingCommand> Pending = [];
		}
	private readonly object _sync = new ();
	private readonly SemaphoreSlim _operations = new (1, 1);
	private readonly Func<IRainPointConnection> _factory;
	private readonly Func<DateTimeOffset> _now;
	private Session _session;
	private Task _transition = Task.CompletedTask;
	private bool _disposed;
	/// <summary>
	/// Creates the session coordinator with optional connection and clock substitutes.
	/// </summary>
	/// <param name="factory">An optional connection factory; null uses the production cloud connection.</param>
	/// <param name="now">An optional UTC clock; null uses DateTimeOffset.UtcNow.</param>
	public IrrigationController (Func<IRainPointConnection> factory = null, Func<DateTimeOffset> now = null)
		{
		_factory = factory ?? (() => new CloudConnection ());
		_now = now ?? (() => DateTimeOffset.UtcNow);
		}
	/// <summary>
	/// Occurs with the current timer catalog after discovery, metadata refresh or configuration removal.
	/// </summary>
	public event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
	/// <summary>
	/// Occurs with accepted feedback for a timer in the current session.
	/// </summary>
	public event Action<TimerReading> Reading;
	/// <summary>
	/// Occurs when configuration, cloud connectivity or feedback availability changes.
	/// </summary>
	public event Action<string> StateChanged;
	/// <summary>
	/// Occurs with RF address, zone and command-progress text; acknowledgement does not imply valve state.
	/// </summary>
	public event Action<int, int, string> CommandChanged;
	/// <summary>
	/// Occurs with RF address and current per-zone plan summaries.
	/// </summary>
	public event Action<int, string[]> PlansChanged;
	/// <summary>
	/// Occurs with RF address, zone, latest recorded usage and the history-read success flag.
	/// </summary>
	public event Action<int, int, RecordedUsage, bool> HistoryChanged;
	/// <summary>
	/// Occurs with a diagnostic message for the caller's logging system.
	/// </summary>
	public event Action<string> Diagnostic;

	/// <summary>
	/// Cancels the previous session before applying changed settings; null removes the configuration.
	/// </summary>
	/// <param name="settings">The next configuration, or null to disconnect and clear discovery.</param>
	/// <returns>A task that completes when the operation finishes.</returns>
	public Task ConfigureAsync (DriverSettings settings)
		{
		lock (_sync)
			{
			if (_disposed)
				{
				throw new ObjectDisposedException (nameof (IrrigationController));
				}
			if (_session?.Settings.Matches (settings) == true)
				{
				return _transition;
				}
			return ReplaceLocked (settings);
			}
		}

	private Task ReplaceLocked (DriverSettings settings)
		{
		Session old = _session;
		old?.Stop.Cancel ();
		Session next = settings == null ? null : new Session (settings);
		_session = next;
		Task previous = _transition;
		StateChanged?.Invoke (settings == null ? "Not configured" : "Connecting");
		_transition = Task.Run (async () =>
			{
				await previous.ConfigureAwait (false);
				await _operations.WaitAsync ().ConfigureAwait (false);
				try
					{
					if (old != null)
						{
						try
							{
							if (old.Connection != null)
								{
								await old.Connection.CloseAsync ().ConfigureAwait (false);
								}
							}
						finally
							{
							old.Stop.Dispose ();
							}
						}
					lock (_sync)
						{
						if (!ReferenceEquals (_session, next))
							{
							return;
							}
						if (next == null)
							{
							CatalogChanged?.Invoke (Array.Empty<TimerIdentity> ());
							}
						}
					if (next == null)
						{
						return;
						}
					next.Connection = _factory ();
					next.Connection.CatalogChanged += timers =>
						{
							lock (_sync)
								{
								if (!ReferenceEquals (_session, next) || next.Stop.IsCancellationRequested)
									return;
								next.Timers = timers;
								if (next.Ready)
									CatalogChanged?.Invoke (timers);
								}
						};
					next.Connection.PlansChanged += (address, plans) =>
						{
							lock (_sync)
								{
								if (ReferenceEquals (_session, next) && !next.Stop.IsCancellationRequested)
									PlansChanged?.Invoke (address, plans);
								}
						};
					next.Connection.HistoryChanged += (address, zone, record, success) =>
						{
							lock (_sync)
								{
								if (!ReferenceEquals (_session, next) || next.Stop.IsCancellationRequested)
									return;
								next.History[Key (address, zone)] = new HistoryUpdate (address, zone, record, success);
								if (next.Ready)
									HistoryChanged?.Invoke (address, zone, record, success);
								}
						};
					next.Connection.Reading += frame => Accept (next, frame);
					next.Connection.ConnectionState += state => PublishState (next, state);
					var timers = await next.Connection.ConnectAsync (next.Settings, next.Stop.Token).ConfigureAwait (false);
					lock (_sync)
						{
						if (!ReferenceEquals (_session, next))
							{
							return;
							}
						next.Timers = timers;
						next.Ready = true;
						CatalogChanged?.Invoke (timers);
						foreach (var history in next.History.Values)
							HistoryChanged?.Invoke (history.Address, history.Zone, history.Record, history.Success);
						StateChanged?.Invoke ("Waiting for feedback");
						foreach (TimerReading frame in next.Readings.Values)
							{
							Reading?.Invoke (frame);
							}
						}
					await next.Connection.RefreshAsync (next.Stop.Token).ConfigureAwait (false);
					}
				catch (OperationCanceledException) when (next?.Stop.IsCancellationRequested == true) { }
				catch (Exception error)
					{
					// Exception messages may contain cloud response data. Log only the type.
					Diagnostic?.Invoke ("Connection failed: " + error.GetType ().Name);
					PublishState (next, "Connection unavailable; reconnect from setup");
					}
				finally
					{
					_operations.Release ();
					}
			});
		return _transition;
		}

	private void PublishState (Session session, string state)
		{
		lock (_sync)
			{
			if (!ReferenceEquals (_session, session))
				{
				return;
				}
			if (session != null && (state == "AuthenticationRequired" || state.StartsWith ("Connection unavailable", StringComparison.Ordinal)))
				{
				session.Ready = false;
				}
			if (session != null)
				session.LiveUpdates = state == "PushConnected";
			StateChanged?.Invoke (state);
			}
		}

	private void Accept (Session session, TimerReading frame)
		{
		lock (_sync)
			{
			if (!ReferenceEquals (_session, session) || session.Stop.IsCancellationRequested
				|| session.Readings.TryGetValue (frame.Timer.Address, out TimerReading last) && last.Revision >= frame.Revision)
				{
				return;
				}
			session.Readings[frame.Timer.Address] = frame;
			foreach (ZoneReading zone in frame.Zones)
				{
				if (frame.IsFresh (_now (), session.LiveUpdates) && last?.Zones.FirstOrDefault (z => z.Zone == zone.Zone)?.Active == true && zone.Active == false)
					session.Connection.RequestHistory (frame.Timer.Address, zone.Zone, true);
				string key = Key (frame.Timer.Address, zone.Zone);
				if (session.Pending.TryGetValue (key, out PendingCommand pending) && frame.ReportTime > pending.Sent && zone.Active == pending.Active)
					{
					session.Pending.Remove (key);
					CommandChanged?.Invoke (frame.Timer.Address, zone.Zone, "New report: " + zone.Status);
					}
				else if (!session.Pending.ContainsKey (key) && frame.IsFresh (_now (), session.LiveUpdates) && last != null
					&& last.Zones.FirstOrDefault (z => z.Zone == zone.Zone)?.Active != zone.Active)
					{
					CommandChanged?.Invoke (frame.Timer.Address, zone.Zone, "New report: " + zone.Status);
					}
				}
			if (session.Timers.Count > 0)
				{
				session.Ready = true;
				Reading?.Invoke (frame);
				}
			}
		}

	/// <summary>
	/// Requests timed watering for a commissioned zone while rejecting stale or duplicate starts.
	/// </summary>
	/// <param name="timerId">The stable commissioned timer controller ID.</param>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <param name="minutes">The watering duration in whole minutes, from 1 through 120.</param>
	/// <returns>A task that completes when the operation finishes; command completion is not proof of valve actuation.</returns>
	/// <exception cref="System.ArgumentOutOfRangeException">An argument is outside the supported range described above.</exception>
	public Task StartAsync (string timerId, int zone, int minutes)
		{
		ValidateZone (zone);
		if (minutes < 1 || minutes > 120)
			{
			throw new ArgumentOutOfRangeException (nameof (minutes));
			}
		return CommandAsync (timerId, zone, minutes);
		}
	/// <summary>
	/// Requests a stop for a commissioned zone without replaying uncertain writes.
	/// </summary>
	/// <param name="timerId">The stable commissioned timer controller ID.</param>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <returns>A task that completes when the operation finishes; command completion is not proof of valve actuation.</returns>
	public Task StopAsync (string timerId, int zone)
		{
		ValidateZone (zone);
		return CommandAsync (timerId, zone, null);
		}
	/// <summary>
	/// Attempts to stop every actual zone independently so one failure does not suppress the other attempts.
	/// </summary>
	/// <param name="timerId">The stable commissioned timer controller ID.</param>
	/// <returns>A task that completes when the operation finishes; command completion is not proof of valve actuation.</returns>
	public async Task StopAllAsync (string timerId)
		{
		Session session;
		int zoneCount;
		lock (_sync)
			{
			session = _session;
			zoneCount = FindCurrent (session, timerId).ZoneCount;
			}
		// Each zone is attempted independently; one failed stop cannot suppress the others.
		for (int zone = 1; zone <= zoneCount; zone++)
			{
			await CommandAsync (timerId, zone, null, session, true).ConfigureAwait (false);
			}
		}
	private async Task CommandAsync (string timerId, int zone, int? minutes, Session captured = null, bool useCaptured = false)
		{
		Session session;
		lock (_sync)
			{
			session = useCaptured ? captured : _session;
			}
		await _operations.WaitAsync ().ConfigureAwait (false);
		TimerIdentity timer = null;
		try
			{
			lock (_sync)
				{
				timer = FindCurrent (session, timerId);
				if (zone > timer.ZoneCount)
					return;
				string key = Key (timer.Address, zone);
				if (minutes.HasValue)
					{
					if (!session.Readings.TryGetValue (timer.Address, out TimerReading reading) || !reading.IsFresh (_now (), session.LiveUpdates)
						|| reading.Zones.Single (z => z.Zone == zone).Active != false)
						{
						CommandChanged?.Invoke (timer.Address, zone, "Start unavailable: refresh an idle zone first");
						return;
						}
					if (session.Pending.TryGetValue (key, out PendingCommand pending) && _now () - pending.Sent < TimeSpan.FromSeconds (90))
						{
						return;
						}
					}
				session.Pending[key] = new PendingCommand (_now (), minutes.HasValue);
				CommandChanged?.Invoke (timer.Address, zone, minutes.HasValue ? "Sending start" : "Sending stop");
				}
			if (minutes.HasValue)
				{
				await session.Connection.StartAsync (timer.Address, zone, minutes.Value, session.Stop.Token).ConfigureAwait (false);
				}
			else
				{
				await session.Connection.StopAsync (timer.Address, zone, session.Stop.Token).ConfigureAwait (false);
				}
			lock (_sync)
				{
				if (ReferenceEquals (session, _session) && session.Pending.ContainsKey (Key (timer.Address, zone)))
					{
					CommandChanged?.Invoke (timer.Address, zone, "Accepted; awaiting timer report");
					}
				}
			}
		catch (Exception error)
			{
			Diagnostic?.Invoke ("Command not replayed: " + error.GetType ().Name);
			lock (_sync)
				{
				if (timer != null && ReferenceEquals (session, _session))
					{
					CommandChanged?.Invoke (timer.Address, zone, "Result uncertain; check reported status");
					}
				}
			}
		finally
			{
			_operations.Release ();
			}
		}

	/// <summary>
	/// Refreshes the current timer session and optionally its saved-plan summaries.
	/// </summary>
	/// <param name="timerId">The stable commissioned timer controller ID.</param>
	/// <param name="plans">Whether to refresh saved-plan summaries along with timer feedback.</param>
	/// <returns>A task that completes when the operation finishes.</returns>
	public async Task RefreshAsync (string timerId, bool plans)
		{
		Session session;
		lock (_sync)
			{
			session = _session;
			}
		await _operations.WaitAsync ().ConfigureAwait (false);
		try
			{
			TimerIdentity timer;
			lock (_sync)
				{
				timer = FindCurrent (session, timerId);
				}
			await session.Connection.RefreshAsync (session.Stop.Token).ConfigureAwait (false);
			if (plans)
				{
				string[] summary = await session.Connection.ReadPlansAsync (timer.Address, session.Stop.Token).ConfigureAwait (false);
				lock (_sync)
					{
					if (ReferenceEquals (session, _session))
						{
						PlansChanged?.Invoke (timer.Address, summary);
						}
					}
				}
			}
		catch (Exception error)
			{
			Diagnostic?.Invoke ("Refresh failed: " + error.GetType ().Name);
			PublishState (session, "Refresh unavailable");
			}
		finally
			{
			_operations.Release ();
			}
		}

	private TimerIdentity FindCurrent (Session session, string id)
		{
		if (session == null || !ReferenceEquals (session, _session) || !session.Ready || session.Stop.IsCancellationRequested)
			{
			throw new InvalidOperationException ("Connection changed or is unavailable.");
			}
		return session.Timers.Single (t => t.ControllerId == id);
		}
	private static string Key (int address, int zone) => address + ":" + zone;
	/// <summary>
	/// Marks commands whose expected device feedback has not arrived within the allowed interval.
	/// </summary>
	public void CheckFeedbackTimeouts ()
		{
		lock (_sync)
			{
			if (_session == null)
				{
				return;
				}
			foreach (var pair in _session.Pending.Where (p => _now () - p.Value.Sent >= TimeSpan.FromSeconds (90)).ToArray ())
				{
				_session.Pending.Remove (pair.Key);
				string[] parts = pair.Key.Split (':');
				CommandChanged?.Invoke (int.Parse (parts[0]), int.Parse (parts[1]), "Feedback timeout; check timer status");
				}
			}
		}
	private static void ValidateZone (int zone)
		{
		if (zone < 1 || zone > 3)
			{
			throw new ArgumentOutOfRangeException (nameof (zone));
			}
		}
	/// <summary>
	/// Prevents new work, cancels the active session and waits for its resources to close.
	/// </summary>
	/// <returns>A task that completes when the operation finishes.</returns>
	public Task ShutdownAsync ()
		{
		lock (_sync)
			{
			if (_disposed)
				{
				return _transition;
				}
			_disposed = true;
			return ReplaceLocked (null);
			}
		}
	}