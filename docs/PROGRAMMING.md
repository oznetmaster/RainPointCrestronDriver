# Crestron Home programming

Each timer exposes its own programming interface. The three zones are independent. All actions retain the driver's authentication, known-idle start, pending-command and duration guards.

## Commands (10)

| Command IDs | Action |
| --- | --- |
| `startZone1`, `startZone2`, `startZone3` | Start the selected zone using its current UI duration. |
| `startZone1For`, `startZone2For`, `startZone3For` | Start the selected zone for the supplied integer `minutes`, from 1 to 120. Does not change the UI duration or any saved plan. |
| `stopZone1`, `stopZone2`, `stopZone3` | Stop that zone. |
| `stopAll` | Attempt to stop all three zones on this timer. |

The combined UI button is deliberately not a programmable toggle: automation uses explicit start and stop actions. Failed or uncertain writes are not automatically replayed.

## Read-only properties (26)

In the IDs below, `N` means 1, 2 or 3.

| Property IDs | Meaning |
| --- | --- |
| `onlineIndicator:isOnline` | Whether reported status is currently available through synchronized live feedback or the recent fallback read. Not proof of physical valve position. |
| `connection` | Cloud/observer connection state. |
| `battery` | Reported battery description, including unknown. |
| `activityState` | `Active`, `Idle` or `Unknown`. Active includes a cycle's soaking pause. |
| `activeZoneCount` | Number of active zones; `-1` if any zone is unknown or status is unavailable. |
| `zoneNState` | `Active`, `Idle` or `Unknown`. |
| `zoneNRemainingSeconds` | Device-end-time countdown, rounded up. `-1` means unavailable; idle is `0`. Zero during an active run means the end time has elapsed, not that stop is confirmed. In cyclic modes this is the current phase time. |
| `zoneNLastUsageLitres` | Last reported volume; `-1` means unknown. Historical usage remains available across connection loss. It is not instantaneous flow or a new-run counter. |
| `zoneNAlarmState` | `Active`, `Clear` or `Unknown`, based on the typed reported alarm code. |
| `zoneNAlarms` | Human-readable last reported alarm details. Check availability before treating these as current. |
| `zoneNCommand` | Most recent command/feedback description. Acceptance is not confirmation that a valve moved. |
| `zoneNMinutes` | Current UI duration for that zone's parameterless start command. |

## Events (18)

| Event IDs | Trigger |
| --- | --- |
| `zoneNStarted`, `zoneNStopped` | A known reported zone changes between idle and active. Soaking is still active and does not cause a stop/start pair. |
| `irrigationStarted` | The timer changes from all idle to at least one active zone. |
| `allZonesStopped` | The timer changes from active to all three zones known idle. |
| `zoneNAlarmRaised`, `zoneNAlarmCleared` | A known alarm state changes between clear and nonzero. Changes between nonzero alarm codes do not raise another alarm event. |
| `batteryLowReported`, `batteryRestored` | A known reported battery state changes between normal and low. |
| `statusUnavailable`, `statusRestored` | Status availability is lost after a valid baseline, or restored after that loss. |

Events require transitions between consecutive known states. Startup establishes a baseline without firing watering, alarm, battery or restoration events. Unknown readings and lost availability reset the affected report baselines; the first recovered reading establishes a new baseline rather than guessing what happened during the gap. Repeated reports, local countdown updates, accepted commands and expired countdowns do not generate watering events. Properties are updated before events are raised.

The local SDK tests cover programming registration, translations, explicit-duration routing on every zone, invalid durations, repeated/out-of-order reports, unknown readings, recovery, alarms and battery transitions. Portable countdown tests run on net472 and net10.0. Processor inspection verified all ten commands, 26 driver properties and 18 custom events in Home programming. An explicit NUnit test on 29 September 2026 passed a representative Home execution path: a selected-duration property-change event evaluated an Idle condition and invoked Stop zone 1. Temporary programming and the duration were restored, and all zones remained idle. This does not establish live firing of every custom watering, battery or alarm event; their transition logic has offline SDK coverage. See ../tests/README.md for the installed automation workflow.


## Relationship to MQTT

The custom events describe known state transitions, not individual MQTT packets. One report can update several properties and raise several transition events; repeated unchanged reports raise none. Home also exposes property-change triggers for programmable properties, including usage and remaining time. Countdown property changes can occur locally between reports. Configuration notifications refresh hub/timer/zone labels and saved-plan summaries, but currently have no dedicated programmable event; these labels and plan summaries are UI properties. RF signal changes likewise update the display without a custom event. MQTT reconnection and status availability are distinct: losing MQTT alone does not mean the timer is offline if fallback status remains current.

The dated Last recorded usage section reads cloud event history independently of the live timer report. The existing zoneNLastUsageLitres programmable property retains its documented meaning: the timer's last reported usage, which may lag or reset during a run. It must not be paired with the date shown for a separate history record. Dated history is currently a UI display; no additional programmable history event or property has been introduced.

## Timer variants

The tables describe the three-zone timer. On HTV145FRF only zone 1 entities are registered; on HTV245FRF only zones 1 and 2 are registered. Absent-zone properties, commands and events are omitted. Aggregate state, active count and Stop all use the actual timer zone count.