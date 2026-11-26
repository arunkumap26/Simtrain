# RimForce on-device checklist

Nothing in RimForce has touched a real Alpha EVO yet. Please run these steps once, in order, **with SimPro's RimForce profile at 100 % and RimForce's safety cap at its default 40 %**, and send back the answers (✍️) plus the two files at the end.

> Hands-off steps mean hands OFF. Everything else: hands at 9 and 3, thumbs outside the spokes. **Space / Esc / the red STOP button stop all force instantly.**

## 0. Before launching
1. In SimPro Manager create a profile **RimForce**: overall strength 100 %, rotation 900°, mechanical damper/friction/inertia ≤ 10 %. Activate it.
2. Close iRacing, irFFB, MAIRA, SimHub FFB and anything else that uses force feedback.

## 1. Self-test (no force)
1. Run `RimForce.exe --selftest` from a terminal (or a shortcut with that argument).
2. Open `%LOCALAPPDATA%\RimForce\selftest.txt`.
   - ✍️ Does it end with `RESULT: OK`? Is your Alpha EVO listed under "DirectInput enumerate"?

## 2. Reading the wheel (no force)
1. Start `RimForce.exe` normally and finish the first-time setup (skip the direction check for now if asked).
2. Top bar: the device pill should show your Simagic base and the loop rate should be ~360 Hz.
   - ✍️ Device name shown? Loop Hz? (Device page shows "max gap" too.)
3. Device page → hold the wheel straight → **Set centre here**.
4. Turn the wheel **right** by about a quarter turn.
   - ✍️ Does the gauge go to **+** about 90°? If it goes negative, switch **Invert angle**. If the angle is off by a factor (e.g. shows 45° at a real 90°), your SimPro rotation differs from the Settings value — fix it in Settings.

## 3. Force direction (hands OFF)
1. Device page → **Run direction check**. The wheel gets a tiny clockwise nudge.
   - ✍️ Result shown (ok / inverted / inconclusive) and the displacement in degrees. If "inconclusive", raise SimPro mechanical damping less / make sure hands were off, and retry.

## 4. Feel each effect (≤ 10 %)
Start at **3 %**, then try 6 % and 10 %. Press **Stop test** between effects.
| Effect | Expected | ✍️ What happened? |
|---|---|---|
| Constant ◀ | steady pull to the left | |
| Constant ▶ | steady pull to the right | |
| Spring | pulls back to centre from either side | |
| Damper | heavier the faster you turn, nothing when still | |
| Friction | even drag like sand, no buzzing at rest | |
| Vibration | 20 Hz buzz | |
- ✍️ Any stutter, pulsing, clunks, delay, or force that continues after **Stop test**?

## 5. Safety stops (low force)
Settings → keep the cap at 40 %.
1. Drills → **Free Ride**, Resistance = *Constant*, load 30 %, Load drill → Arm & start. When the force has ramped in, **let go of the wheel**.
   - ✍️ Did it stop by itself within a fraction of a second with "Hands off detected"? How far did the wheel move?
2. Arm & start again, then **Alt-Tab** to another window.
   - ✍️ Did force stop immediately ("App lost focus to …")? Which program did the message name?
   - If unrelated programs keep stealing focus, use **Keep running in background (like iRacing)** on the stop panel, then check that **Pause/Break** stops the wheel while another window is focused.
3. Arm & start again, press **Space**.
   - ✍️ Immediate stop?
4. (Optional) Arm a Corner Hold and turn towards the lock.
   - ✍️ Stopped 20° before the lock?

## 6. Torque calibration (optional, luggage scale)
Device page → section 4. Hook the scale on the rim at 3 o'clock, start **Constant ◀** at 10 %, pull the scale tangentially until the wheel is still, read the kg, enter it, Apply.
- ✍️ Reading in kg and the resulting factor.

## 7. Experimental settings
1. Settings → **Effect lapse safety** ON → Save. Device page → Constant ▶ at 5 % for ~20 s.
   - ✍️ Smooth, or pulsing / dropping out? (Then turn it back OFF.)
2. Only if arming failed with an "acquire" error: Settings → DirectInput mode **Background** → Save → retry.
   - ✍️ Which mode works?

## 8. First real drill
1. Drills → **Corner Hold**, defaults → Arm & start. Follow the grey ghost wheel.
   - ✍️ Does the force feel like the number on screen (e.g. 4.8 Nm ≈ a firm, steady pull)? Does the cockpit animation keep up (no lag)?
2. Run a **Breakpoint Test** (two hands, both directions).
   - ✍️ Your result(s).

## 9. Loop health
During any drill, glance at the top bar: the loop rate should sit near 360 Hz, and the Device page shows the worst gap.
- ✍️ Did you ever get a "Force loop stalled" safety stop? (It fires if the force loop pauses > 100 ms — seen once in the cloud on a 4-core VM under heavy screenshot load; it should never happen on a desktop CPU.)

## Send back
- `%LOCALAPPDATA%\RimForce\selftest.txt`
- `%LOCALAPPDATA%\RimForce\rimforce.log`
- Your ✍️ answers.
