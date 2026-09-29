# Testing RainPointCrestronDriver

All default tests are offline; no credentials, cloud login or valve operations are used.

| Project | Runtime | Coverage |
|---|---|---|
| RainPointCrestronDriver.Tests | net472 and net10.0 | Typed status presentation, discovery selection, every zone's commands, pending feedback, cancellation and connection replacement |
| RainPointCrestronDriver.Lifecycle.Tests | net10.0 desktop SDK | Real Crestron entity definitions, dynamic child registration, UI property/command/translation bindings and XML schema |
| Package category in the SDK project | net10.0 loading merged net472 driver | Attributed JSON round trip through synthetic HTTP responses and embedded MQTT trust anchor |

Development dependencies: NUnit **5.0.0**, NUnit3TestAdapter **6.3.0**, Microsoft.NET.Test.Sdk **18.10.1**, NUnit.Analyzers **4.15.0**. Visual Studio Test Explorer discovers the portable and desktop SDK suites through the NUnit adapter. Runtime dependencies are the released RainPointClient **1.1.0** NuGet package and Crestron.DeviceDrivers.DevKit **29.0.10**; the portable net472 projects use Microsoft.NETFramework.ReferenceAssemblies **1.0.3** for compilation.

The SDK desktop harness restores its compatibility assembly from the public Crestron.DeviceDrivers.ManifestUtil **29.0.10** package. Its bytes match the previously used local SDK assembly. No private desktop assembly, credential or CI secret is required. This dependency is confined to the test host; the production merge excludes Crestron-provided assemblies.

Run `tools/Build-Package.ps1` for the merged assembly tests and package inspection. The script sets `RAINPOINT_PACKAGED_ASSEMBLY` only while the package tests run. Without that explicit path, package-specific tests are skipped; regular tests need no package build. Results are written under ignored `artifacts/tests` when using the packaging script.

The checked-in `ExtensionsSchemaDefinition.xsd` is the Crestron v3 schema already used in TeslaPowerwallCrestronDriver. Its source URL is retained in [docs/UI.md](../docs/UI.md). UI tests validate every layout destination, label translation and bound entity property or command, as well as the XML schema.

Client processor tests use the existing `CrestronLibraryTests/RainPointClient.ProcessorTests.slnx` solution and its NUnit workflow adapter. The current run passed all 1,380 processor cases and removed its temporary test instance. Those shared workflow edits remain local. Driver installation, programming and live results are recorded below and in DEVELOPMENT-HISTORY.md. Watering fixtures remain explicit and duration-bounded.


Programming coverage includes SDK registration and translations for all 26 properties, 10 commands and 18 events; transition suppression at startup/unknown/recovery; duplicate reports; typed battery/alarms; per-zone explicit duration commands; and numeric countdowns. The release suite passes 91 portable tests on each runtime, 44 SDK tests and two merged-package smoke tests. No watering commands were sent by these tests.

## Installed Home automation tests

`RainPointCrestronDriver.Automation.Tests` uses .NET 10 on Windows, the NUnit 5 framework and NUnit3TestAdapter versions above, and CrestronHomeDevTools 1.21.0. All installed fixtures are explicit and excluded from normal offline runs. The solution includes the project for Test Explorer discovery.

Set `RAINPOINT_HOME_TEST_SETTINGS` to an ignored private JSON file containing `BindingsPath`, `CredentialAlias`, `Host`, `DeviceId`, `ParentDeviceId`, `RoomId`, `Version` and `AllowProgrammingChanges`. Bindings refer to the DevTools private credential store; do not put passwords in test settings. The saved processor TLS certificate and SSH host-key pins are required. Run from a trusted Windows worker that can access that store and the processor.

Use an exact fully qualified NUnit filter to select either `InspectInstalledProgrammingCatalog` (read-only) or `HomePropertyEventExecutesConditionalStopAndRestores` (temporary programming). The execution fixture also requires `AllowProgrammingChanges: true`. It validates the installed timer identity/version, acquires a processor reservation, requires all zones idle, and refuses to overwrite an already programmed event.

The execution fixture attaches a conditional Stop-zone-1 action to the otherwise unused selected-duration change event. It changes only the selected duration to trigger Home, checks command feedback, removes its own programming, restores the original duration and releases its reservation. It sends no Start command. Read-back, rather than an HTTP acknowledgement, establishes completion. Its private sequence journal records mutations and cleanup; if cleanup cannot be verified, inspect and recover the retained reservation before retrying. This representative execution test does not establish live firing of all watering, battery and alarm events.

## Release validation — 29 September 2026

The v1.0.0 preparation passed 91 portable cases on net472, 91 on net10.0, 44 SDK cases and two merged-package cases. The public ManifestUtil package supplies the desktop SDK compatibility assembly, so a clean checkout has no private-file requirement. Hosted Tests runs the same scripts and retains TRX files and candidate assets. The release-check policy also has 40 offline scenarios.

Regression coverage includes every zone's explicit commands and named duration arguments, countdown ambiguity, pending feedback, startup/recovery transition suppression, command/session cancellation, assigned-name fallback, configuration coalescing/retry, and dated volume/history pairing. Package cases verify attributed serialization after merging and the MQTT trust anchor.

Installed Home validation on the public-client preview verified the programming catalog, preserved device identities/rooms, online/ready state, MQTT connection, and no command replay after reload. A single one-minute zone-1 Home automation run verified active/countdown/control transitions, natural stop, execution of the stopped event and a new dated 3.1 L history record. No zone-2 or zone-3 watering was initiated. Independent physical volume measurement was not performed.

Use the explicit `OneMinuteZone1CompletesWithCustomEventAndDatedUsage` fixture only with both AllowProgrammingChanges and AllowShortZone1Run enabled. It refuses existing programming on its test events, obtains a processor reservation and restores durations/programming on cleanup. A selected-duration event on zone 2 triggers the zone-1 action; it does not operate zone 2. The earlier generic-command dispatch attempt failed without starting water; the successful corrected test uses Home programming. Private journals retain both attempts.

App zone renaming and disabled-plan creation/deletion reached the shared-account driver without manual refresh or reload. The test plan and temporary Home programming were removed. All three zones finished idle. The separate client workflow passed 1,380 processor cases plus 2,760 desktop cases and cleaned up its temporary instance.

Detailed dated evidence and earlier preview versions are retained in [DEVELOPMENT-HISTORY.md](../DEVELOPMENT-HISTORY.md). Hardware credentials and private journals are excluded from the repository. The release preparation itself does not initiate watering or run installed automation automatically.