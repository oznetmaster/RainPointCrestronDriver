# Extension page design

Each supported timer is an `IrrigationSystem` child of one configured hub platform. The root platform holds credentials and the connection. Each child has a shared six-page extension definition:

1. **MainPage** — current reported summary; Zone 1, 2 and 3 navigation; stop all; plans; diagnostics.
2. **Zone1Page** — zone 1 feedback and timed manual control.
3. **Zone2Page** — equivalent controls for zone 2.
4. **Zone3Page** — equivalent controls for zone 3.
5. **PlansPage** — saved-plan summaries and rain-delay configuration for each zone, refreshed automatically when the page is opened.
6. **DiagnosticsPage** — connection, report timestamp, battery condition, RF signal, updated automatically by the connection monitor and incoming status reports.

The selected duration changes only the next manual command; it never edits saved plans. Each zone's +/- controls are disabled while that zone is active or awaiting command feedback, and require fresh idle status to become editable. Other idle zones remain independently adjustable. Each zone has one action button: Start timed watering while idle, Stop this zone while active or awaiting start feedback, and disabled Stopping... while a stop awaits feedback. Reported idle restores Start timed watering. The button always sends an explicit start or stop; it does not assume command acceptance changes the valve state. Acknowledgements and reports have separate displays.

The status line counts down from the reported end timestamp using home timezone data; it never infers a countdown from configured duration or command-send time. The one-second UI refresh sends no cloud request. Expiry waits for reported stop confirmation, and cyclic operation labels phase time separately. The tile says **active**, covering both watering and the soaking phase of an active program.

Use the [Crestron Entity V2 extension adapter](https://sdkcon78221.crestron.com/sdk/Crestron_Certified_Drivers_SDK/Content/Topics/Driver-SDK-V2/Create-a-Driver/Add-Extension-Support.htm), [extension schema](https://prd-use-rad-assets.azurewebsites.net/ExtensionsSchemaDefinition.xsd) and [standard icon library](https://sdkcon78221.crestron.com/sdk/Crestron_Certified_Drivers_SDK/Content/Extension-Device-Icons.pdf). The timer tile uses `icSprinklersOn`, `icSprinklersOff` and `icSprinklersOffDisabled`.

The Windows RainPoint app guides terminology and feature selection. Its account administration, pairing, firmware operations, scene editor, weather tools and complex schedule editor are outside this initial household control surface. Plan editing and cycle/misting creation remain available in RainPoint Home. Existing cyclic operation is decoded and displayed by the driver.

Room-tile and zone-page navigation were reviewed on the installed preview with the user. Schema and SDK tests separately validate bindings and layout structure; they do not establish rendering on every phone or touchscreen.

The timer tile appears only on its assigned room page, such as Garden or Lawn; it is hidden from the Home page.


The main detail-page title uses the app-assigned hub name, with HTV345FRF as its subtitle. Zone pages also show the model as their subtitle. Zone rows, detail-page titles and plan headings use the app-assigned zone names, falling back independently to Zone 1, Zone 2 or Zone 3 for blank entries. MQTT home-configuration notifications queue a fresh read of names and plans; reconnecting also catches up configuration. Duplicate notifications are coalesced. A failed configuration read retains the last display and retries without marking watering feedback offline. Timer identities and room assignments remain unchanged. There are no separate Refresh status or Refresh plans buttons; opening Plans still refreshes its summaries.

Each zone has a Last recorded usage section. Its volume and date/time come from the same watering or water-usage history record. Device-reported home time and its supplied timezone are preserved; if that local time is missing, the cloud record time is labelled explicitly as UTC. Startup and MQTT reconnection read recent history. A known active-to-idle report queues one immediate read and two retries 30 seconds apart to allow for cloud delay. Reads are coalesced per zone and serialized; there is no periodic history poll or manual refresh button. History is limited to the latest 50 returned events per zone. Missing history is not zero usage, and a failed read retains any previous dated record with an unavailable label. Cloud history can lag the latest run.

Version 1.1.0 retains one shared v3 definition. `hasZone2` and `hasZone3` control visibility of zone rows, detail-page control groups and plan summaries. The user-facing diagnostics label is **Connection and timer**.