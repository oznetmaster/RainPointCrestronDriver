// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;

using NUnit.Framework;

using RainPoint.CrestronDriver;
using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Lifecycle.Tests;

[TestFixture]
public sealed partial class ExtensionTests
	{
	private DriverLogger _logger;
	private IrrigationController _controller;
	private DriverControllerCreationArgs _args;
	private DriverImplementationResources _resources;
	private RainPointTimerEntity _timer;
	private static string Data => Path.Combine (TestContext.CurrentContext.TestDirectory, "DriverTestData");
	[SetUp]
	public void SetUp ()
		{
		_logger = new DriverLogger ("rainpoint-offline");
		_controller = new IrrigationController (() => new FakeConnection ());
		_args = new DriverControllerCreationArgs ("rainpoint-offline", Data, _logger.AppLogger, null);
		_resources = new DriverImplementationResources
			{
			Logger = _logger,
			InitLogger = _logger.GetComponentLogger ("test", "driver"),
			DriverDefinition = Serialization.DefinitionFromJsonString (File.ReadAllText (Path.Combine (Data, "DriverDefinition.json"))),
			Conditions = new Dictionary<string, ICondition> (),
			Transformations = new Dictionary<string, ITransformation> (),
			TransportConfigItems = new Dictionary<string, IList<ConfigurationItemDefinition>> ()
			};
		_timer = new RainPointTimerEntity (new TimerIdentity (1, 2, "Garden timer"), _controller, _args, _resources);
		}
	[TearDown]
	public async Task TearDown ()
		{
		_timer?.Dispose ();
		if (_controller != null)
			{
			await _controller.ShutdownAsync ();
			}
		_logger?.Dispose ();
		}
	[Test]
	public void XmlPassesCrestronExtensionSchema ()
		{
		var errors = new List<string> ();
		var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, XmlResolver = null };
		settings.Schemas.Add (null, Path.Combine (TestContext.CurrentContext.TestDirectory, "ExtensionsSchemaDefinition.xsd"));
		settings.ValidationEventHandler += (_, e) => errors.Add (e.Message);
		using var reader = XmlReader.Create (Path.Combine (Data, "UiDefinitions", "UiDefinition.xml"), settings);
		while (reader.Read ())
			{
			}
		Assert.That (errors, Is.Empty);
		}
	[Test]
	public void AllBindingsResolveToSdkPropertiesCommandsAndPages ()
		{
		XDocument xml = XDocument.Load (Path.Combine (Data, "UiDefinitions", "UiDefinition.xml"));
		var state = _timer.GetState ();
		string[] layouts = xml.Descendants ("layout").Select (x => (string)x.Attribute ("id")).ToArray ();
		var labels = JsonSerializer.Deserialize<Dictionary<string, string>> (File.ReadAllText (Path.Combine (Data, "Translations", "en-US.json")));
		foreach (XAttribute attribute in xml.Descendants ().Attributes ())
			{
			foreach (Match match in Regex.Matches (attribute.Value, @"\{([^}]+)\}"))
				{
				Assert.That (state.Definition.Properties.ContainsKey (match.Groups[1].Value), Is.True, attribute.ToString ());
				}
			if (attribute.Value.StartsWith ("command:", StringComparison.Ordinal))
				{
				Assert.That (state.Definition.Commands.ContainsKey (attribute.Value.Substring (8)), Is.True, attribute.ToString ());
				}
			if (attribute.Value.StartsWith ("show:", StringComparison.Ordinal))
				{
				Assert.That (layouts, Does.Contain (attribute.Value.Substring (5)));
				}
			if (attribute.Value.StartsWith ("^", StringComparison.Ordinal))
				{
				Assert.That (labels.ContainsKey (attribute.Value.Substring (1)), Is.True, attribute.ToString ());
				}
			}
		Assert.That (layouts, Has.Length.EqualTo (6));
		Assert.That (state.Definition.Properties.ContainsKey ("extension:uiDefinition"), Is.True);
		}
	[Test]
	public void MetadataUpdatesAssignedLabelsWithoutChangingWateringOrSelection ()
		{
		_timer.Update (Frame (1));
		string status = _timer.Zone2Status;
		_timer.UpdateIdentity (new TimerIdentity (1, 2, "Renamed timer", "Renamed hub", new[] { "Lawn", "Beds", "Tap" }));
		Assert.That (_timer.DeviceLabel, Is.EqualTo ("Renamed timer"));
		Assert.That (_timer.HubLabel, Is.EqualTo ("Renamed hub"));
		Assert.That (new[] { _timer.Zone1Name, _timer.Zone2Name, _timer.Zone3Name }, Is.EqualTo (new[] { "Lawn", "Beds", "Tap" }));
		Assert.That (_timer.Zone2Status, Is.EqualTo (status));
		Assert.That (_timer.Zone2CanStart, Is.False);
		_timer.UpdateIdentity (new TimerIdentity (999, 2, "Other hub"));
		Assert.That (_timer.DeviceLabel, Is.EqualTo ("Renamed timer"));
		_timer.UpdateIdentity (new TimerIdentity (1, 2, "Timer", null, new[] { "", "Beds" }));
		Assert.That (new[] { _timer.Zone1Name, _timer.Zone2Name, _timer.Zone3Name }, Is.EqualTo (new[] { "Zone 1", "Beds", "Zone 3" }));
		XDocument xml = XDocument.Load (Path.Combine (Data, "UiDefinitions", "UiDefinition.xml"));
		var main = xml.Descendants ("layout").Single (x => (string)x.Attribute ("id") == "MainPage");
		Assert.That (main.Attribute ("title")?.Value, Is.EqualTo ("{hubLabel}"));
		Assert.That (main.Attribute ("subtitle")?.Value, Is.EqualTo ("^TimerModel"));
		foreach (int zone in new[] { 1, 2, 3 })
			{
			Assert.That (xml.Descendants ().Single (x => (string)x.Attribute ("id") == "Zone" + zone + "Row").Attribute ("label")?.Value, Is.EqualTo ("{zone" + zone + "Name}"));
			Assert.That (xml.Descendants ().Single (x => (string)x.Attribute ("id") == "Zone" + zone + "Page").Attribute ("title")?.Value, Is.EqualTo ("{zone" + zone + "Name}"));
			}
		}

	[TestCase (1), TestCase (2), TestCase (3)]
	public void HistoryDateAndVolumeStayPairedAcrossStatusAndReadFailures (int zone)
		{
		var date = new DateTimeOffset (2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
		_timer.UpdateHistory (zone, new RecordedUsage ("first", date, 1.4m), true);
		_timer.Update (Frame (1));
		_timer.UpdateHistory (zone, null, false);
		var type = _timer.GetType ();
		Assert.That (type.GetProperty ("Zone" + zone + "Usage").GetValue (_timer), Is.EqualTo ("1.4 L"));
		Assert.That (type.GetProperty ("Zone" + zone + "UsageWhen").GetValue (_timer), Does.Contain ("08:00:00"));
		Assert.That (type.GetProperty ("Zone" + zone + "HistoryStatus").GetValue (_timer), Is.EqualTo ("History refresh unavailable"));
		_timer.UpdateHistory (zone, new RecordedUsage ("stale", date.AddMinutes (-1), 999m), true);
		Assert.That (type.GetProperty ("Zone" + zone + "Usage").GetValue (_timer), Is.EqualTo ("1.4 L"));
		_timer.UpdateHistory (zone, new RecordedUsage ("latest", date.AddMinutes (1), 0m), true);
		Assert.That (type.GetProperty ("Zone" + zone + "Usage").GetValue (_timer), Is.EqualTo ("0 L"));
		Assert.That (type.GetProperty ("Zone" + zone + "UsageWhen").GetValue (_timer), Does.Contain ("08:01:00"));
		}

	[Test]
	public void TimerStartsUnknownAndDisabled ()
		{
		Assert.That (_timer.Zone1CanStart || _timer.Zone2CanStart || _timer.Zone3CanStart, Is.False);
		Assert.That (_timer.IsReady, Is.False);
		Assert.That (_timer.TileStatus, Is.EqualTo ("Waiting for feedback"));
		}
	[Test]
	public void ReportUpdatesTileWithoutChangingOtherZones ()
		{
		_timer.Update (Frame (1));
		Assert.That (_timer.TileStatus, Is.EqualTo ("Zone 2 active"));
		Assert.That (_timer.Zone2CanStart, Is.False);
		Assert.That (_timer.Zone1CanStart && _timer.Zone3CanStart, Is.True);
		Assert.That (_timer.TileIcon, Is.EqualTo ("icSprinklersOn"));
		}
	[Test]
	public void DisconnectInvalidatesTileButRetainsLabelledLastReadings ()
		{
		_timer.Update (Frame (1));
		_timer.SetConnection ("AuthenticationRequired");
		Assert.That (_timer.TileStatus, Is.EqualTo ("Connection unavailable"));
		Assert.That (_timer.Zone2Status, Is.EqualTo ("Last reported: Watering"));
		Assert.That (_timer.Zone1CanStart, Is.False);
		}
	[Test]
	public void PushReconnectPreservesFreshCloudFeedback ()
		{
		_timer.Update (Frame (1));
		_timer.SetConnection ("Reconnecting");
		Assert.That (_timer.TileStatus, Is.EqualTo ("Zone 2 active"));
		Assert.That (_timer.TileIcon, Is.EqualTo ("icSprinklersOn"));
		Assert.That (_timer.IsOnline && _timer.IsReady, Is.True);
		}
	[Test]
	public void ReconnectingCannotMakeStaleFeedbackOnline ()
		{
		TimerReading frame = Frame (1);
		_timer.Update (new TimerReading (frame.Timer, frame.Revision, frame.HubOnline, frame.ReportTime,
			frame.Zones, frame.Battery, frame.Signal, DateTimeOffset.UtcNow.AddMinutes (-2)));
		_timer.SetConnection ("Reconnecting");
		Assert.That (_timer.IsOnline || _timer.IsReady || _timer.Zone1CanStart, Is.False);
		Assert.That (_timer.TileIcon, Is.EqualTo ("icSprinklersOffDisabled"));
		Assert.That (_timer.CanStop, Is.True);
		}
	[Test]
	public void PushReconnectDoesNotRestoreExpiredAuthentication ()
		{
		_timer.Update (Frame (1));
		_timer.SetConnection ("AuthenticationRequired");
		_timer.SetConnection ("Reconnecting");
		Assert.That (_timer.IsOnline || _timer.IsReady || _timer.CanStop, Is.False);
		}
	[TestCase (false, "icSprinklersOff")]
	[TestCase (true, "icSprinklersOn")]
	public void IconReflectsAnyActiveZone (bool active, string icon)
		{
		_timer.Update (new TimerReading (new TimerIdentity (1, 2, "Garden timer"), 1, true, DateTimeOffset.UtcNow,
			Enumerable.Range (1, 3).Select (z => new ZoneReading (z, active && z == 3, "Watering", 0m, TimeSpan.FromMinutes (5), "No reported alarms")).ToArray (), "Battery normal", "RF -70 dBm"));
		Assert.That (_timer.TileIcon, Is.EqualTo (icon));
		}
	[Test]
	public void QuietLiveConnectionRetainsStateUntilDisconnected ()
		{
		TimerReading frame = Frame (1);
		_timer.Update (new TimerReading (frame.Timer, frame.Revision, frame.HubOnline, frame.ReportTime,
			frame.Zones, frame.Battery, frame.Signal, DateTimeOffset.UtcNow.AddMinutes (-10)));
		_timer.SetConnection ("PushConnected");
		Assert.That (_timer.IsReady && _timer.Zone1CanStart, Is.True);
		Assert.That (_timer.TileStatus, Is.EqualTo ("Zone 2 active"));
		_timer.SetConnection ("Reconnecting");
		Assert.That (_timer.IsReady || _timer.Zone1CanStart, Is.False);
		Assert.That (_timer.Zone2CanControl, Is.True, "A known active zone retains Stop during recovery.");
		}
	[Test]
	public void CombinedButtonTracksPendingStartAndStop ()
		{
		_timer.Update (Frame (1));
		Assert.That (_timer.Zone1ActionLabel, Is.EqualTo ("Start timed watering"));
		Assert.That (_timer.Zone2ActionLabel, Is.EqualTo ("Stop this zone"));
		_timer.CommandResult (1, "Sending start");
		_timer.CommandResult (1, "Accepted; awaiting timer report");
		Assert.That (_timer.Zone1ActionLabel, Is.EqualTo ("Stop this zone"));
		Assert.That (_timer.Zone1CanControl, Is.True);
		_timer.CommandResult (1, "Sending stop");
		Assert.That (_timer.Zone1ActionLabel, Is.EqualTo ("Stopping..."));
		Assert.That (_timer.Zone1CanControl, Is.False);
		_timer.CommandResult (1, "New report: Idle");
		Assert.That (_timer.Zone1ActionLabel, Is.EqualTo ("Start timed watering"));
		Assert.That (_timer.Zone1CanControl, Is.True);
		}
	[Test]
	public void ZonePagesHaveOneActionAndNoManualStatusRefresh ()
		{
		XDocument xml = XDocument.Load (Path.Combine (Data, "UiDefinitions", "UiDefinition.xml"));
		foreach (int zone in Enumerable.Range (1, 3))
			{
			XElement page = xml.Descendants ("layout").Single (x => (string)x.Attribute ("id") == "Zone" + zone + "Page");
			Assert.That (page.Descendants ("button").Count (), Is.EqualTo (1));
			Assert.That ((string)page.Descendants ("button").Single ().Attribute ("action"), Is.EqualTo ("command:operateZone" + zone));
			}
		Assert.That (xml.Descendants ("button").Any (x => (string)x.Attribute ("action") == "command:refresh"), Is.False);
		}
	[Test]
	public void ReconnectAcceptsResetRevision ()
		{
		_timer.Update (Frame (100));
		_timer.SetConnection ("Connecting");
		_timer.Update (Frame (1));
		Assert.That (_timer.TileStatus, Is.EqualTo ("Zone 2 active"));
		}
	[Test]
	public void CommandFeedbackDoesNotSetZoneState ()
		{
		_timer.Update (Frame (1));
		_timer.CommandResult (1, "Accepted; awaiting timer report");
		Assert.That (_timer.Zone1Status, Is.EqualTo ("Idle"));
		Assert.That (_timer.Zone1Command, Does.StartWith ("Accepted"));
		}
	[TestCase (1), TestCase (2), TestCase (3)]
	public void DurationLocksForActiveZoneAndUnlocksOnIdle (int zone)
		{
		void Report (long revision, bool active) => _timer.Update (new TimerReading (new TimerIdentity (1, 2, "Garden timer"), revision, true, DateTimeOffset.UtcNow,
			Enumerable.Range (1, 3).Select (z => new ZoneReading (z, z == zone && active, "Watering", null, TimeSpan.FromMinutes (5), "")).ToArray (), "", ""));
		void SetMinutes (int value)
			{
			switch (zone)
				{
				case 1:
					_timer.SetZone1Minutes (value);
					break;
				case 2:
					_timer.SetZone2Minutes (value);
					break;
				case 3:
					_timer.SetZone3Minutes (value);
					break;
				}
			}
		int Minutes () => zone == 1 ? _timer.Zone1Minutes : zone == 2 ? _timer.Zone2Minutes : _timer.Zone3Minutes;
		Report (1, false);
		SetMinutes (8);
		Assert.That (Minutes (), Is.EqualTo (8));
		Report (2, true);
		SetMinutes (10);
		Assert.That (Minutes (), Is.EqualTo (8));
		Assert.That (zone == 1 ? _timer.Zone2CanStart : _timer.Zone1CanStart, Is.True);
		Report (3, false);
		SetMinutes (10);
		Assert.That (Minutes (), Is.EqualTo (10));
		XDocument xml = XDocument.Load (Path.Combine (Data, "UiDefinitions", "UiDefinition.xml"));
		Assert.That ((string)xml.Descendants ("raiselowerwithtext").Single (x => (string)x.Attribute ("id") == "Zone" + zone + "Duration").Attribute ("enabled"), Is.EqualTo ("{zone" + zone + "CanStart}"));
		}
	[Test]
	public void DurationCannotChangeWhileStartPending ()
		{
		_timer.Update (Frame (1));
		_timer.CommandResult (1, "Sending start");
		_timer.SetZone1Minutes (10);
		Assert.That (_timer.Zone1Minutes, Is.EqualTo (5));
		}
	[TestCase (0), TestCase (121)]
	public void InvalidUiDurationRetainsPreviousValue (int minutes)
		{
		_timer.SetZone3Minutes (minutes);
		Assert.That (_timer.Zone3Minutes, Is.EqualTo (5));
		}
	[Test]
	public async Task UnconfiguredPlatformCanBeCreatedAndDisposed ()
		{
		var platform = new RainPointPlatformDriver (_args, _resources, _controller);
		Assert.That (platform.ManagedDevices, Is.Empty);
		Assert.That (platform.GetState ().Definition.Properties.Keys, Does.Contain ("platform:managedDevices"));
		platform.Dispose ();
		await platform.Shutdown;
		}
	[Test]
	public async Task DiscoveryPublishesIrrigationChildrenWithStableIds ()
		{
		var platform = new RainPointPlatformDriver (_args, _resources, _controller);
		try
			{
			await _controller.ConfigureAsync (new DriverSettings ("test@example.invalid", "test", "44"));
			Assert.That (platform.ManagedDevices.Keys, Is.EquivalentTo (new[] { "rainpoint_1_2", "rainpoint_1_3" }));
			Assert.That (platform.ManagedDevices.Values.All (d => d.UxCategory == DeviceUxCategory.IrrigationSystem), Is.True);
			await _controller.ConfigureAsync (null);
			Assert.That (platform.ManagedDevices, Is.Empty);
			}
		finally
			{
			platform.Dispose ();
			await platform.Shutdown;
			}
		}
	private static TimerReading Frame (long revision) => new (new TimerIdentity (1, 2, "Garden timer"), revision, true, DateTimeOffset.UtcNow,
		Enumerable.Range (1, 3).Select (z => new ZoneReading (z, z == 2, "Watering", 1.4m, TimeSpan.FromMinutes (5), "No reported alarms")).ToArray (), "Battery normal", "RF -70 dBm");
	private sealed class FakeConnection : IRainPointConnection
		{
#pragma warning disable CS0067
#pragma warning disable CS0067
		public event Action<IReadOnlyList<TimerIdentity>> CatalogChanged;
		public event Action<int, string[]> PlansChanged;
#pragma warning disable CS0067
		public event Action<int, int, RecordedUsage, bool> HistoryChanged;
#pragma warning disable CS0067
		public void RequestHistory (int address, int zone, bool completion)
			{
			}
		public event Action<TimerReading> Reading;
		public event Action<string> ConnectionState;
#pragma warning restore CS0067
		public Task<IReadOnlyList<TimerIdentity>> ConnectAsync (DriverSettings settings, CancellationToken token) => Task.FromResult<IReadOnlyList<TimerIdentity>> (new[] { new TimerIdentity (1, 2, "Garden"), new TimerIdentity (1, 3, "Patio") });
		public Task StartAsync (int address, int zone, int minutes, CancellationToken token) => throw new AssertionException ("Unexpected live write");
		public Task StopAsync (int address, int zone, CancellationToken token) => throw new AssertionException ("Unexpected live write");
		public Task RefreshAsync (CancellationToken token) => Task.CompletedTask;
		public Task<string[]> ReadPlansAsync (int address, CancellationToken token) => Task.FromResult (new[] { "", "", "" });
		public Task CloseAsync () => Task.CompletedTask;
		}
	}