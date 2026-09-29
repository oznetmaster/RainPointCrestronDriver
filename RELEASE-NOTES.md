# RainPointCrestronDriver v1.0.0

Initial release of the RainPoint Home / Smart+ irrigation extension for Crestron Home. Each supported HTV345FRF three-zone timer has its own room-page tile.

## Features

- Independent timed watering and stop controls for all three zones, plus Stop all for the timer.
- Live reported activity, remaining phase time, alarms, battery condition and dated water usage.
- App-assigned hub and zone names, saved-plan summaries and automatic configuration updates.
- 26 programming properties, 10 commands and 18 events for Crestron Home automation.

Scheduling and device pairing remain in the RainPoint app. Use a dedicated account invited to the home to avoid displacing the owner's app login. This release uses the published RainPointClient 1.1.0 package and requires cloud access.

## Install

Use the attached `RainPointCrestronDriver.pkg`, or extract it from NuGet package `CrestronHomeDriver.RainPoint.Irrigation` version `1.0.0`. The manifest version is `1.0.000.0017`. Configure the RainPoint Irrigation platform and assign its timer extension to a room. See the [README](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/README.md) for configuration, supported hardware and operating limits.

## Validation

The driver suite covers 91 portable cases on each of net472 and net10.0, 44 desktop SDK cases and two merged-package cases. The separate client suite passed 1,380 processor cases. Installed Home testing verified programming metadata and a one-minute zone-1 automation run, including decreasing countdown, natural completion, the stopped event and dated usage. App rename and disabled-plan creation/deletion changes reached the driver automatically. Temporary programming and the test plan were removed.

Live evidence is from the currently paired hub and three-zone timer. All-zone command paths have offline coverage; the automated watering run used zone 1. Device-reported usage was verified without independent physical volume measurement. See the [test guide](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/tests/README.md) for dependencies and evidence boundaries.

## License and notices

MIT License with Commons Clause; see the attached LICENSE and third-party notices. RainPoint, HomGar and Crestron are trademarks of their respective owners. This is an independent, unofficial project, supplied AS IS without warranty.