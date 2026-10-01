# RainPointCrestronDriver

**Version 1.1.1** of a Crestron Home irrigation extension, using the released [RainPointClient 1.2.1 NuGet package](https://www.nuget.org/packages/RainPointClient/1.2.1).

One configured RainPoint Home / Smart+ hub shares a cloud session and MQTT observer. Each recognized **HTV145FRF one-zone, HTV245FRF two-zone or HTV345FRF three-zone timer gets its own room-page tile (for example, Garden or Lawn)**. Supported hubs are HWG023WBRF and HWG023WBRF-V2. Timer identity uses the hub ID and RF address, so changing its display name does not change its identity.

## Installation

Download `RainPointCrestronDriver.pkg` from the [v1.1.1 release](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.1), or extract it from NuGet package `CrestronHomeDriver.RainPoint.Irrigation` version `1.1.1`. This is an installable driver archive, not a .NET application reference. The package manifest reports **1.1.001.0019**, retaining the build sequence used during preview testing.

The recommended way to download and install this driver is the [Crestron Home Driver Feed Installer](https://github.com/oznetmaster/Crestron-Home-Driver-Feed-Installer). Select the NuGet.org feed, search for `CrestronHomeDriver.RainPoint.Irrigation`, inspect version 1.1.1 or newer, select your processor and upload the package. Then complete steps 3–6 below in Crestron Home Setup.

The NuGet distribution follows the [Crestron Home Driver NuGet Publishing Standard v1](https://github.com/oznetmaster/Crestron-Home-Driver-Feed-Installer/blob/main/docs/standard/crestron-home-driver-nuget-publishing-standard-v1.md), including its root `crestron-driver-package.json` manifest. This community packaging standard is not an official Crestron specification. For manual installation, follow all the steps below. The GitHub **Source code (zip)** and **Source code (tar.gz)** downloads are repository snapshots, not installable driver packages.

1. Download `RainPointCrestronDriver.pkg` from the release assets, or extract that file from the NuGet package.
2. Connect to your Crestron Home processor using SFTP and your processor credentials. Upload the `.pkg` to `/user/ThirdPartyDrivers/Import`.
3. Open the **Crestron Home Setup** application and connect to the processor. In **Pair Devices**, add the **RainPoint Irrigation** platform from the **RainPoint** manufacturer entry after the import completes.
4. Enter the RainPoint account email, password and country calling code. Use a dedicated account invited to the RainPoint home. Set the optional Home ID and Hub ID when discovery is ambiguous; see [Configuration](#configuration).
5. Save the configuration and allow cloud discovery to complete. Select each discovered timer extension and assign it to the appropriate room, such as **Garden** or **Lawn**.
6. Open that room on a Crestron Home touch panel or app. Confirm that the timer tile is online and opens its zone page. Tiles appear on the **room page**, not the Home page.

For an update, importing a newer package does not by itself update an installed driver instance. Apply the available driver update in Setup, then verify the installed version and retained room assignment.

Internet access and a RainPoint Home / Smart+ account with access to the paired hub and timer are required. Pair devices and manage schedules in the official app. This release has been exercised with the currently paired HWG023WBRF-family hub and HTV345FRF timer; other hardware and multiple simultaneous hubs have not been validated.

HTV145FRF and HTV245FRF have app/protocol-reference and offline NUnit coverage; they have not been tested on physical hardware by this project. Normal timed start/stop, status, usage, names and saved-plan display are implemented. See [timer variants](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/docs/TIMER-VARIANTS.md) for scope.

See the [release notes](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/RELEASE-NOTES.md), [changelog](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/CHANGELOG.md) and [programming reference](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/docs/PROGRAMMING.md).

## User interface

- **Timer tile:** zones reported active, all zones idle, unknown status or connection unavailable. A soaking pause remains an active program. Sprinkler icons follow the reported state.
- **Overview:** one status row for each actual zone, stop-all for that timer, saved plans and connection/timer information.
- **Zone pages:** reported mode, dated last recorded usage in litres, time left from the reported end timestamp, alarms, selected manual duration (1–120 minutes, initially 5), one button that changes from Start timed watering to Stop this zone during active or pending watering, then returns after reported idle.
- **Saved plans:** per-zone plan summaries, enabled state, start time, recurrence, mode, duration and configured rain-delay end. Editing schedules stays in RainPoint Home for this first driver iteration.
- **Connection and timer:** cloud/observer state, timer data-change timestamp, battery condition, RF signal, updated automatically.

Every existing zone has the same controls; absent zones are hidden and have no programmable entities. Starting one zone sends no commands to the others. Stop-all explicitly attempts each of the timer's actual zones, including when one attempt fails.

The main detail-page title uses the app-assigned hub name, with its discovered model as the subtitle. Zone pages also show the model as their subtitle. Zone rows, detail-page titles and plan headings use the app-assigned zone names, falling back independently to Zone 1, Zone 2 or Zone 3 for blank entries. MQTT home-configuration notifications queue a fresh read of names and plans; reconnecting also catches up configuration. Duplicate notifications are coalesced. A failed configuration read retains the last display and retries without marking watering feedback offline. Timer identities and room assignments remain unchanged. There are no separate Refresh status or Refresh plans buttons; opening Plans still refreshes its summaries.

Each zone has a Last recorded usage section. Its volume and date/time come from the same watering or water-usage history record. Device-reported home time and its supplied timezone are preserved; if that local time is missing, the cloud record time is labelled explicitly as UTC. Startup and MQTT reconnection read recent history. A known active-to-idle report queues one immediate read and two retries 30 seconds apart to allow for cloud delay. Reads are coalesced per zone and serialized; there is no periodic history poll or manual refresh button. History is limited to the latest 50 returned events per zone. Missing history is not zero usage, and a failed read retains any previous dated record with an unavailable label. Cloud history can lag the latest run.

## Programming

The three-zone timer exposes **26 read-only properties, 10 commands and 18 events**. One- and two-zone timers omit the corresponding entities for absent zones. All are available to Crestron Home programming. These include per-zone activity, countdown, usage and alarms; explicit starts with a duration; individual/all-zone stops; and reported activity, alarm, battery and availability transitions. See the [programming reference](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/docs/PROGRAMMING.md) for IDs, unknown values and event semantics.

## Configuration

Add the RainPoint Irrigation platform, supply the account email, password and country calling code (for example `44`), then select its discovered irrigation extensions in Crestron Home. Optional **Home ID** and **Hub ID** fields select exact numeric cloud IDs. Omit an ID only when discovery has exactly one matching result; ambiguous discovery never chooses the first item silently.

Use a dedicated RainPoint account invited to the home. The official app can displace another login to the same account. The library renews the session and permits one bounded password recovery; repeated displacement requires explicit reconnect. Passwords are handled through a masked persistent Crestron configuration field and are never included in diagnostics. No private settings are included in this repository.

One platform instance per account/hub is the initial supported arrangement. Multiple timer tiles share its observer. Multiple concurrent hub connections with the same account are not implemented or validated.

## Feedback and controls

Acknowledgements appear as **Accepted; awaiting timer report** and never change the displayed zone state. Failed or uncertain watering commands are not replayed. Duplicate starts are blocked while awaiting feedback; an unconfirmed command reports a timeout after 90 seconds. Stops remain available when readings become stale, provided the session is available. The overview Stop all button is disabled when all zones are confirmed idle and no command is pending.

The driver reads status at startup and catches up after each MQTT connection. Once that connection is synchronized, routine cloud status polling stops. If live updates are unavailable, the monitor checks status every 30 seconds. Known readings remain available while synchronized MQTT is connected; otherwise, ninety seconds without a successful cloud read or timer push makes controls/status unavailable. The timer's **data-change time is shown separately**: an unchanged idle report can be old even when cloud communication is healthy. Neither a fresh cloud read nor a broker connection proves physical valve position. Last usage is a reported volume, not instantaneous flow. Time left is calculated locally from the timer-reported end timestamp and the home timezone, without additional polling. Missing, stale or ambiguous time information displays as unavailable. Reaching zero waits for reported stop confirmation; cyclic modes label it as phase time. The duration selector configures the next manual start and is disabled while that zone is active, awaiting command feedback, or lacks fresh idle status.

Reconfiguration cancels and drains old work before closing the old connection. Queued commands cannot transfer to a replacement session. Startup, discovery, reconnect, build and tests do not initiate watering.

## Build and tests

Open `RainPointCrestronDriver.slnx` in Visual Studio with .NET SDK **10.0.401**, or:

```powershell
dotnet build RainPointCrestronDriver.slnx -c Release
dotnet test RainPointCrestronDriver.Tests -c Release
dotnet test RainPointCrestronDriver.Lifecycle.Tests -c Release --filter "TestCategory!=Package"
```

The solution restores RainPointClient 1.2.1 from NuGet.org. A sibling client checkout is not required.

Production driver targets **net472** with **C# 14**. The shared controller and portable tests also target **net10.0**. Follow the root `.editorconfig`.

See [tests/README.md](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/tests/README.md) for NUnit dependencies, SDK harness setup and package smoke tests, and [release preparation](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/docs/RELEASING.md) for the publication workflow. `tools/Build-Package.ps1` creates and validates a local `.pkg`; it does not deploy, publish or reboot a processor. Normal solution builds compile only. Packaging requires ManifestUtil 29.0.10 and dotnet-ilrepack 2.0.45, matching the local model-driver toolchain. Tool locations can be supplied in an ignored `RainPointCrestronDriver.Local.targets` file.

## Validation

Validation covers 91 portable tests on each runtime, 44 SDK tests, two merged-package tests, and the separate client suite of 1,380 processor tests. A one-minute zone-1 Home automation run verified device-reported countdown, automatic stop, the programmable stopped event and dated usage. App renames and disabled-plan creation/deletion updated the connected driver automatically through the shared support account. The test plan and temporary Home programming were removed. All-zone command paths have offline coverage; the automated live watering run used only zone 1. Phone/touch-panel appearance and device-reported results remain distinct from independent physical observation.

See [DEVELOPMENT-HISTORY.md](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/DEVELOPMENT-HISTORY.md) for validation and [docs/UI.md](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/docs/UI.md) for the page design and initial scope.

## Attributions, trademarks and license

Crestron SDK example definitions retain their original notices. RainPointClient supplies protocol behavior; its upstream references and notices are preserved with the package. See [THIRD-PARTY-NOTICES.md](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/THIRD-PARTY-NOTICES.md).

RainPoint, HomGar and Crestron are trademarks of their respective owners and identify compatibility only. This independent, unofficial project is not affiliated with, endorsed or approved by those owners. Software is provided **AS IS**, without warranty.

Copyright (c) 2026 Neil Colvin. [MIT License with Commons Clause](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/LICENSE), including the installation-service exception used by the other driver repositories.
## NUnit 5 test tooling

All maintained NUnit suites use the official NUnit 5.0.0 framework. Async exception assertions are awaited, and discarded-task warnings fail test builds. Processor test packages use CrestronHomeNUnit SDK 2.2.0; workflow and Android suites, where provided, use the released 2.2.0 adapter. Tests remain available in Visual Studio, VS Code and the command line. Live and manual tests still require their documented devices and permissions. This is a test-tooling update; the published product version and runtime behavior are unchanged.

## NUnit 5 test package

Test package **1.0.0** uses **NUnit 5.0.0**. It is independent of the product version. [Download package](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0.pkg), [documentation](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0-Documentation.zip), [validation](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0.validation.json), [exact source revisions](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0.sources.json), and [SHA-256 checksums](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0-SHA256SUMS.txt) are attached to the existing product release. No product binary or NuGet version changed for this test update.

Validated on 1 October 2026: 96 offline cases passed in each of two runs from the packaged assembly on Windows. All suite identities were checked against source discovery. Live/manual tests and execution on the processor were not repeated during this migration; earlier hardware results do not certify this new package.
