// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NUnit.Framework;
using RainPoint.CrestronDriver;
using RainPoint.CrestronDriver.Core;

namespace RainPointCrestronDriver.Lifecycle.Tests;

public sealed partial class ExtensionTests
	{
	[TestCase ("HTV145FRF", 1), TestCase ("HTV245FRF", 2), TestCase ("HTV345FRF", 3)]
	public void VariantUiAndProgrammingOnlyExposeExistingZones (string model, int count)
		{
		_timer.Dispose ();
		var identity = new TimerIdentity (1, 2, "Timer", "Garden", new[] { "Lawn", "Beds", "Tap" }, model);
		_timer = new RainPointTimerEntity (identity, _controller, _args, _resources);
		string path = Path.Combine (Data, "UiDefinitions/UiDefinition.xml");
		var errors = new List<string> ();
		var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, XmlResolver = null };
		settings.Schemas.Add (null, Path.Combine (TestContext.CurrentContext.TestDirectory, "ExtensionsSchemaDefinition.xsd"));
		settings.ValidationEventHandler += (_, e) => errors.Add (e.Message);
		using (var reader = XmlReader.Create (path, settings))
			while (reader.Read ()) { }
		Assert.That (errors, Is.Empty);
		var xml = XDocument.Load (path);
		var state = _timer.GetState ();
		foreach (var attribute in xml.Descendants ().Attributes ())
			foreach (Match match in Regex.Matches (attribute.Value, @"\{([^}]+)\}"))
				Assert.That (state.Definition.Properties.ContainsKey (match.Groups[1].Value), Is.True, attribute.ToString ());
		for (int zone = 1; zone <= 3; zone++)
			{
			Assert.That (xml.Descendants ("layout").Any (x => (string)x.Attribute ("id") == "Zone" + zone + "Page"), Is.True);
			if (zone > 1)
				{
				Assert.That (xml.Descendants ("statusandnavigation").Single (x => (string)x.Attribute ("id") == "Zone" + zone + "Row").Attribute ("visible")?.Value, Is.EqualTo ("{hasZone" + zone + "}"));
				Assert.That (state.Definition.PropertyMetadata["zone" + zone + "Minutes"].Programmable, Is.EqualTo (zone <= count));
				}
			Assert.That (state.Definition.Properties.ContainsKey ("zone" + zone + "State"), Is.EqualTo (zone <= count));
			Assert.That (state.Definition.Commands.ContainsKey ("startZone" + zone + "For"), Is.EqualTo (zone <= count));
			Assert.That (state.Definition.Events.ContainsKey ("zone" + zone + "Started"), Is.EqualTo (zone <= count));
			}
		Assert.That (_timer.DeviceModel, Is.EqualTo (model));
		Assert.That (_timer.HasZone2, Is.EqualTo (count >= 2));
		Assert.That (_timer.HasZone3, Is.EqualTo (count >= 3));
		foreach (var attribute in xml.Descendants ().Attributes ())
			{
			if (attribute.Value.StartsWith ("command:", StringComparison.Ordinal))
				Assert.That (state.Definition.Commands.ContainsKey (attribute.Value.Substring (8)), Is.True, attribute.ToString ());
			if (attribute.Value.StartsWith ("show:", StringComparison.Ordinal))
				Assert.That (xml.Descendants ("layout").Any (x => (string)x.Attribute ("id") == attribute.Value.Substring (5)), Is.True);
			}
		var zones = Enumerable.Range (1, count).Select (zone => new ZoneReading (zone, false, "Idle", 0, TimeSpan.Zero, "None")).ToArray ();
		_timer.Update (new TimerReading (identity, 1, true, DateTimeOffset.UtcNow, zones, "Normal", "-70"));
		_timer.UpdatePlans (Enumerable.Repeat ("No saved plans", count).ToArray ());
		Assert.That (_timer.TileStatus, Is.EqualTo ("All zones idle"));
		Assert.That (_timer.ActiveZoneCount, Is.Zero);
		Assert.That (_timer.CanStop, Is.False);
		int started = 0, stopped = 0;
		_timer.IrrigationStarted += (_, _) => started++;
		_timer.AllZonesStopped += (_, _) => stopped++;
		zones[0] = new ZoneReading (1, true, "Watering", 1, TimeSpan.FromMinutes (1), "None", DateTimeOffset.UtcNow.AddSeconds (30));
		_timer.Update (new TimerReading (identity, 2, true, DateTimeOffset.UtcNow, zones, "Normal", "-70"));
		Assert.That (_timer.ActiveZoneCount, Is.EqualTo (1));
		Assert.That (_timer.CanStop, Is.True);
		zones[0] = new ZoneReading (1, false, "Idle", 1, TimeSpan.Zero, "None");
		_timer.Update (new TimerReading (identity, 3, true, DateTimeOffset.UtcNow, zones, "Normal", "-70"));
		Assert.That (started, Is.EqualTo (1));
		Assert.That (stopped, Is.EqualTo (1));
		}
	}