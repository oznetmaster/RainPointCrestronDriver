# Changelog

## [1.1.5](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.5) - 2026-11-15

- Keep 30-second cloud status reconciliation active alongside MQTT push, preventing quiet sessions and observer renewal from losing recent successful-contact evidence.
- Preserve the existing 90-second fallback freshness limit: failed reads never refresh contact, and expired authentication still disables control.
- Add regression coverage for quiet timer reports, successful periodic reads, failed reads and reconnect/authentication availability. Watering commands and saved configuration formats are unchanged.

## [1.1.4](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.4) - 2026-11-10

- Use RainPointClient 1.2.2.
- Extend the existing processor test package to run the SDK entity lifecycle and programming regressions on net472/Mono as well as the portable tests.
- No new watering behavior or configuration migration is introduced.

## [1.1.1](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.1) — 2026-09-29

- Documentation updates. No functional changes.

## [1.1.0](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.0) — 2026-09-29

Add one- and two-zone timer UI/control handling with offline coverage. Correct NuGet feed packaging and update documentation.

## [1.0.0](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.0.0) — 2026-09-29

Initial release.

- One room-page irrigation tile per supported three-zone timer, with app-assigned names and activity icons.
- Independent timed watering and stop controls for every zone, device-reported countdown, alarms and dated usage.
- Live cloud feedback, automatic name and plan updates, saved-plan summaries and connection diagnostics.
- Crestron Home programming properties, commands and events.
- RainPointClient 1.1.0 integration, NUnit coverage on net472 and net10.0, SDK and merged-package checks, and installed Home automation validation.