# RainPointCrestronDriver

**Version 1.0.0** of a Crestron Home irrigation extension, using the released [RainPointClient 1.1.0 NuGet package](https://www.nuget.org/packages/RainPointClient/1.1.0).

One configured RainPoint Home / Smart+ hub shares a cloud session and MQTT observer. Each discovered **HTV345FRF three-zone timer gets its own room-page tile (for example, Garden or Lawn)**. Supported hubs are HWG023WBRF and HWG023WBRF-V2. Timer identity uses the hub ID and RF address, so changing its display name does not change its identity.

## Installation

Download `RainPointCrestronDriver.pkg` from the [v1.0.0 release](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.0.0), or extract it from NuGet package `CrestronHomeDriver.RainPoint.Irrigation` version `1.0.0`. This is an installable driver archive, not a .NET application reference. The package manifest reports **1.0.000.0017**, retaining the build sequence used during preview testing.

Import the package through your usual Crestron Home custom-driver installation process, add the RainPoint Irrigation platform, and configure the dedicated account described below. Assign each discovered timer to its garden or lawn room. Its tile appears on that room page.

Internet access and a RainPoint Home / Smart+ account with access to the paired hub and timer are required. Pair devices and manage schedules in the official app. This release has been exercised with the currently paired HWG023WBRF-family hub and HTV345FRF timer; other hardware and multiple simultaneous hubs have not been validated.

See the [release notes](RELEASE-NOTES.md), [changelog](CHANGELOG.md) and [programming reference](docs/PROGRAMMING.md).

## User interface

- **Timer tile:** zones reported active, all zones idle, unknown status or connection unavailable. A soaking pause remains an active program. Sprinkler icons follow the reported state.
- **Overview:** three zone status rows, stop-all for that timer, saved plans and diagnostics.
- **Zone 1 / 2 / 3:** reported mode, dated last recorded usage in litres, time left from the reported end timestamp, alarms, selected manual duration (1–120 minutes, initially 5), one button that changes from Start timed watering to Stop this zone during active or pending watering, then returns after reported idle.
- **Saved plans:** per-zone plan summaries, enabled state, start time, recurrence, mode, duration and configured rain-delay end. Editing schedules stays in RainPoint Home for this first driver iteration.
- **Diagnostics:** cloud/observer state, timer data-change timestamp, battery condition, RF signal, updated automatically.

All three zones have identical controls. Starting one zone sends no commands to the others. Stop-all explicitly attempts each of the timer's three zones, including when one attempt fails.

The main detail-page title uses the app-assigned hub name, with HTV345FRF as its subtitle. Zone pages also show the model as their subtitle. Zone rows, detail-page titles and plan headings use the app-assigned zone names, falling back independently to Zone 1, Zone 2 or Zone 3 for blank entries. MQTT home-configuration notifications queue a fresh read of names and plans; reconnecting also catches up configuration. Duplicate notifications are coalesced. A failed configuration read retains the last display and retries without marking watering feedback offline. Timer identities and room assignments remain unchanged. There are no separate Refresh status or Refresh plans buttons; opening Plans still refreshes its summaries.

Each zone has a Last recorded usage section. Its volume and date/time come from the same watering or water-usage history record. Device-reported home time and its supplied timezone are preserved; if that local time is missing, the cloud record time is labelled explicitly as UTC. Startup and MQTT reconnection read recent history. A known active-to-idle report queues one immediate read and two retries 30 seconds apart to allow for cloud delay. Reads are coalesced per zone and serialized; there is no periodic history poll or manual refresh button. History is limited to the latest 50 returned events per zone. Missing history is not zero usage, and a failed read retains any previous dated record with an unavailable label. Cloud history can lag the latest run.

## Programming

Each timer exposes **26 read-only properties, 10 commands and 18 events** to Crestron Home programming. These include per-zone activity, countdown, usage and alarms; explicit starts with a duration; individual/all-zone stops; and reported activity, alarm, battery and availability transitions. See the [programming reference](docs/PROGRAMMING.md) for IDs, unknown values and event semantics.

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

The solution restores RainPointClient 1.1.0 from NuGet.org. A sibling client checkout is not required.

Production driver targets **net472** with **C# 14**. The shared controller and portable tests also target **net10.0**. Follow the root `.editorconfig`.

See [tests/README.md](tests/README.md) for NUnit dependencies, SDK harness setup and package smoke tests, and [release preparation](docs/RELEASING.md) for the publication workflow. `tools/Build-Package.ps1` creates and validates a local `.pkg`; it does not deploy, publish or reboot a processor. Normal solution builds compile only. Packaging requires ManifestUtil 29.0.10 and dotnet-ilrepack 2.0.45, matching the local model-driver toolchain. Tool locations can be supplied in an ignored `RainPointCrestronDriver.Local.targets` file.

## Validation

Validation covers 91 portable tests on each runtime, 44 SDK tests, two merged-package tests, and the separate client suite of 1,380 processor tests. A one-minute zone-1 Home automation run verified device-reported countdown, automatic stop, the programmable stopped event and dated usage. App renames and disabled-plan creation/deletion updated the connected driver automatically through the shared support account. The test plan and temporary Home programming were removed. All-zone command paths have offline coverage; the automated live watering run used only zone 1. Phone/touch-panel appearance and device-reported results remain distinct from independent physical observation.

See [DEVELOPMENT-HISTORY.md](DEVELOPMENT-HISTORY.md) for validation and [docs/UI.md](docs/UI.md) for the page design and initial scope.

## Attributions, trademarks and license

Driver scaffolding and packaging follow the author's existing Overkiz and Tesla Powerwall extension drivers. Crestron SDK example definitions retain their original notices. RainPointClient supplies protocol behavior; its upstream references and notices are preserved with the package. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

RainPoint, HomGar and Crestron are trademarks of their respective owners and identify compatibility only. This independent, unofficial project is not affiliated with, endorsed or approved by those owners. Software is provided **AS IS**, without warranty.

Copyright (c) 2026 Neil Colvin. [MIT License with Commons Clause](LICENSE), including the installation-service exception used by the other driver repositories.