# RainPointCrestronDriver v1.1.0

Adds one-zone HTV145FRF and two-zone HTV245FRF handling alongside the three-zone HTV345FRF, using RainPointClient 1.2.0. Each timer exposes only its actual zone pages, controls, plan summaries and programmable entities. Status, history and Stop all follow the same zone count.

The added models have app/protocol-reference and offline NUnit coverage. Only the owned HWG023WBRF-family hub with HTV345FRF has project hardware validation. This release does not claim physical validation of the added models or add their advanced configuration writes.

The NuGet distribution now includes the standard feed manifest and discovery metadata. Documentation includes explicit installation steps and corrected attribution wording.

Install using the [Crestron Home Driver Feed Installer](https://github.com/oznetmaster/Crestron-Home-Driver-Feed-Installer), or upload the attached `RainPointCrestronDriver.pkg` to `/user/ThirdPartyDrivers/Import` using SFTP. Configure **RainPoint Irrigation** in Crestron Home Setup and assign each discovered timer to its room. For an existing installation, apply the driver update after importing. Manifest version: **1.1.000.0018**.

See the [README](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/README.md) and [test guide](https://github.com/oznetmaster/RainPointCrestronDriver/blob/main/tests/README.md). Offline validation covers 96 portable cases on each target, 47 SDK cases and five merged-package cases. No new hardware watering or deployment was performed for this release.

MIT License with Commons Clause; see LICENSE and third-party notices. RainPoint, HomGar and Crestron are trademarks of their respective owners. This independent, unofficial project is supplied AS IS without warranty.