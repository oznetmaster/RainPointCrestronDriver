// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using CrestronHomeDevTools;

using NUnit.Framework;

namespace RainPointCrestronDriver.Automation.Tests;

public sealed partial class ProgrammingLiveTests
	{
	[Test, Explicit ("Starts zone 1 once for one minute; verifies automatic completion, a Home custom event and dated history; restores temporary programming.")]
	public async Task OneMinuteZone1CompletesWithCustomEventAndDatedUsage ()
		{
		Settings settings = JsonSerializer.Deserialize<Settings> (File.ReadAllText (Environment.GetEnvironmentVariable ("RAINPOINT_HOME_TEST_SETTINGS")));
		Assert.That (settings.AllowShortZone1Run && settings.AllowProgrammingChanges, Is.True);
		var binding = DevToolsCredentialBindings.Read (settings.BindingsPath);
		var saved = DevToolsPrivateStore.Open (binding.StoreDirectory).LoadCredential (settings.CredentialAlias, DevToolsCredentialPurpose.Processor, settings.Host);
		var credential = new NetworkCredential (saved.UserName, saved.Password);
		using var deadline = new CancellationTokenSource (TimeSpan.FromMinutes (5));
		CancellationToken token = deadline.Token;
		await using var management = await ConfigurationClient.ConnectAsync (new ProcessorConnectionOptions { Host = saved.Host, CertificateSha256 = saved.CertificateSha256 }, credential, token);
		await using var api = await ProgrammingApi.ConnectAsync (saved.Host, saved.CertificateSha256, credential, token);
		using var lease = await ProcessorOperationLease.AcquireAsync (saved.Host, credential, saved.SshFingerprint, Guid.NewGuid ().ToString ("N"), token);
		var journal = new List<object> ();
		JsonElement entity = default, eventInfo = default, originalMode = default;
		int blockId = 0, stepId = 0, startBlockId = 0, startStepId = 0, originalMinutes1 = 0, originalMinutes2 = 0;
		JsonElement startEvent = default, startOriginalMode = default;
		bool startModeAttempted = false, startStepAttempted = false, durationChanged = false;
		bool modeAttempted = false, stepAttempted = false, startAttempted = false;
		try
			{
			var platform = await management.GetDeviceAsync (settings.ParentDeviceId, token);
			var items = platform.PropertyValues["cp.driverConfiguration:configurationItems"];
			Assert.That (items.EnumerateArray ().Single (x => x.GetProperty ("Id").GetString () == "Email").GetProperty ("Value").GetProperty ("CurrentValue").GetString (), Is.EqualTo ("support@marvelous.com"));
			var device = await management.GetDeviceAsync (settings.DeviceId, token);
			Assert.That (device.ParentDeviceId, Is.EqualTo (settings.ParentDeviceId));
			Assert.That (device.LocationId, Is.EqualTo (settings.RoomId));
			Assert.That (device.Model, Is.EqualTo ("HTV345FRF"));
			Assert.That (device.PropertyValues["cp.driverInformation:version"].GetString (), Is.EqualTo (settings.Version));
			Assert.That (device.PropertyValues["connection"].GetString (), Is.EqualTo ("PushConnected"));
			Assert.That (device.PropertyValues["onlineIndicator:isOnline"].GetBoolean (), Is.True);
			Assert.That (device.PropertyValues["activeZoneCount"].GetInt32 (), Is.Zero);
			Assert.That (device.PropertyValues["zone1CanStart"].GetBoolean (), Is.True);
			originalMinutes1 = device.PropertyValues["zone1Minutes"].GetInt32 ();
			originalMinutes2 = device.PropertyValues["zone2Minutes"].GetInt32 ();
			string oldDate = device.PropertyValues["zone1UsageWhen"].GetString ();
			string[] otherCommands = [device.PropertyValues["zone2Command"].GetString (), device.PropertyValues["zone3Command"].GetString ()];
			var entities = await api.Command ("cp.programming:getProgrammableEntitiesWithEvents", new
				{
				locationId = settings.RoomId
				}, token);
			entity = entities.EnumerateArray ().Single (e => e.GetProperty ("Id").GetInt32 () == settings.DeviceId);
			var events = await api.Command ("cp.programming:getProgrammableEvents", new
				{
				programmableEntities = new[] { entity }
				}, token);
			eventInfo = events[0].GetProperty ("Events").EnumerateArray ().Single (e => e.GetProperty ("EventSource").GetString () == "zone1Stopped");
			originalMode = await api.Command ("cp.programming:getMode", new
				{
				programmableEntity = entity,
				eventInfo
				}, token);
			Assert.That (originalMode.GetProperty ("Type").GetString (), Is.EqualTo ("None"), "Never overwrite user programming.");
			var modes = await api.Command ("cp.programming:getAvailableModes", new
				{
				programmableEntity = entity,
				eventInfo
				}, token);
			var mode = modes.EnumerateArray ().Single (m => m.GetProperty ("Type").GetString () == "Sequence");
			modeAttempted = true;
			await api.Command ("cp.programming:setMode", new
				{
				programmableEntity = entity,
				eventInfo,
				mode
				}, token);
			var trigger = (await api.Command ("cp.programming:getModeTriggers", new
				{
				programmableEntity = entity,
				eventInfo
				}, token)).EnumerateArray ().Single ();
			var block = await api.Command ("cp.programming:getSequenceProgrammingBlock", new
				{
				programmableEntity = entity,
				eventInfo,
				trigger
				}, token);
			blockId = block.GetProperty ("Id").GetInt32 ();
			Assert.That (block.GetProperty ("Steps").GetArrayLength (), Is.Zero);
			stepAttempted = true;
			// The stopped event invokes an idempotent stop after natural completion; it cannot start water.
			stepId = (await api.Command ("cp.programming:createSequenceCommandStep", new
				{
				programmingBlockId = blockId,
				position = 0,
				programmableEntity = entity,
				operationId = "stopZone1",
				operationArguments = Array.Empty<object> ()
				}, token)).GetInt32 ();
			journal.Add (new
				{
				Phase = "programmed",
				blockId,
				stepId,
				oldDate
				});
			startEvent = events[0].GetProperty ("Events").EnumerateArray ().Single (e => e.GetProperty ("EventSource").GetString () == "zone2MinutesChanged");
			startOriginalMode = await api.Command ("cp.programming:getMode", new
				{
				programmableEntity = entity,
				eventInfo = startEvent
				}, token);
			Assert.That (startOriginalMode.GetProperty ("Type").GetString (), Is.EqualTo ("None"));
			durationChanged = true;
			await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
				{
				property = "zone1Minutes",
				value = "1"
				}, token);
			await Task.Delay (500, token);
			device = await management.GetDeviceAsync (settings.DeviceId, token);
			Assert.That (device.PropertyValues["zone1Minutes"].GetInt32 (), Is.EqualTo (1));
			startModeAttempted = true;
			await api.Command ("cp.programming:setMode", new
				{
				programmableEntity = entity,
				eventInfo = startEvent,
				mode
				}, token);
			var startTrigger = (await api.Command ("cp.programming:getModeTriggers", new
				{
				programmableEntity = entity,
				eventInfo = startEvent
				}, token)).EnumerateArray ().Single ();
			var startBlock = await api.Command ("cp.programming:getSequenceProgrammingBlock", new
				{
				programmableEntity = entity,
				eventInfo = startEvent,
				trigger = startTrigger
				}, token);
			startBlockId = startBlock.GetProperty ("Id").GetInt32 ();
			Assert.That (startBlock.GetProperty ("Steps").GetArrayLength (), Is.Zero);
			startStepAttempted = true;
			startStepId = (await api.Command ("cp.programming:createSequenceCommandStep", new
				{
				programmingBlockId = startBlockId,
				position = 0,
				programmableEntity = entity,
				operationId = "startZone1",
				operationArguments = Array.Empty<object> ()
				}, token)).GetInt32 ();
			journal.Add (new
				{
				Phase = "start-sequence",
				startBlockId,
				startStepId,
				originalMinutes1,
				originalMinutes2
				});
			startAttempted = true;
			journal.Add (new
				{
				Phase = "one-start-intent",
				Utc = DateTimeOffset.UtcNow,
				Minutes = 1
				});
			await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
				{
				property = "zone2Minutes",
				value = originalMinutes2 == 1 ? "2" : "1"
				}, token);
			// Remove the single-use start step before restoration or any further property change.
			await Task.Delay (1000, token);
			await api.Command ("cp.programming:deleteSequenceStep", new
				{
				programmingBlockId = startBlockId,
				stepId = startStepId
				}, token);
			startStepId = 0;
			startStepAttempted = false;
			bool activeSeen = false, idleSeen = false, eventSeen = false, countdownSeen = false, historySeen = false;
			int largestCountdown = -1;
			DateTimeOffset until = DateTimeOffset.UtcNow.AddSeconds (190);
			while (DateTimeOffset.UtcNow < until)
				{
				device = await management.GetDeviceAsync (settings.DeviceId, token);
				var p = device.PropertyValues;
				string state = p["zone1State"].GetString (), command = p["zone1Command"].GetString ();
				int remaining = p["zone1RemainingSeconds"].GetInt32 ();
				journal.Add (new
					{
					Utc = DateTimeOffset.UtcNow,
					state,
					command,
					remaining,
					Button = p["zone1ActionLabel"].GetString (),
					Usage = p["zone1Usage"].GetString (),
					Date = p["zone1UsageWhen"].GetString (),
					Connection = p["connection"].GetString ()
					});
				Assert.That (p["zone2State"].GetString (), Is.EqualTo ("Idle"));
				Assert.That (p["zone3State"].GetString (), Is.EqualTo ("Idle"));
				Assert.That (p["zone2Command"].GetString (), Is.EqualTo (otherCommands[0]));
				Assert.That (p["zone3Command"].GetString (), Is.EqualTo (otherCommands[1]));
				if (state == "Active")
					{
					activeSeen = true;
					Assert.That (p["zone1CanStart"].GetBoolean (), Is.False);
					Assert.That (p["zone1ActionLabel"].GetString (), Is.EqualTo ("Stop this zone"));
					if (remaining > 0 && largestCountdown > remaining)
						countdownSeen = true;
					largestCountdown = Math.Max (remaining, largestCountdown);
					}
				if (activeSeen && command == "Sending stop")
					eventSeen = true;
				if (activeSeen && state == "Idle")
					idleSeen = true;
				if (idleSeen && p["zone1UsageWhen"].GetString () != oldDate && p["zone1HistoryStatus"].GetString () == "")
					historySeen = true;
				if (idleSeen && eventSeen && countdownSeen && historySeen && p["zone1CanStart"].GetBoolean ())
					break;
				await Task.Delay (250, token);
				}
			Assert.That (activeSeen, Is.True, "No active device report was observed.");
			Assert.That (idleSeen, Is.True, "No natural completion report was observed.");
			Assert.That (countdownSeen, Is.True, "The device deadline did not produce a decreasing countdown.");
			Assert.That (eventSeen, Is.True, "The custom stopped event was not observed executing Home's stop sequence.");
			Assert.That (historySeen, Is.True, "No newly dated usage arrived within the observation window.");
			Assert.That (device.PropertyValues["zone1ActionLabel"].GetString (), Is.EqualTo ("Start timed watering"));
			}
		finally
			{
			using var cleanup = new CancellationTokenSource (TimeSpan.FromSeconds (120));
			var clean = cleanup.Token;
			Save ("one-minute-journal.json", JsonSerializer.SerializeToElement (journal));
			if (startStepAttempted)
				{
				Assert.That (startStepId, Is.GreaterThan (0));
				await api.Command ("cp.programming:deleteSequenceStep", new
					{
					programmingBlockId = startBlockId,
					stepId = startStepId
					}, clean);
				}
			if (startModeAttempted)
				{
				var block = await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
					{
					programmingBlockId = startBlockId
					}, clean);
				Assert.That (block.GetProperty ("Steps").GetArrayLength (), Is.Zero);
				if (startAttempted)
					{
					var current = await management.GetDeviceAsync (settings.DeviceId, clean);
					if (current.PropertyValues["zone1State"].GetString () != "Idle")
						{
						int stopStep = (await api.Command ("cp.programming:createSequenceCommandStep", new
							{
							programmingBlockId = startBlockId,
							position = 0,
							programmableEntity = entity,
							operationId = "stopZone1",
							operationArguments = Array.Empty<object> ()
							}, clean)).GetInt32 ();
						await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
							{
							property = "zone2Minutes",
							value = originalMinutes2.ToString (System.Globalization.CultureInfo.InvariantCulture)
							}, clean);
						await Task.Delay (1000, clean);
						await api.Command ("cp.programming:deleteSequenceStep", new
							{
							programmingBlockId = startBlockId,
							stepId = stopStep
							}, clean);
						}
					}
				await api.Command ("cp.programming:setMode", new
					{
					programmableEntity = entity,
					eventInfo = startEvent,
					mode = startOriginalMode
					}, clean);
				var restored = await api.Command ("cp.programming:getMode", new
					{
					programmableEntity = entity,
					eventInfo = startEvent
					}, clean);
				Assert.That (restored.GetProperty ("Type").GetString (), Is.EqualTo ("None"));
				}
			if (stepAttempted)
				{
				Assert.That (stepId, Is.GreaterThan (0), "Uncertain programming retained for inspection.");
				var block = await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
					{
					programmingBlockId = blockId
					}, clean);
				Assert.That (block.GetProperty ("Steps").EnumerateArray ().Select (x => x.GetProperty ("Id").GetInt32 ()).ToArray (), Is.EqualTo (new[] { stepId }));
				await api.Command ("cp.programming:deleteSequenceStep", new
					{
					programmingBlockId = blockId,
					stepId
					}, clean);
				block = await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
					{
					programmingBlockId = blockId
					}, clean);
				Assert.That (block.GetProperty ("Steps").GetArrayLength (), Is.Zero);
				}
			if (modeAttempted)
				{
				await api.Command ("cp.programming:setMode", new
					{
					programmableEntity = entity,
					eventInfo,
					mode = originalMode
					}, clean);
				var mode = await api.Command ("cp.programming:getMode", new
					{
					programmableEntity = entity,
					eventInfo
					}, clean);
				Assert.That (mode.GetProperty ("Type").GetString (), Is.EqualTo ("None"));
				}
			if (startAttempted)
				{
				var final = await management.GetDeviceAsync (settings.DeviceId, clean);
				do
					{
					final = await management.GetDeviceAsync (settings.DeviceId, clean);
					if (final.PropertyValues["activeZoneCount"].GetInt32 () == 0 && final.PropertyValues["zone1CanStart"].GetBoolean ())
						break;
					await Task.Delay (1000, clean);
					} while (true);
				}
			if (durationChanged)
				{
				await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
					{
					property = "zone1Minutes",
					value = originalMinutes1.ToString (System.Globalization.CultureInfo.InvariantCulture)
					}, clean);
				await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
					{
					property = "zone2Minutes",
					value = originalMinutes2.ToString (System.Globalization.CultureInfo.InvariantCulture)
					}, clean);
				await Task.Delay (500, clean);
				var restored = await management.GetDeviceAsync (settings.DeviceId, clean);
				Assert.That (restored.PropertyValues["zone1Minutes"].GetInt32 (), Is.EqualTo (originalMinutes1));
				Assert.That (restored.PropertyValues["zone2Minutes"].GetInt32 (), Is.EqualTo (originalMinutes2));
				}
			await lease.ReleaseAsync (clean);
			TestContext.Out.WriteLine ("Temporary programming removed and processor reservation released.");
			}
		}
	}