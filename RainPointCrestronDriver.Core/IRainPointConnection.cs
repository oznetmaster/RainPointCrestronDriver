// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RainPoint.CrestronDriver.Core;

public interface IRainPointConnection
	{
	event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
	event Action<int, string[]> PlansChanged;
	event Action<int, int, RecordedUsage, bool> HistoryChanged;
	void RequestHistory (int address, int zone, bool completion);
	event Action<TimerReading> Reading;
	event Action<string> ConnectionState;
	Task<IReadOnlyList<TimerIdentity>> ConnectAsync (DriverSettings settings, CancellationToken token);
	Task StartAsync (int address, int zone, int minutes, CancellationToken token);
	Task StopAsync (int address, int zone, CancellationToken token);
	Task RefreshAsync (CancellationToken token);
	Task<string[]> ReadPlansAsync (int address, CancellationToken token);
	Task CloseAsync ();
	}