// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RainPoint.CrestronDriver.Core;

/// <summary>
/// Provides the cloud session, reported-state events and explicit commands used by the driver controller.
/// </summary>
public interface IRainPointConnection
	{
	/// <summary>
	/// Occurs when assigned hub or timer metadata changes; the argument is the refreshed timer catalog.
	/// </summary>
	event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
	/// <summary>
	/// Occurs with an RF address and per-zone plan summaries after plans are refreshed.
	/// </summary>
	event Action<int, string[]> PlansChanged;
	/// <summary>
	/// Occurs with RF address, zone, latest usage record and read-success flag after a history request.
	/// </summary>
	event Action<int, int, RecordedUsage, bool> HistoryChanged;
	/// <summary>
	/// Queues a coalesced history read, optionally allowing bounded retries after watering completion.
	/// </summary>
	/// <param name="address">The timer RF address within the configured hub.</param>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <param name="completion">Whether this read follows watering completion and may use bounded delayed retries.</param>
	void RequestHistory (int address, int zone, bool completion);
	/// <summary>
	/// Occurs when a new timer observation is ready for presentation.
	/// </summary>
	event Action<TimerReading> Reading;
	/// <summary>
	/// Occurs when cloud or MQTT connection state changes.
	/// </summary>
	event Action<string> ConnectionState;
	/// <summary>
	/// Signs in, selects the configured home and hub, and starts observing reported state.
	/// </summary>
	/// <param name="settings">The validated account and resource-selection settings.</param>
	/// <param name="token">Cancellation for the operation.</param>
	/// <returns>A task containing the discovered supported timers.</returns>
	Task<IReadOnlyList<TimerIdentity>> ConnectAsync (DriverSettings settings, CancellationToken token);
	/// <summary>
	/// Submits one timed-watering request without replaying it after a connection failure.
	/// </summary>
	/// <param name="address">The timer RF address within the configured hub.</param>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <param name="minutes">The watering duration in whole minutes, from 1 through 120.</param>
	/// <param name="token">Cancellation for the operation.</param>
	/// <returns>A task that completes when the operation finishes; command completion is not proof of valve actuation.</returns>
	Task StartAsync (int address, int zone, int minutes, CancellationToken token);
	/// <summary>
	/// Submits one zone-stop request without replaying it after a connection failure.
	/// </summary>
	/// <param name="address">The timer RF address within the configured hub.</param>
	/// <param name="zone">The one-based zone number on the selected timer.</param>
	/// <param name="token">Cancellation for the operation.</param>
	/// <returns>A task that completes when the operation finishes; command completion is not proof of valve actuation.</returns>
	Task StopAsync (int address, int zone, CancellationToken token);
	/// <summary>
	/// Reads fresh hub and timer state without issuing a watering command.
	/// </summary>
	/// <param name="token">Cancellation for the operation.</param>
	/// <returns>A task that completes when the operation finishes.</returns>
	Task RefreshAsync (CancellationToken token);
	/// <summary>
	/// Reads the saved plans for each actual zone on the selected timer.
	/// </summary>
	/// <param name="address">The timer RF address within the configured hub.</param>
	/// <param name="token">Cancellation for the operation.</param>
	/// <returns>A task containing plan summaries in zone order.</returns>
	Task<string[]> ReadPlansAsync (int address, CancellationToken token);
	/// <summary>
	/// Cancels background observation and waits for connection resources to close.
	/// </summary>
	/// <returns>A task that completes when the operation finishes.</returns>
	Task CloseAsync ();
	}