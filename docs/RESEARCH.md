# Research summary

Three research passes were done before building (full write-ups with sources in `docs/research/`). Their findings drove the design. Where sources were only available as search excerpts, the detailed docs say so.

## 1. How F1 drivers train steering strength (`research/01`)

- Teams use weighted/loaded steering wheels, isokinetic dynamometers and lots of grip/forearm endurance work with little bulk. Exact protocols and F1 steering torques are not published; one trainer quote suggests ~10 kg average and 15–20 kg peak rim force per corner.
- Exposure is the limiter: ~12 corners/min, ~950 cornering events per race, sustained 4–5 g, 90–120 min.
- Isometric strength: 80–100 % MVC, 1–5 s holds; hypertrophy/endurance: 70–75 % MVC, 3–30 s holds. Gains stay near the trained angle (±15–20°), so RimForce holds at 0/45/90°.
- Forearm extensors (ECR/ECU) fatigue first in drivers → one-hand holds, grip variants, kerb holds.
- Eccentric and eccentric-quasi-isometric work suits a capped motor (fatigue makes the cap supramaximal) → Slow Return, hold-to-failure breakpoint.
- Perturbations: expected kicks cause ~30 % less wrist displacement than unexpected ones → Snap Catch with random timing.
- Progression when the load is capped: time under tension, rest, one hand, grip closer to the hub, weaker grips, tempo, unpredictability.

## 2. DirectInput force feedback on a Simagic base (`research/02`)

- No public Simagic SDK. DirectInput 8 is what iRacing/irFFB/MAIRA use. Vortice.DirectInput 3.8.3 is the maintained C# wrapper (API names verified by reflection).
- Proven pattern: one INFINITE constant-force effect, updated with `TypeSpecificParameters | NoRestart` at ~360 Hz; skip unchanged values but refresh every 250 ms.
- Simagic quirks: durations stored as 16-bit ms, native spring ignores its centre, native conditions scaled by SimPro game sliders, only Sine periodic works, "Reverse FFB" flips direction, exclusive access only.
- Strength chain: DI magnitude × effect gain × device gain × SimPro overall strength × base peak torque. **No DirectInput path exceeds SimPro's strength.** iRacing's "Max Force" maps car Nm onto DI 100 %; RimForce's Resistance % plays the same role.

## 3. Is 12 Nm "only 8 kg"? (`research/03`)

- Arithmetic is right (12 Nm / 0.15 m = 80 N ≈ 8.2 kgf), conclusion was wrong: the joint loads are what matter.
- Static 12 Nm hold, typical man: lifting shoulder ~33 % MVC (~2 min to failure), wrist ~29 %, grip ~11–20 %, forearm twist ~75 %. Women roughly 1.5–2× those percentages.
- Seated two-hand steering max for trained drivers ~40–55 Nm on a 300 mm rim → a two-hand breakpoint test usually hits the cap → hold-to-failure + Rohmert estimate.
- Formulas used live: rim force F = τ/r; per-hand F/2; grip ≈ 1.4·F_hand/(2μ); wrist M = F_hand·0.075 m; shoulder M = F_vert·0.45 m + arm weight; power τ·ω; impulse ∫|τ|dt; Rohmert ET = −1.5 + 2.1/f − 0.6/f² + 0.1/f³ minutes.
- Real cars: road car 0–5 Nm (≤ 15 Nm emergency), GT3 ~6–12 Nm; no reliable published F1 figure.
