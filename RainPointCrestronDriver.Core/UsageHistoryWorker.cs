// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using RainPointClient;

namespace RainPoint.CrestronDriver.Core;

/// <summary>Coalesces per-zone reads and bounds completion retries. Never runs a periodic poll or sends a watering command.</summary>
internal sealed class UsageHistoryWorker
	{
	private sealed class Job
		{
		internal int Remaining; internal Task Task;
		}
	private readonly object _sync = new ();
	private readonly Dictionary<string, Job> _jobs = [];
	private readonly SemaphoreSlim _reads = new (1, 1);
	private readonly CancellationTokenSource _stop;
	private readonly Func<int, int, CancellationToken, Task<RecordedUsage>> _read;
	private readonly Action<int, int, RecordedUsage, bool> _publish;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;
	private Task _closing;
	internal UsageHistoryWorker (Func<int, int, CancellationToken, Task<RecordedUsage>> read,
		Action<int, int, RecordedUsage, bool> publish, CancellationToken token,
		Func<TimeSpan, CancellationToken, Task> delay = null)
		{
		_read = read;
		_publish = publish;
		_delay = delay ?? Task.Delay;
		_stop = CancellationTokenSource.CreateLinkedTokenSource (token);
		}
	internal void Request (int address, int zone, bool completion)
		{
		lock (_sync)
			{
			if (_closing != null || _stop.IsCancellationRequested)
				return;
			string key = address + ":" + zone;
			if (_jobs.TryGetValue (key, out Job current))
				{
				if (completion)
					current.Remaining = Math.Max (current.Remaining, 3);
				return;
				}
			var job = new Job { Remaining = completion ? 3 : 1 };
			_jobs.Add (key, job);
			job.Task = Task.Run (() => RunAsync (key, address, zone, job));
			}
		}
	private async Task RunAsync (string key, int address, int zone, Job job)
		{
		CancellationToken token = _stop.Token;
		try
			{
			while (true)
				{
				RecordedUsage record = null;
				bool success = false;
				await _reads.WaitAsync (token).ConfigureAwait (false);
				try
					{
					record = await _read (address, zone, token).ConfigureAwait (false);
					success = true;
					}
				catch (Exception error) when (error is RainPointException or HttpRequestException or OperationCanceledException or InvalidOperationException) { }
				finally { _reads.Release (); }
				token.ThrowIfCancellationRequested ();
				_publish (address, zone, record, success);
				lock (_sync)
					{
					if (--job.Remaining == 0)
						{
						_jobs.Remove (key);
						return;
						}
					}
				await _delay (TimeSpan.FromSeconds (30), token).ConfigureAwait (false);
				}
			}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		finally { lock (_sync) { if (_jobs.TryGetValue (key, out var current) && ReferenceEquals (current, job)) _jobs.Remove (key); } }
		}
	internal Task StopAsync ()
		{
		lock (_sync)
			return _closing ??= StopCoreAsync ();
		}
	private async Task StopCoreAsync ()
		{
		_stop.Cancel ();
		Task[] jobs = _jobs.Values.Select (j => j.Task).ToArray ();
		try
			{
			await Task.WhenAll (jobs).ConfigureAwait (false);
			}
		finally { _stop.Dispose (); _reads.Dispose (); }
		}
	}