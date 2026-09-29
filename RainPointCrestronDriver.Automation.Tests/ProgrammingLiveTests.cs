// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using CrestronHomeDevTools;

using NUnit.Framework;

namespace RainPointCrestronDriver.Automation.Tests;

[SupportedOSPlatform ("windows")]
[TestFixture, NonParallelizable, Category ("InstalledAutomation")]
public sealed partial class ProgrammingLiveTests
	{
	[Test, Explicit ("Requires private installed-driver bindings; reads Home programming metadata only.")]
	public async Task InspectInstalledProgrammingCatalog ()
		{
		string settingsPath = Environment.GetEnvironmentVariable ("RAINPOINT_HOME_TEST_SETTINGS");
		Assert.That (settingsPath, Is.Not.Null.And.Not.Empty);
		Settings settings = JsonSerializer.Deserialize<Settings> (File.ReadAllText (settingsPath));
		var binding = DevToolsCredentialBindings.Read (settings.BindingsPath);
		var saved = DevToolsPrivateStore.Open (binding.StoreDirectory).LoadCredential (settings.CredentialAlias, DevToolsCredentialPurpose.Processor, settings.Host);
		var credential = new NetworkCredential (saved.UserName, saved.Password);
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (90));
		CancellationToken token = timeout.Token;
		await using var management = await ConfigurationClient.ConnectAsync (new ProcessorConnectionOptions { Host = saved.Host, CertificateSha256 = saved.CertificateSha256 }, credential, token);
		var device = await management.GetDeviceAsync (settings.DeviceId, token);
		Assert.That (device, Is.Not.Null);
		Assert.That (device.ParentDeviceId, Is.EqualTo (settings.ParentDeviceId));
		Assert.That (device.LocationId, Is.EqualTo (settings.RoomId));
		Assert.That (device.Model, Is.EqualTo ("HTV345FRF"));
		Assert.That (device.PropertyValues["cp.driverInformation:version"].GetString (), Is.EqualTo (settings.Version));
		await using var api = await ProgrammingApi.ConnectAsync (saved.Host, saved.CertificateSha256, credential, token);
		JsonElement entities = await api.Command ("cp.programming:getProgrammableEntitiesWithEvents", new
			{
			locationId = settings.RoomId
			}, token);
		Save ("entities.json", entities);
		JsonElement entity = entities.EnumerateArray ().Single (e => e.GetProperty ("Id").GetInt32 () == settings.DeviceId);
		JsonElement events = await api.Command ("cp.programming:getProgrammableEvents", new
			{
			programmableEntities = new[] { entity }
			}, token);
		Save ("events.json", events);
		JsonElement operations = await api.Command ("cp.programming:getSequenceOperations", new
			{
			programmableEntity = entity,
			operationType = "Actions"
			}, token);
		Save ("operations.json", operations);
		Assert.That (operations.ValueKind, Is.EqualTo (JsonValueKind.Array));
		JsonElement feedbacks = await api.Command ("cp.programming:getSequenceOperations", new
			{
			programmableEntity = entity,
			operationType = "Feedbacks"
			}, token);
		Save ("feedbacks.json", feedbacks);
		JsonElement eventInfo = events[0].GetProperty ("Events").EnumerateArray ().Single (e => e.GetProperty ("EventSource").GetString () == "zone1CommandChanged");
		Save ("modes.json", await api.Command ("cp.programming:getAvailableModes", new
			{
			programmableEntity = entity,
			eventInfo
			}, token));
		Save ("current-mode.json", await api.Command ("cp.programming:getMode", new
			{
			programmableEntity = entity,
			eventInfo
			}, token));
		TestContext.Out.WriteLine ("Installed timer identity and programming catalog responses verified; no programming or valve commands were changed.");
		}

	[Test, Explicit ("Temporarily programs an unused zone-1 property event to stop the already-idle zone; restores programming and duration.")]
	public async Task HomePropertyEventExecutesConditionalStopAndRestores ()
		{
		string settingsPath = Environment.GetEnvironmentVariable ("RAINPOINT_HOME_TEST_SETTINGS");
		Assert.That (settingsPath, Is.Not.Null.And.Not.Empty);
		Settings settings = JsonSerializer.Deserialize<Settings> (File.ReadAllText (settingsPath));
		var binding = DevToolsCredentialBindings.Read (settings.BindingsPath);
		var saved = DevToolsPrivateStore.Open (binding.StoreDirectory).LoadCredential (settings.CredentialAlias, DevToolsCredentialPurpose.Processor, settings.Host);
		var credential = new NetworkCredential (saved.UserName, saved.Password);
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (180));
		CancellationToken token = timeout.Token;
		await using var management = await ConfigurationClient.ConnectAsync (new ProcessorConnectionOptions { Host = saved.Host, CertificateSha256 = saved.CertificateSha256 }, credential, token);
		var device = await management.GetDeviceAsync (settings.DeviceId, token);
		Assert.That (device, Is.Not.Null);
		Assert.That (device.ParentDeviceId, Is.EqualTo (settings.ParentDeviceId));
		Assert.That (device.LocationId, Is.EqualTo (settings.RoomId));
		Assert.That (device.Model, Is.EqualTo ("HTV345FRF"));
		Assert.That (device.PropertyValues["cp.driverInformation:version"].GetString (), Is.EqualTo (settings.Version));
		await using var api = await ProgrammingApi.ConnectAsync (saved.Host, saved.CertificateSha256, credential, token);

		Assert.That (settings.AllowProgrammingChanges, Is.True);
		string owner = Guid.NewGuid ().ToString ("N");
		string journal = Path.Combine (TestContext.CurrentContext.WorkDirectory, "sequence-journal.jsonl");
		void Record (string phase, object data) => File.AppendAllText (journal, JsonSerializer.Serialize (new
			{
			Utc = DateTimeOffset.UtcNow,
			phase,
			data
			}) + Environment.NewLine);
		Record ("lease-intent", new
			{
			owner,
			settings.DeviceId,
			settings.Host
			});
		using var lease = await ProcessorOperationLease.AcquireAsync (saved.Host, credential, saved.SshFingerprint, owner, token);
		int blockId = 0, ifStepId = 0, originalMinutes = 0;
		bool modeAttempted = false, stepAttempted = false, durationAttempted = false;
		JsonElement entity = default, eventInfo = default, originalMode = default;
		try
			{
			DateTimeOffset readyDeadline = DateTimeOffset.UtcNow.AddSeconds (60);
			do
				{
				device = await management.GetDeviceAsync (settings.DeviceId, token);
				Record ("readiness", new
					{
					online = device.PropertyValues["onlineIndicator:isOnline"].GetBoolean (),
					connection = device.PropertyValues["connection"].GetString ()
					});
				if (device.PropertyValues["onlineIndicator:isOnline"].GetBoolean ())
					break;
				await Task.Delay (1000, token);
				} while (DateTimeOffset.UtcNow < readyDeadline);
			Assert.That (device.PropertyValues["onlineIndicator:isOnline"].GetBoolean (), Is.True);
			Assert.That (device.PropertyValues["activeZoneCount"].GetInt32 (), Is.Zero);
			Assert.That (device.PropertyValues["zone1CanStart"].GetBoolean (), Is.True);
			originalMinutes = device.PropertyValues["zone1Minutes"].GetInt32 ();
			string originalCommand = device.PropertyValues["zone1Command"].GetString ();
			string[] otherCommands = new[] { device.PropertyValues["zone2Command"].GetString (), device.PropertyValues["zone3Command"].GetString () };
			int[] otherMinutes = new[] { device.PropertyValues["zone2Minutes"].GetInt32 (), device.PropertyValues["zone3Minutes"].GetInt32 () };
			JsonElement entities = await api.Command ("cp.programming:getProgrammableEntitiesWithEvents", new
				{
				locationId = settings.RoomId
				}, token);
			entity = entities.EnumerateArray ().Single (e => e.GetProperty ("Id").GetInt32 () == settings.DeviceId);
			JsonElement events = await api.Command ("cp.programming:getProgrammableEvents", new
				{
				programmableEntities = new[] { entity }
				}, token);
			eventInfo = events[0].GetProperty ("Events").EnumerateArray ().Single (e => e.GetProperty ("EventSource").GetString () == "zone1MinutesChanged");
			originalMode = await api.Command ("cp.programming:getMode", new
				{
				programmableEntity = entity,
				eventInfo
				}, token);
			Assert.That (originalMode.GetProperty ("Type").GetString (), Is.EqualTo ("None"), "Never overwrite existing user programming.");
			JsonElement modes = await api.Command ("cp.programming:getAvailableModes", new
				{
				programmableEntity = entity,
				eventInfo
				}, token);
			JsonElement sequenceMode = modes.EnumerateArray ().Single (m => m.GetProperty ("Type").GetString () == "Sequence");
			Record ("before", new
				{
				entity,
				eventInfo,
				originalMode,
				originalMinutes,
				originalCommand,
				otherCommands,
				otherMinutes
				});
			modeAttempted = true;
			Record ("set-mode-intent", new
				{
				mode = sequenceMode
				});
			await api.Command ("cp.programming:setMode", new
				{
				programmableEntity = entity,
				eventInfo,
				mode = sequenceMode
				}, token);
			JsonElement triggers = await api.Command ("cp.programming:getModeTriggers", new
				{
				programmableEntity = entity,
				eventInfo
				}, token);
			Record ("triggers", triggers);
			JsonElement trigger = triggers.EnumerateArray ().Single ();
			JsonElement block = await api.Command ("cp.programming:getSequenceProgrammingBlock", new
				{
				programmableEntity = entity,
				eventInfo,
				trigger
				}, token);
			blockId = block.GetProperty ("Id").GetInt32 ();
			Assert.That (blockId, Is.GreaterThan (0));
			Assert.That (block.GetProperty ("Steps").GetArrayLength (), Is.Zero);
			stepAttempted = true;
			Record ("create-if-intent", new
				{
				blockId
				});
			ifStepId = (await api.Command ("cp.programming:createSequenceIfStep", new
				{
				programmingBlockId = blockId,
				position = 0
				}, token)).GetInt32 ();
			Record ("if-created", new
				{
				blockId,
				ifStepId
				});
			int conditionId = (await api.Command ("cp.programming:initializeSequenceIfStepTopLevelCondition", new
				{
				ifStepId,
				compare = "Equal"
				}, token)).GetInt32 ();
			await api.Command ("cp.programming:addSequenceConditionReferenceOperand", new
				{
				conditionId,
				compareElement = "Left",
				valueType = "String",
				entity,
				operationId = "zone1State",
				entityComponentId = (string)null,
				units = "None",
				upperLimit = "NaN",
				lowerLimit = "NaN"
				}, token);
			await api.Command ("cp.programming:addSequenceConditionLiteralOperand", new
				{
				conditionId,
				compareElement = "Right",
				valueType = "String",
				value = "Idle",
				valueFormatLocale = "en-US",
				units = "None",
				upperLimit = "NaN",
				lowerLimit = "NaN"
				}, token);
			block = await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
				{
				programmingBlockId = blockId
				}, token);
			Record ("conditional-block", block);
			JsonElement ifStep = block.GetProperty ("Steps").EnumerateArray ().Single (step => step.GetProperty ("Id").GetInt32 () == ifStepId);
			int trueBlockId = ifStep.GetProperty ("TrueLogic").GetProperty ("Id").GetInt32 ();
			int commandId = (await api.Command ("cp.programming:createSequenceCommandStep", new
				{
				programmingBlockId = trueBlockId,
				position = 0,
				programmableEntity = entity,
				operationId = "stopZone1",
				operationArguments = Array.Empty<object> ()
				}, token)).GetInt32 ();
			Record ("stop-step-created", new
				{
				trueBlockId,
				commandId
				});
			Save ("test-sequence.json", await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
				{
				programmingBlockId = blockId
				}, token));
			device = await management.GetDeviceAsync (settings.DeviceId, token);
			Assert.That (device.PropertyValues["activeZoneCount"].GetInt32 (), Is.Zero);
			Assert.That (device.PropertyValues["zone1Minutes"].GetInt32 (), Is.EqualTo (originalMinutes));
			Assert.That (device.PropertyValues["zone1Command"].GetString (), Is.EqualTo (originalCommand));
			durationAttempted = true;
			Record ("trigger-intent", new
				{
				originalMinutes,
				temporaryMinutes = originalMinutes == 1 ? 2 : 1
				});
			var triggerReceipt = await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
				{
				property = "zone1Minutes",
				value = originalMinutes == 1 ? "2" : "1"
				}, token);
			Record ("trigger-receipt", triggerReceipt);
			bool executed = false;
			DateTimeOffset propertyDeadline = DateTimeOffset.UtcNow.AddSeconds (10);
			do
				{
				device = await management.GetDeviceAsync (settings.DeviceId, token);
				string observedCommand = device.PropertyValues["zone1Command"].GetString ();
				Record ("trigger-observation", new
					{
					observedCommand,
					minutes = device.PropertyValues["zone1Minutes"].GetInt32 ()
					});
				if (observedCommand == "Sending stop" || observedCommand.StartsWith ("Accepted", StringComparison.Ordinal))
					executed = true;
				if (device.PropertyValues["zone1Minutes"].GetInt32 () == (originalMinutes == 1 ? 2 : 1))
					break;
				await Task.Delay (250, token);
				} while (DateTimeOffset.UtcNow < propertyDeadline);
			Assert.That (device.PropertyValues["zone1Minutes"].GetInt32 (), Is.EqualTo (originalMinutes == 1 ? 2 : 1), "The UI duration command must actually change its property.");
			DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds (50);
			while (!executed && DateTimeOffset.UtcNow < deadline)
				{
				device = await management.GetDeviceAsync (settings.DeviceId, token);
				string command = device.PropertyValues["zone1Command"].GetString ();
				Record ("observation", new
					{
					command,
					state = device.PropertyValues["zone1State"].GetString ()
					});
				Assert.That (device.PropertyValues["activeZoneCount"].GetInt32 (), Is.Zero);
				Assert.That (device.PropertyValues["zone2Command"].GetString (), Is.EqualTo (otherCommands[0]));
				Assert.That (device.PropertyValues["zone3Command"].GetString (), Is.EqualTo (otherCommands[1]));
				if (command == "Sending stop" || command.StartsWith ("Accepted", StringComparison.Ordinal) || (command != originalCommand && command == "New report: Idle"))
					{
					executed = true;
					break;
					}
				await Task.Delay (500, token);
				}
			Assert.That (executed, Is.True, "Home did not execute the conditional Stop action after the property event.");
			Record ("execution-confirmed", new
				{
				blockId,
				ifStepId,
				conditionId,
				commandId
				});
			}
		finally
			{
			using var cleanup = new CancellationTokenSource (TimeSpan.FromSeconds (120));
			CancellationToken clean = cleanup.Token;
			if (stepAttempted)
				{
				Assert.That (ifStepId, Is.GreaterThan (0), "Uncertain step creation: reservation retained for inspection.");
				JsonElement block = await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
					{
					programmingBlockId = blockId
					}, clean);
				Assert.That (block.GetProperty ("Steps").EnumerateArray ().Select (x => x.GetProperty ("Id").GetInt32 ()).ToArray (), Is.EqualTo (new[] { ifStepId }), "Unexpected programming: reservation retained.");
				Record ("remove-step-intent", new
					{
					blockId,
					ifStepId
					});
				await api.Command ("cp.programming:deleteSequenceStep", new
					{
					programmingBlockId = blockId,
					stepId = ifStepId
					}, clean);
				block = await api.Command ("cp.programming:getSequenceProgrammingBlockById", new
					{
					programmingBlockId = blockId
					}, clean);
				Assert.That (block.GetProperty ("Steps").GetArrayLength (), Is.Zero);
				}
			if (modeAttempted)
				{
				Record ("restore-mode-intent", new
					{
					originalMode
					});
				await api.Command ("cp.programming:setMode", new
					{
					programmableEntity = entity,
					eventInfo,
					mode = originalMode
					}, clean);
				JsonElement mode = await api.Command ("cp.programming:getMode", new
					{
					programmableEntity = entity,
					eventInfo
					}, clean);
				Assert.That (mode.GetProperty ("Type").GetString (), Is.EqualTo ("None"));
				}
			if (durationAttempted)
				{
				// Wait for command confirmation/timeout to unlock the UI duration before restoration.
				do
					{
					device = await management.GetDeviceAsync (settings.DeviceId, clean);
					if (device.PropertyValues["zone1CanStart"].GetBoolean ())
						break;
					await Task.Delay (1000, clean);
					} while (true);
				Record ("restore-duration-intent", new
					{
					originalMinutes
					});
				await management.ExecuteDeviceCommandAsync (settings.DeviceId, "extension:setPropertyValue", new
					{
					property = "zone1Minutes",
					value = originalMinutes.ToString (System.Globalization.CultureInfo.InvariantCulture)
					}, clean);
				DateTimeOffset restoreDeadline = DateTimeOffset.UtcNow.AddSeconds (10);
				do
					{
					device = await management.GetDeviceAsync (settings.DeviceId, clean);
					if (device.PropertyValues["zone1Minutes"].GetInt32 () == originalMinutes)
						break;
					await Task.Delay (250, clean);
					} while (DateTimeOffset.UtcNow < restoreDeadline);
				Assert.That (device.PropertyValues["zone1Minutes"].GetInt32 (), Is.EqualTo (originalMinutes));
				Assert.That (device.PropertyValues["activeZoneCount"].GetInt32 (), Is.Zero);
				}
			await lease.ReleaseAsync (clean);
			Record ("restored-and-released", new
				{
				owner
				});
			TestContext.AddTestAttachment (journal);
			}
		}

	private static void Save (string name, JsonElement value)
		{
		string path = Path.Combine (TestContext.CurrentContext.WorkDirectory, name);
		File.WriteAllText (path, value.GetRawText ());
		TestContext.AddTestAttachment (path);
		}
	private sealed class Settings
		{
		public bool AllowShortZone1Run
			{
			get; set;
			}
		public bool AllowProgrammingChanges
			{
			get; set;
			}
		public string BindingsPath
			{
			get; set;
			}
		public string CredentialAlias
			{
			get; set;
			}
		public string Host
			{
			get; set;
			}
		public int DeviceId
			{
			get; set;
			}
		public int ParentDeviceId
			{
			get; set;
			}
		public int RoomId
			{
			get; set;
			}
		public string Version
			{
			get; set;
			}
		}
	}