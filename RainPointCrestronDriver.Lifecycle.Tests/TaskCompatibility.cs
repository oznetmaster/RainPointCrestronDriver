// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.

#if NETFRAMEWORK
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RainPointCrestronDriver.Lifecycle.Tests;

internal static class TaskCompatibility
	{
	internal static async Task<T> WaitAsync<T> (this Task<T> task, TimeSpan timeout)
		{
		using var deadline = new CancellationTokenSource ();
		Task expired = Task.Delay (timeout, deadline.Token);
		if (await Task.WhenAny (task, expired).ConfigureAwait (false) != task)
			throw new TimeoutException ("Test operation did not finish before its deadline.");
		deadline.Cancel ();
		return await task.ConfigureAwait (false);
		}
	}
#endif