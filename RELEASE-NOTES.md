# RainPointCrestronDriver v1.1.5

This patch keeps successful cloud-contact evidence current when a timer is quiet or its MQTT observer renews. Status is reconciled every 30 seconds alongside push updates. Failed reads do not refresh contact, the existing 90-second fallback limit is retained, and expired authentication still disables control.

The repair was previously exercised in the private 1.1.3 submission-validation candidate but had not reached public 1.1.4. This release brings it into the maintained source with regression tests. RainPointClient remains at 1.2.2.

## Installation

Download the installable package from the [GitHub release](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.5) or matching NuGet distribution. Upload it, then update the existing RainPoint driver in Crestron Home Setup or Configure Pro. Uploading alone does not update the running driver. Public version 1.1.5 reports driver version 1.1.005.0000.

Existing account settings, room assignments and configuration formats are retained. Routine updates do not require removing and re-adding the driver. This patch does not change watering command behavior or initiate watering at startup.

## Validation and submission status

See [development history](DEVELOPMENT-HISTORY.md) for the exact workstation, merged-package and processor results. Synthetic regression tests are distinct from physical watering acceptance. The retained earlier endurance and live evidence keeps its original version, date and scope.

The initial Crestron submission is being completed separately using this public release. This release does not claim that the submission has been signed, sent or accepted. Submission-only help PDFs and correspondence are excluded from public artifacts.
