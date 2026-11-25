# Decisions

Choices made while building RimForce without being able to ask, and why. Where something was ambiguous the safer option was picked.

## Platform

| # | Decision | Why |
|---|---|---|
| 1 | **.NET 10 (LTS)** instead of .NET 8 | .NET 8 support ended in November 2026; .NET 10 is the current LTS (to Nov 2028). |
| 2 | **UI = React + TypeScript in WebView2**, hosted by a small WinForms window, instead of WinForms + ScottPlot | The brief asked for a very appealing, animated UI with a live ghost wheel. Canvas/CSS give 60–144 fps animations; the same UI runs in a normal browser against the dev host, so it could be built, run and screenshotted in the cloud. If the WebView2 runtime is missing the app opens the UI in the default browser instead. |
| 3 | **Loopback WebSocket bridge** (Kestrel on `127.0.0.1`, random port, random per-launch token, Origin check) between UI and engine | One transport for WebView2, browser fallback and tests. Token + Origin check stop other web pages on the PC from connecting to the wheel. Never listens on the network. |
| 4 | **Vortice.DirectInput 3.8.3** (SharpDX.DirectInput as fallback) | Maintained, has a .NET 10 build, verified by reflection and compilation. Its `ResultCode.InputLost/NotAcquired/...` constants are wrong, so RimForce defines the correct HRESULTs; `TriggerButton = -1` is set (the default 0 makes `CreateEffect` fail). See docs/research/02. |
| 5 | **Every force is computed in software and sent as one constant-force effect** (springs, dampers, friction, pulses, kerb vibration), like irFFB/MAIRA | The Simagic firmware ignores a native spring's centre offset, native condition effects are scaled by SimPro's game sliders, and their Nm scaling is device-defined and unknowable from here. Software rendering gives exact, testable Nm and identical behaviour in the simulator. Native Spring/Damper/Friction support exists in the device class but is off. |
| 6 | **Force loop 360 Hz** (configurable 250–500) | irFFB/MAIRA use 360 Hz; MAIRA reports ~500 Hz is at the practical limit of many bases. Unchanged magnitudes are skipped but re-sent at least every 250 ms. |
| 7 | **Kerb Hold vibration in software** | Works on any base, so the drill never needs hiding. The brief's "only if periodic effects are supported" condition is therefore moot. |
| 8 | **SQLite** via Microsoft.Data.Sqlite **10.0.15** | 10.0.0 pulled a SQLitePCLRaw with a published high-severity advisory. |

## Safety

| # | Decision | Why |
|---|---|---|
| 9 | **Rule 4 (hands-off) counts only while the motor applies ≥ 0.3 Nm**, and **Lock-to-Lock Sprints may raise the threshold to 720 °/s only while the motor is braking the motion** | Taken literally, "400 °/s for 50 ms" stops the Red Bull sprint drill whenever the user is fast (the damper always opposes the motion). An earlier "ignore whenever braking" version was rejected: with reversed hardware force an inverted spring *looks* like braking, and the test suite caught that it then reached the lock. The final rule keeps the literal 400 °/s everywhere except the sprint drill, with a 720 °/s ceiling. |
| 10 | **Extra rules beyond the brief:** runaway detection (motor and motion in the same direction above 180 °/s for 25 ms → stop), overspeed fade (motor torque fades to 0 between 250 and 400 °/s when it is the one moving the wheel), UI heartbeat (UI frozen > 1.5 s → stop), WebView2 crash → stop, last client disconnect → stop | Runaway detection catches a let-go in ~50–70 ms, faster than the speed rule. Drills that legitimately move briskly with the motor (Stint corner exits, Apex Hunter, Snap Catch) raise only the runaway threshold, never the hands-off one. |
| 11 | **Force-direction check is mandatory before arming a real wheel** (hands off, +4 % for 0.2 s, checks which way it moved, also estimates inertia) | A reversed direction turns software springs into anti-springs and dampers into negative dampers. SimPro's "Reverse FFB" can flip it. The simulator does not need it. |
| 12 | **Device Test arms in a test mode capped at 10 %** of the device maximum | Lets you feel effects and run the direction check before the check has passed, without risk. |
| 13 | **Finite effect durations default OFF** (setting: "Effect lapse safety") | The brief asked for short finite durations refreshed each loop "where the API allows", configurable in case of pulsing. Research: Simagic's driver stores durations as 16-bit milliseconds and a finite effect can only be extended by restarting it, which may pulse. With it off, protection comes from the independent watchdog thread (StopAll after 100 ms stall), exclusive-mode release on exit, focus loss and the UI heartbeat. Turning it on uses 250 ms effects restarted every 100 ms. On-device check item. |
| 14 | **Cooperative level Exclusive \| Foreground** by default, Background selectable | Foreground means Windows itself unacquires the wheel when RimForce loses focus (a hardware-level stop). irFFB/MAIRA use Background; if arming fails on the real device, try Background (focus loss still stops force in software). Browser-fallback mode uses Background automatically. |
| 15 | **A user STOP (button / Space / Esc) disarms without needing acknowledgement; any automatic trip must be acknowledged** | Stops should be friction-free; faults should be noticed. |
| 16 | **Arming requires the wheel to be still (< 30 °/s) and away from the lock** | You can't be surprised by force while spinning the wheel. |

