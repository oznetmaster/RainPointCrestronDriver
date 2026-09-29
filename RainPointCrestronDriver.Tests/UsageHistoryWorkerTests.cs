// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Tests;

[TestFixture]
public sealed class UsageHistoryWorkerTests
	{
	[Test]
	public async Task CompletionRetriesAreBoundedAndDoNotMixRecords ()
		{
		int reads = 0, waits = 0;
		var results = new List<string> ();
		var finished = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var worker = new UsageHistoryWorker ((_, _, _) =>
			{
				reads++;
				if (reads == 2)
					throw new HttpRequestException ("History delayed");
				return Task.FromResult (new RecordedUsage (reads.ToString (), DateTimeOffset.UtcNow, reads));
			}, (_, _, row, success) =>
			{
				results.Add (success ? row.Id : "failed");
				if (results.Count == 3)
					finished.TrySetResult (true);
			}, CancellationToken.None, (_, _) => { waits++; return Task.CompletedTask; });
		try
			{
			worker.Request (2, 1, true);
			Assert.That (await Task.WhenAny (finished.Task, Task.Delay (5000)), Is.SameAs (finished.Task));
			}
		finally { await worker.StopAsync (); }
		Assert.That (results, Is.EqualTo (new[] { "1", "failed", "3" }));
		Assert.That (reads, Is.EqualTo (3));
		Assert.That (waits, Is.EqualTo (2));
		}
	[Test]
	public async Task DuplicateRequestsCoalesceAndShutdownCancelsInFlightRead ()
		{
		int reads = 0, published = 0;
		var started = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var worker = new UsageHistoryWorker (async (_, _, token) =>
			{
				Interlocked.Increment (ref reads);
				started.TrySetResult (true);
				await Task.Delay (Timeout.Infinite, token);
				return null;
			}, (_, _, _, _) => published++, CancellationToken.None);
		try
			{
			worker.Request (2, 1, false);
			Assert.That (await Task.WhenAny (started.Task, Task.Delay (5000)), Is.SameAs (started.Task));
			for (int i = 0; i < 20; i++)
				worker.Request (2, 1, false);
			worker.Request (2, 2, false);
			}
		finally { await worker.StopAsync (); }
		worker.Request (2, 3, true);
		Assert.That (reads, Is.EqualTo (1), "Reads are serialized, not sent concurrently for every UI notification.");
		Assert.That (published, Is.Zero);
		await worker.StopAsync ();
		}
	[Test]
	public async Task ShutdownInterruptsTheCompletionRetryDelay ()
		{
		int reads = 0;
		var waiting = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var worker = new UsageHistoryWorker ((_, _, _) => { reads++; return Task.FromResult<RecordedUsage> (null); },
			(_, _, _, _) => { }, CancellationToken.None,
			(_, token) => { waiting.TrySetResult (true); return Task.Delay (Timeout.Infinite, token); });
		try
			{
			worker.Request (2, 1, true);
			Assert.That (await Task.WhenAny (waiting.Task, Task.Delay (5000)), Is.SameAs (waiting.Task));
			}
		finally { await worker.StopAsync (); }
		Assert.That (reads, Is.EqualTo (1));
		}
	[Test]
	public void DateLabelsPreserveHomeTimeAndIdentifyCloudFallback ()
		{
		var cloud = new DateTimeOffset (2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
		Assert.That (new RecordedUsage ("1", cloud, 0, new DateTime (2026, 9, 29, 9, 0, 0), "GMT+01:00").TimeLabel,
			Is.EqualTo ("29 Sep 2026 09:00:00 (GMT+01:00)"));
		Assert.That (new RecordedUsage ("2", cloud, 1.4m).TimeLabel, Is.EqualTo ("29 Sep 2026 08:00:00 UTC (cloud record)"));
		}
	}