## Training logic

| # | Decision | Why |
|---|---|---|
| 17 | **Loads are % of your breakpoint** for that hand/direction (falls back to the other direction, then one-hand ≈ 55 % of two-hand, then the device max if never tested) | Matches the brief's "working sets start at 50 % of the latest breakpoint" and scales every drill as you get stronger. |
| 18 | **Breakpoint test that hits the torque cap switches to hold-to-failure** and estimates your max from Rohmert's endurance curve (`max ≈ cap / f(time)`) | Research shows a fit man's two-hand steering max (~40–55 Nm) is far above a 12 Nm base, so a pure ramp test would mostly read "> cap". Estimated values are flagged and drawn hollow on the chart. |
| 19 | **When progression would exceed the torque cap, the program grows work time ×1.1 instead** (up to ×2) | The motor can't go heavier; time under tension, one-hand work and grip variants are the overload levers (F1-training research). |
| 20 | **User torque is estimated** as commanded motor torque corrected by J·α | There is no torque sensor. While holding still it equals the motor torque. All body-load numbers are labelled as estimates. A luggage-scale calibration factor corrects the Nm scale. |
| 21 | **Body-load comparisons use published men's or women's norms, chosen by the user** (never inferred) | Same torque is ~2× harder relative to typical women's MVC. |
| 22 | **"Master intensity" (10–150 %)** multiplies every prescribed load, live, like iRacing's strength slider; caps still apply | The user asked for easy difficulty control on top of SimPro. Bindable to wheel buttons, ↑/↓, presets. |
| 23 | **Grip Variants cycle Open hand → Fingertips → Pinch** with load factors 0.85 / 0.7 / 0.6 | Weaker grips at the same torque would otherwise be unsafe or impossible. |
| 24 | **One-Hand Hold with "Both" selected alternates hands** (R, R, L, L with alternating directions) | Balanced training without a config step. |
| 25 | **Program templates rotate A (hold strength) → B (power & reaction) → C (grip & endurance)**; Stint length grows 8 → 22 min over the 8 weeks; first session is a baseline breakpoint test if none exists | The brief asks for 3–5 drills per session; the rotation covers all four goals each week. |
| 26 | **The simulator needs no safety checklist or direction check** | It cannot hurt anyone; this keeps demo mode instant. |

## Not done / limits

- Nothing has run on the real Alpha EVO. Every hardware behaviour is marked "needs on-device check" — see `docs/ON-DEVICE-CHECKLIST.md`.
- Native DirectInput condition effects (spring/damper/friction) are implemented in the device class but not used by any drill.
- No reading of SimPro's settings (Simagic has no public API; the HID route is undocumented and was rejected).
- The app does not change SimPro's rotation or strength.
