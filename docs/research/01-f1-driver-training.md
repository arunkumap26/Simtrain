# 01 — F1 driver steering / forearm / grip / shoulder training: research for RimForce

Research date: 2026-11-25. Audience: the RimForce implementer (Windows app driving a Simagic Alpha EVO, 12 Nm peak).

## 0. Method, limits and confidence tags

- **How this was gathered:** web search only. This session's network policy blocked direct page fetches (redbull.com, autosport.com, formula1.com, PubMed/PMC and others). Every claim below comes from search-engine abstracts or snippets of the cited page, not from a full-text read. Treat exact numbers as "verify before hard-coding", especially anything tagged [M] or [U].
- **Confidence tags** used after claims:
  - **[P]** peer-reviewed paper (abstract or excerpt seen)
  - **[O]** official team, series, manufacturer or university source
  - **[M]** media article or trainer quote
  - **[U]** forum, vendor marketing, blog, or unverified
  - **[D]** my own derivation or calculation from the cited inputs (formula shown)
- **Bottom line on gaps:** no team publishes real F1 steering-torque data in Nm. No peer-reviewed on-track EMG study of F1 drivers' forearms or shoulders was found. Several specifics in the brief could not be verified at all (see §1.10).

---

## Key numbers at a glance

| Quantity | Value | Tag | Ref |
|---|---|---|---|
| Alpha EVO family peak torque | Sport 9 Nm / EVO 12 Nm / Pro 18 Nm (continuous torque not stated) | [O] | [65] |
| Rim force at 12 Nm | ~86 N (8.7 kgf) on a 280 mm wheel; ~73 N (7.4 kgf) on 330 mm. Formula: F = T / r | [D] | — |
| Adult max isometric two-hand wheel-turning torque | Men ~109–152 Nm; women ~66–91 Nm (college students) | [P] | [62] |
| 12 Nm as % of that max | ~8–11 % (men), ~13–18 % (women), so endurance territory for two-hand steering | [D] | [62] |
| Single-joint wrist/forearm max (isometric) | Wrist flexion 12.2, extension 7.1, radial dev. 11.0, ulnar dev. 9.5 Nm (10 men); supination ~16.2, pronation ~13.1 Nm | [P] | [115][116] |
| F1 steering torque | **Not published.** A trainer estimate of ~10 kg per corner on average and 15–20 kg at most works out to roughly 13 Nm average and 20–26 Nm peaks *if* it means total rim force | [M]/[D] | [25] |
| GT3 (power-assisted) | ~6–8 Nm typical, 12–15 Nm peaks (vendor and forum figures) | [U] | [56][57] |
| Karts (no power steering) | <20 Nm sustained, ~40 Nm momentary peaks (sim-forum figure); ~10–11 Nm static (design spec of one research kart) | [U] | [59][60] |
| F1 lateral g | 4–5 g sustained in fast corners, peaks ~6 g (6.1 g at Maggotts, 2017) | [O]/[M] | [32][66][67] |
| Corners per minute | ~12 (Silverstone: 18 turns / 1:27; Suzuka: 18 / 1:31) | [D] | [70][71] |
| Cornering events per GP | ~940–950 (18 turns × 52–53 laps) | [D] | [70][71] |
| Race duration | 90–120 min; heart rate ~160–180+ bpm | [M] | [72][73][24] |

---

## 1. How F1 teams and programmes train steering, forearm, grip, shoulder and neck

### 1.1 Red Bull Racing / Red Bull Athlete Performance Center

- **Pérez trained on a weighted steering wheel.** Red Bull's own fitness feature says "while some drivers use a weight, Pérez has a steering wheel connected to weights to build specific muscles for driving" [1] [O]. No load, sets or durations are given.
- After joining Red Bull, Pérez's programme was reported as neck-focused and different from his previous teams' (ESPN interview via GPFans) [2] [M].
- **Verstappen** trains "mostly working with his own body weight", with runs, free weights and bodyweight work [1] [O]. His trainer at the time, Bradley Scanes, described neck work on an elastic-band device that applies force "in every direction", because in-car g-load cannot be replicated in pre-season [3] [M].
- **The Athlete Performance Center (Thalgau, Austria)** offers "performance profiling" with strength, power and endurance tests for upper and lower body, plus Vicon motion capture [4] [O]. No published source links a specific F1 steering test to this centre.
- **Not found:** any "Hulkenberg weighted-wheel lock-to-lock test", or any Verstappen-specific weighted-wheel test at the centre. Treat both as unverified until a primary source appears.

### 1.2 Mercedes

- George Russell's neck harness work with about 30 kg is documented in video and media [5] [M]. He describes neck training as a daily priority [5] [M].
- Mercedes' workload explainers [6] [O] list steering, shifting and frequent brake-balance changes. Mercedes' Andrew Short gives about 50 gearshifts, 17 corners and 7–8 rotary-switch changes on a qualifying lap [7] [M]. At Monaco, about 25 upshifts and 25 downshifts fit into a lap of just over 70 s [8] [M]. The Monaco hairpin needs about 180° of steering lock one way [9] [O].
- For Monaco, teams raise the road-wheel steering angle from about 14° to nearer 20° [10] [O]. A common sim-industry figure for formula cars is about 360° lock-to-lock [125] [U]; no official team figure was found.

### 1.3 McLaren

- Technogym has supported McLaren's drivers since 2004 [11] [O].
- The **"F1 Training Machine"** has a tethered helmet with adjustable resistance cables for neck loading [11][16] [O]/[M].
- The base is classic strength work: squats, deadlifts, rows and bench press, planned with periodisation. Accessories include seated rows, shrugs, lateral raises, pull-ups, push-ups and stability-ball work. Cardio and strength are often done in separate sessions [11] [O].
- Lando Norris's trainer Jon Malvern runs neck strength and neck endurance tests with strain-gauge dynamometry in the off-season [12] [M]. Norris is reported to do neck work at least five times a week [13] [M].
- McLaren's human-performance staff say the most common complaints they see are "upper quadrant" pains and injuries in the neck and shoulders, which they attribute to load [14] [O] (per search summary; verify).

### 1.4 Ferrari

- **Technogym F1 Training Machine (Alonso/Massa era).** This car-shaped rig targets the **neck and forearms**. Its **steering wheel can be loaded with weight and vibrates** to mimic track vibration [15][16][17] [M].
  - Sources disagree on the load: a nominal 25 kg [16][17], "30 kg" [15], and 2.5–25 kg adjustable (forum).
  - This is the closest published precedent for a "resisted steering wheel" device used by an F1 team.
- Technogym has supplied Ferrari for more than 20 years, including the Ferrari Driver Academy (FDA). Check-ups lead to individual programmes [18] [O].
- FDA camps cover physical and mental training [19] [O]. No published FDA steering-specific protocol was found.

### 1.5 Hintsa Performance

- Hintsa uses a holistic model: physical training, sleep and recovery, nutrition, psychology and physiological monitoring. Full-time coaches with sports-science degrees travel with drivers [20] [O].
- Hintsa claims its drivers have won 19 drivers' titles from 2010 to 2024 [20] [U, marketing].
- Autosport credits the late Aki Hintsa with changing how F1 driver performance is managed [21] [M].
- It publishes no steering or forearm protocols.
- **Implication:** RimForce should treat readiness, recovery and load monitoring as part of the product, not only the drills.

### 1.5b General trainer practice

- Daniel Ricciardo's trainer describes an emphasis on **shoulders and forearms for steering**, alongside heavy core and posterior-chain work [123] [M].
- Common practitioner drills include rotating a heavy plate held at arm's length like a wheel [32] [M]. These are unquantified.

### 1.6 Formula Medicine (Dr Riccardo Ceccarelli, Viareggio)

- Formula Medicine has monitored driver heart rate since 1989, across more than 30 drivers and 100+ races. It reports race averages above 170 bpm [24] [M].
- Its "Mental Economy" training is cognitive work done under physiological load, with performance, brain activity and heart rate analysed "like telemetry" [22][23] [M].
- Ceccarelli describes a brain-scan comparison of 12 drivers and 12 controls: drivers showed lower cerebral blood flow for the same performance [23] [M]. This is not peer-reviewed.
- A trainer quote in an Autosport feature on a Formula Medicine visit gives: average shoulder/arm load about 10 kg per corner, maximum 15–20 kg depending on the corner, and up to about 30 kg on the neck [25] [M].
  - This is the only "steering load" number from a team-side practitioner that I found.
  - The units are ambiguous: total rim force or per arm is not stated.
- Formula Medicine ran a physical-development workshop for F1 Academy [26] [O].

### 1.7 Rally precedent for drill structure (Red Bull, Thierry Neuville)

- Neuville balances on an exercise ball, moves one leg, and holds a weight at steering height, turning it like a wheel against elastic bands. Dose: **4 sets × 30 s** [30] [O].
- This is useful as a concrete example of "steering under instability" programming.
- IndyCar drivers do seated wheel-grip holds with a weight plate turned like a steering wheel [124] [O/U].

### 1.8 Neck training dose (context only: RimForce cannot load the neck)

- Typical practice is **10–15 min per session, 2–3 times a week** (coach who worked with Vettel) [31] [M]. Some drivers do much more, e.g. Norris at 5 times a week [13].
- Ricciardo's warning on progression: "Your neck's fragile, so you don't want to start by putting 10 kg plates on the side of your head" [32] [M].
- Neck load estimate: about 7 kg (head + helmet + HANS) × 5 g ≈ 35 kg in a high-speed corner [32] [M]. A review gives about 26 kg at about 4 g [87] [P/U].

### 1.9 Isokinetic steering wheels (IsoMed 2000, Biodex)

- **No source found** for an IsoMed 2000 or Biodex *steering-wheel attachment*.
  - The IsoMed 2000 is a single-joint dynamometer for arms and legs, with static (isometric) and dynamic (isokinetic) modes and a trunk module [33] [O].
  - Biodex and Cybex devices have published forearm pronation/supination and wrist protocols (see §3.8).
- **The closest academic analogue to "isokinetic steering"** is a motor-driven steering wheel that rotates at a fixed angular speed to quantify continuous steering torque. It borrows isokinetic-dynamometer logic to remove inertia effects (12 adults) [34] [P]. This is essentially what RimForce can do with a velocity-controlled mode.
- Instrumented steering wheels split into 3 sections, each on a 6-axis load cell, can separate left-hand and right-hand forces. The two hands were found to contribute unequally [35] [P].

### 1.10 Reaction and cognitive training under load

- **Batak:** Jenson Button set a Guinness record of 58 lights in 30 s [36] [O].
  - A 60-s Batak format was used in a 2010 MSA Team UK challenge [37] [M].
  - No standard F1 Batak norm was found. Reaction-time "league tables" online lack sources.
- **NeuroTracker (3D multiple-object tracking):** the vendor claims use by F1, NASCAR and MotoGP drivers [38] [U].
  - An independent critical systematic review (Vater, Gray & Holcombe, Psychon Bull Rev 2021) concluded the abilities underlying object tracking "are not those advertised by the Neurotracker manufacturers".
  - That review also found no pre-registered studies, and only 3 studies of far transfer, 2 of which found no effect [39] [P].
- **Light-based reactive training RCT in racing drivers** (Horváth et al., Sports Med Open 2022): 24 car racing drivers, 6 weeks (12 training vs 12 control).
  - Cognitive testing (Vienna Test System) was done at rest and during moderate cycling.
  - Reaction times improved across sessions and some cognitive measures improved.
  - The load did not change heart-rate measures [40] [P].
  - This is the best motorsport evidence for "reaction under physical load" drills.

### 1.11 F1 Academy and junior single-seaters

- **F1 Academy / More than Equal:** 35 drivers tested on 40 physical and cognitive variables.
  - Top-10 % drivers had neck strength about 2× that of the general female population.
  - F1 Academy cars reach about 2.0 g lateral [27][28] [O].
  - A 2026 rookie camp is supported by Loughborough University [29] [O].
- **F2/F3 steering is manual rack-and-pinion, with no power steering** [41][44] [M].
  - For the new F2 car, the FIA set out to make steering weight acceptable to "small drivers… top drivers and female drivers", and consistent from F4 to F2. It used suspension geometry rather than power steering [41][42] [M].
  - In female-driver F3 tests, complaints were about brake effort, not steering effort [41] [M].
- Lando Norris said F2 was "much harder physically than F1" [43] [M], which fits unassisted steering.
- **Formula E Gen3 has no power steering.** Drivers call it "brutally heavy".
  - Sam Bird broke his hand from the **whip of the steering wheel** in a low-speed crash.
  - Manufacturers then tested a revised steering system and rounder wheels [45][46] [M].
  - This is a directly relevant **safety precedent for "snap" or perturbation modes** on a direct-drive wheel.

### 1.12 Karting (no power steering)

- **Kart forearm EMG, 2025** (J Funct Morphol Kinesiol 10(2):190): 11 trained kart drivers, two sessions on a 680 m track; ECR, ECU, FCR and FCU measured.
  - The median-frequency change in right ECR was significant (p = 0.018).
  - The snippets conflict on the direction of change: the table shows about −0.58 % while the text says "increase". Verify before use.
  - Conclusion: even short bouts of kart driving measurably change forearm neuromuscular activation [77] [P].
- **Portsmouth case study, 2025** (Applied Sciences; Mills, Blackmore, Wakefield, Neupert): one ex-professional kart driver, bilateral FDS EMG plus 20 Hz GPS.
  - The forearm on the inside of the corner was more active.
  - Peak activity reached about 300 % of an MVC reference in session 2.
  - Higher forearm activity may reflect steering effort that could be optimised [79] [P].
  - A 2019 Portsmouth on-track test found a driver "barely using his left arm on right-hand corners" [80] [O]. This is a reason to track **left/right asymmetry**.
- Handgrip force did **not** deteriorate after a 45-min kart competition session (Durand et al. 2013, abstract) [81] [P].
- Kart race heart rate averaged 168.8 bpm, versus 140.9 bpm in solo driving and 75.1 bpm at rest (Matsumura et al.) [82] [P].
- A sham-controlled RCT of elbow manipulation in kart drivers (J Clin Med 2026) found no benefit for EMG recovery, grip or steering. Its EMG was recorded only in static lab contractions [78] [P].

---

## 2. Real steering loads, cornering exposure and EMG/fatigue evidence

### 2.1 F1 power steering: what is known

- F1 uses **hydraulic, hydro-mechanical power-assisted steering (PAS)**.
  - Moog states an FIA cost-cutting rule in 2002 banned electronic control of PAS.
  - Moog's E243 mechanical PAS valve (2005) became the common solution, with assistance curves "shaped" in hydraulic ports in place of software lookup tables [47] [O, manufacturer].
  - The valve weighs 27.5 g and is rated to 280 bar [48] [O].
- **No team or FIA document giving F1 steering torque in Nm was found.** Circulating figures:
  - "10–25 Nm" (sim forum) [56] [U]
  - "20–40 Nm peak" attributed to unnamed engineers [55] [U]
  - A forum claim that F1 uses electric PAS since 2014 contradicts Moog's account of the 2002 ban [56]
- **Trainer estimate:** about 10 kg average and 15–20 kg maximum "per corner" on shoulders and arms [25] [M]. Converted [D]:
  - With a grip radius of about 0.135 m and the figure read as total tangential rim force: about 13 Nm average and about 20–26 Nm peaks.
  - If the figure is per arm, double it. The source does not say.
- **What failure looks like (qualitative):**
  - Sargeant (Dutch GP 2023): after a PAS and hydraulic failure he "simply couldn't steer the car" [49] [M].
  - Verstappen (Bahrain 2022) on heavy steering: "Oh my god it's almost locked" [50] [M].
  - Palmer on Norris's fading PAS: steering "too heavy in the fast corners" without assistance [51] [O].
- **2017 rules** brought about 25 % wider tyres and more downforce. PAS capacity had to rise "by a significant margin" without hurting feel. Peak cornering approached 5 g or more [52][53] [M].
- **Before PAS:** Ligier is usually credited with the first F1 PAS in 1988, and it spread through the 1990s as aero loads rose. No measured pre-PAS steering forces were found; claims such as "FW14B heavy steering, Mansell could muscle it" are anecdotal [122] [U].

### 2.2 Other categories (low confidence unless stated)

- **GT3 (power-assisted):** about 6–8 Nm typical and 12–15 Nm peaks [56][57] [U].
  - A forum estimate for a GT car *without* PAS: 15–20 Nm in corners and 27–28 Nm on kerbs [58] [U].
  - Fanatec says professionals often train with direct-drive bases at 8–12 Nm [57] [U].
- **Karts:**
  - Sim-derived forum figure for KZ: about 40 Nm peaks lasting a fraction of a second, under 20 Nm sustained in fast corners [59] [U].
  - A drive-by-wire research kart lists 10.3–11.1 Nm steering-shaft torque (design spec, 70 kg load) [60] [P, but not an on-track measurement].
- **Formula SAE:** static steering effort of about 4–7.5 Nm [61] [U/O].
- **Historic single-seater (Lola T332, no PAS):** about 5.5–8.5 Nm (forum) [54] [U].

### 2.3 Human capacity compared with 12 Nm

- **Two-hand isometric wheel-turning torque** (HFES 1992, 125 men and 125 women): men about 109–152 Nm, women about 66–91 Nm, depending on the measure (3-s average, peak, or ramp-to-peak) [62] [P].
- Handwheel studies (35–70 cm diameters): women produce about 45–51 % of men's torque. Torque rises about 90 % from the smallest to the largest wheel. Recommended (not maximal) limits are 26.4–57.6 Nm [63] [P].
- Steering torque was greatest with the seat at **50 % of arm length** [64] [P].
- **So 12 Nm with two hands at the rim is only about 8–18 % of maximal steering torque for typical adults [D].** It cannot deliver strength-zone loading for bimanual steering at full rim radius.
- **Single-joint forearm and wrist actions are a different matter.** 12 Nm is near or above maximal [D]:

  | Action | Max (Nm) | 12 Nm as % of max |
  |---|---|---|
  | Wrist extension | 7.1 | ~170 % |
  | Wrist flexion | 12.2 | ~98 % |
  | Ulnar deviation | 9.5 | ~126 % |
  | Radial deviation | 11.0 | ~109 % |
  | Pronation | 13.1 | ~92 % |
  | Supination | 16.2 | ~74 % |

  - Wrist values: Delp et al., 10 men [115]. Forearm values: O'Sullivan & Gallwey [116]. Both [P].
  - The full 12 Nm reaches these joints only in a **coaxial** setup, e.g. a hub handle with forearm rotation aligned to the wheel axis.
  - In a rim grip with the forearm fixed, joint moment ≈ T_motor × (d_joint / r_grip). Example: 12 Nm × 0.07 m / 0.14 m ≈ 6 Nm at the wrist [D, simple statics].

### 2.4 Cornering exposure (for shaping "stint" drills)

- **Lateral g:**
  - Fast corners: 4–5 g sustained, peaks near 6 g [32] [M].
  - Vettel logged 6.1 g at Maggotts in 2017; Copse is quoted at about 5–5.5 g [67] [M].
  - Mercedes: "over 5 g at the apex", the highest lateral load on the 2024 calendar [66] [O].
  - Suzuka: Turn 1 over 5 g; only about 1.2 km of the 5.807 km lap is straight; T10 about 3.5 g [68] [O].
  - Up to about 6 g at Eau Rouge, Copse and Istanbul T8 [69] [U].
  - 130R estimates conflict: about 4 g versus over 5 g [U].
- **Sustained duration per corner: not published.** A forum quote of "5 lateral g sustained for 14–18 of 23 seconds" is unverified [U].
  - Derive real profiles from public timing telemetry. FastF1 exposes Speed, RPM, gear, throttle, brake, DRS and X/Y position (1/10 m), but **no steering or g channels**. Lateral g would have to be estimated from path curvature × speed² [121] [O/D].
- **Corners per minute [D]:**
  - Silverstone: 18 turns, lap record 1:27.1 → about 12.4 per min [70].
  - Suzuka: 18 turns, 1:30.97 → about 11.9 per min [71].
  - A GP is about 52–53 laps, i.e. about 940–950 cornering events.
- **Race context:**
  - Races last 90–120 min, with sweat rate about 1 L/h [72] [M].
  - Heart rate is quoted at 160–180+ bpm (182 bpm average in one cited study) [73] [M]. Formula Medicine reports over 170 bpm [24] [M].

### 2.5 EMG and fatigue studies relevant to steering muscles

| Study | Population / task | Key findings | Tag |
|---|---|---|---|
| **Pick & Cole 2006**, ASME J Dyn Syst Meas Control 128(4):960–968 | 1 subject, right-arm EMG regression | Steering torque mainly from **middle and anterior deltoid, sternal pectoralis major, triceps long head**. Isometric rim force predicted from EMG | [P] [74] |
| 2024 Sensors (double lane change), citing Pick & Cole | Road driving manoeuvre | Anterior/middle deltoid, clavicular pec and triceps long head correlate with steering torque (multiple R ≈ 0.9). Another citing paper instead names scapular deltoid, infraspinatus, latissimus, subscapularis and triceps, so the **muscle set depends on the task** | [P] [74] |
| O'Sullivan & Gallwey 2002, J Electromyogr Kinesiol 12(4):275–285 | Max pronation/supination | **ECRB highly active in both directions**: a stabiliser in pronation, prime mover in supination. Forearm angle affects supination torque more | [P] [116] |
| **Backman et al. 2005**, J Strength Cond Res 19(4):777–784 | 9 international open-wheel, 9 rally drivers, 10 controls | Open-wheel drivers stronger than rally drivers in neck lateral flexion and extension (p < 0.01). Open-wheel drivers stronger than controls in **neck, grip, shoulder flexion** and leg extension. Recommendation: open-wheel drivers train neck especially, plus all groups evenly | [P] [75] |
| Backman 2005 MSc thesis (Jyväskylä) | 9 kart drivers; 30-min simulated full-speed race (5 drove) vs rowing | Max isometric strength (neck, shoulder, grip, leg, calf) fell more after driving than after rowing. Significant driving-vs-rowing differences for **shoulder** and plantar flexors. Trapezius EMG reportedly up about 40 % (fragmentary; verify) | [P/O] [76] |
| Kart EMG 2025, JFMK 10(2):190 | 11 drivers, 680 m track | Short driving bouts change forearm (ECR/ECU/FCR/FCU) activation. Right ECR median-frequency change p = 0.018 (direction unclear) | [P] [77] |
| Portsmouth case 2025 | 1 driver, FDS bilateral | Inside-corner forearm more active; peaks about 300 % MVC reference | [P] [79] |
| Durand et al. 2013 | Kart, 45-min competition | No handgrip-force decline | [P] [81] |
| **Marina, Torrado & Bescós 2021**, IJERPH 18(15):7926 | 21 road-racing motorcycle riders, braking-like grip | **Recovery length mattered more than intensity.** 5 s on / 5 s off at 30 % MVC drove frequency decline; 10 s at 50 % with 60 s rest did not. **Isometric MVC fell about 53 %** (verify). Authors recommend **5–10 s recoveries** for forearm training and testing | [P] [83] |
| Marina et al. 2011, J Electromyogr Kinesiol 21:255–261 | 24-h motorcycle race | Significant MVC decrease without EMG median-frequency decline | [P] [84] |
| McKnight et al. 2019, Med Sci Sports Exerc | F1, IndyCar, NASCAR, IMSA drivers | Isometric neck strength highest in F1 and IndyCar | [P] [85] |
| Tyler et al. (BJSM, F1 review plus coach interviews) | 25 studies + 3 F1 performance coaches | Neck isometric about 49 kg extension, 39 kg lateral flexion, 23 kg flexion; core temperature +1.7–2.5 °C (from a blog summary; verify) | [P/U] [86] |
| Halo-type structure study | Open-cockpit driver(s), qualifying | Neck/shoulder EMG; right-arm-dominant steering pattern noted (small sample; summaries disagree on n) | [P] [91] |
| Simulated 1-h driving | Non-racing | Steering-grip muscle activity fell as drowsiness rose | [P] [92] |

- **Not found:** an on-track F1 EMG study of ECR, ECU, FDS, deltoid or trapezius.
- The reviews state the formula-driver evidence base is small and heterogeneous [88][89][90] [P].
  - A brief review (Appl Sci 2026) cites a 2013 study of 18 open-wheel drivers in which experienced drivers had about 18–25 % better grip strength [88] [P, verify].
  - A scoping review concludes that "grip strength and upper limb isometric endurance are critical" [90] [P].

---

## 3. Training science for designing drills on a torque-limited device

### 3.1 Isometric dosing (the core of RimForce)

- **Lum & Barbosa 2019** (Int J Sports Med 40(6):363–375) [93] [P]:
  - **Maximal strength:** 80–100 % MVC, **1–5 s** holds, **30–90 s total contraction per session**, at multiple or targeted joint angles.
  - **Hypertrophy:** 70–75 % MVC, **3–30 s** holds, **>80–150 s total per session**, for **>36 sessions**.
  - Transfer to dynamic performance is best when position, intent and force-time demands match the target task.
- **Oranchuk et al. 2019** (Scand J Med Sci Sports 29(4):484–503) [94] [P]:
  - **Long muscle length** training gives more hypertrophy (0.86–1.69 %/week) and better transfer to dynamic performance.
  - **Ballistic intent** improves rate of force development (RFD; 1.04–10.5 %/week).
  - **≥70 % MVIC** is needed for tendon adaptations.
  - Hypertrophy and maximal force improved regardless of training intensity.

### 3.2 Angle specificity

- Kitai & Sale 1989: isometric training raised strength **at the training angle and the two adjacent tested angles only**, through a neural mechanism [95] [P].
  - Secondary summaries quote the transfer band as "±10–20°" or "within 15°". The exact spacing was not confirmed from the primary text.
- **Short-length** training is clearly angle-specific and neural. **Long-length** training adds muscle mass and transfers better dynamically (Noorkoiv et al. 2014) [96] [P].
  - Training at 90° vs 50° knee flexion shifted the peak-torque angle toward the trained length (Alegre et al. 2014) [96] [P].
- Spreading isometric training over **four angles** still gave bigger isometric than dynamic gains (18.0 % vs 13.1 %), with some residual angle effect [97] [P].
- **Design consequence [D]:** cover the steering range with hold angles spaced about **20–30° apart** (e.g. 0, ±30, ±60, ±90°, plus ±150–180° for hairpins). Bias some volume toward the **extreme angles**, where muscles are at long length.

### 3.3 Holding vs pushing isometrics; eccentric quasi-isometrics

- **Pushing ("overcoming")** = maximal effort against an immovable resistance. **Holding ("yielding")** = resisting a load to keep a position.
  - A 2024 preprint meta-analysis found that fatigue and activation trends differ between the two, inconsistently. It is not peer-reviewed [100] [U].
  - On a motor:
    - *pushing* = stiff position-hold, where the user pushes and the device measures
    - *holding* = constant torque that the user resists
- **Eccentric quasi-isometric (EQI)** (Oranchuk et al., J Strength Cond Res 2019) [98] [P]:
  - Hold a submaximal yielding isometric **until fatigue causes lengthening**, then **maximally resist through the range**.
  - Effective for muscle-tendon morphology and performance, with low injury risk and less DOMS. Little specificity to high-velocity or power movements.
  - Authors' workshop guidance: about 40–60 s per set, roughly 30 s isometric to failure then 10–30 s eccentric [99] [U, presentation].
  - **EQI fits a capped device well:** fatigue lowers the user's capacity until a fixed 12 Nm becomes supramaximal.

### 3.4 Isokinetic and velocity specificity

- Strength gains are greatest at or near the **training velocity** (Behm & Sale, Sports Med 1993).
  - The **intent** to contract ballistically drives high-velocity adaptations even without fast movement [101] [P].
  - In isokinetic studies, peak torque usually improves most at the trained velocity. Velocity specificity is **weaker for eccentric** training (Morrissey et al. 1995) [102] [P].
- **Design consequence [D]:** offer several speed bands, e.g. slow 30°/s, moderate 90°/s, fast 180–300°/s.
  - Real F1 steering rates were not found. Let users import their own sim steering-velocity logs to set targets.

### 3.5 Eccentric emphasis

- Douglas et al. 2017 (Sports Med 47:917–941; 40 studies): **eccentric loads not limited by concentric strength** beat traditional training for strength, power and speed. Gains are largely mode-specific [103] [P].
- 2025 systematic review using isokinetic dynamometers: eccentric-only training raised eccentric MVC by about **27 % vs 10 %** for concentric-only (Hedges' g 1.51). The advantage grows with longer programmes [104] [P].
- **Design consequence [D]:** true eccentric overload (motor overpowering the user) is **impossible for fresh two-hand rim steering at 12 Nm**. It is feasible in:
  - one-hand work
  - hub or wrist-isolated grips (§2.3)
  - pinch or open-hand grips
  - a pre-fatigued state (EQI)

### 3.6 Endurance, time under tension and grip endurance

- **Low-load training to failure:** hypertrophy is similar to high-load, but **1RM strength gains are smaller** (Schoenfeld et al. 2017, ≤60 % vs >60 % 1RM). Isometric-strength gains did not differ [106] [P].
  - With low loads, effort or proximity to failure matters more than volume (Lasevicius 2022, cited in [106]).
- High-repetition training (20–28RM) is recognised for local muscular endurance (Campos et al. 2002) [108] [P].
- **Sustained handgrip endurance reference** (West et al. 1995) [110] [P]. Useful to sanity-check RimForce endurance tests:

  | Intensity | Women | Men |
  |---|---|---|
  | 30 % MVC | ~400 s | ~364 s |
  | 50 % MVC | ~205 s | ~139 s |

- **Intermittent protocols** [109] [P]:
  - The climbing-research **7 s on / 3 s off** structure is standard.
  - Giles et al. 2019 ran tests to failure at 45, 60 and 80 % MVC to model critical force.
  - Giles et al. 2021 (IJSPP 16(7):942–949) introduced a single **4-min all-out 7:3 test**. **Critical force = mean of the last 3 contractions** (validated 2024).
  - Forearm-fatigue duty cycle: 5 s on / 5 s off at 30–50 % MVC to failure [83][110] [P].
- **Isometric handgrip for blood pressure:** typically 4 × 2 min at 30 % MVC with 1-min rests (registered protocol). Meta-analyses show resting-BP reductions [117] [P].

### 3.7 Progressive overload when maximum load is capped

Each lever below is tied to its evidence; tags mark evidence vs my inference.

1. **More time under tension or volume** toward the hypertrophy dose (>80–150 s per session) [93][106] [P].
2. **Shorter rest and higher density**: shorter recoveries produce more forearm fatigue [83] [P].
3. **Unilateral work** roughly doubles the relative load [D].
   - Cross-education: the untrained limb gains about **7.8 %** (about 35 % of the trained limb's gain) [112a] [P].
   - Cirer-Sastre 2017 pooled effect size 0.56; the effect depends more on volume than load [112b] [P].
4. **Lever-arm progression**: holding closer to the hub raises the required hand force, since F = T / r [D].
5. **Eccentric tempo and EQI** [98][103] [P].
6. **Longer muscle length** at extreme angles [94][96] [P].
7. **Ballistic intent** on pushing isometrics, for RFD [94][101] [P].
8. **Grip-limited variants** (open hand, pinch, fingertips, thicker rim) make grip the limiter [D].
9. **Pre-fatigue or supersets** [D].
10. **Unpredictable perturbations** and **cognitive dual-task** loading [113][40] [P].

### 3.8 Deload

- A Delphi consensus (Bell et al., Sports Med Open 2023) defines deloading as a planned reduction of training stress to cut fatigue and aid recovery and readiness [111] [P].
- Practitioner-reported pattern: **5–7 days every 4–6 weeks**, cutting volume and/or intensity, with wide variation. This comes from Bell et al.'s 2022 coach interviews; treat it as practice, not proven [111] [P].

### 3.9 Perturbation and reaction training for wrist stability

- Wrist perturbation during a tracking task (Forman et al., Sci Rep 2020):
  - Extensor co-contraction ratios were higher across all conditions.
  - **Expected perturbations produced anticipatory activation and about 30 % less wrist displacement** than unexpected ones [113] [P].
- **Higher grip force → less angular displacement** and shorter time-to-peak displacement under radial/ulnar perturbations (Mannella et al., PeerJ 2022, p ≤ 0.001) [114] [P].
- No wrist-perturbation *training* RCT was found in athletes. Proprioceptive-training RCTs exist only as abstracts or registrations. Evidence that the practice transfers is weak, though the mechanism is plausible.
- **Design consequence [D]:** mix cued and uncued "kerb strike" impulses. Score peak displacement and time to re-centre. Vary grip instructions.

### 3.10 Safety evidence that should shape the app

- **Isometric pressor response:** the acute blood-pressure rise is larger in **men, middle-aged and older adults, hypertensive people, and at higher intensities** (2024 meta-analysis) [117] [P].
  - Add a health screen, breathing cues (no breath-holding), and conservative defaults.
- **Lateral elbow pain:** gripping provokes it. Graded isometric and eccentric loading both help, with no clear winner and undefined dosing [118] [P].
  - Track elbow/forearm pain and auto-reduce load.
- **Steering whip or kickback** broke a Formula E driver's hand [46] [M].
  - Keep thumbs outside the spokes for snap drills, cap impulse amplitude, and use soft limits plus an emergency stop.
- **Isolated wrist and forearm setups can exceed 100 % MVC at 12 Nm** (§2.3) [D].
  - These need calibrated caps (start ≤50 % of the user's measured max), slow ramps, and no impulsive perturbations.

---

## 4. Training modes and resistance profiles a programmable motor can deliver

**API mapping:** standard DirectInput force-feedback effects include constant force, ramp, periodic waveforms (square, sine, triangle, sawtooth), condition effects (spring, damper, inertia, friction) and custom force [120] [O]. Whether the Simagic SDK or driver exposes faster position or torque control is outside this document and should be checked separately.

| # | Mode (working name) | Motor behaviour | Training target and evidence | Suggested dose [D] |
|---|---|---|---|---|
| 1 | **Apex Hold** (yielding isometric) | Constant torque at a target angle; hold within ±3–5° | Angle-specific isometric strength/endurance [93][95] | 3–30 s holds; total ≥80–150 s per session; angles every 20–30° |
| 2 | **Wall Push** (overcoming isometric) | Stiff spring/position lock; user pushes max for 3–5 s with ballistic intent | Max strength, RFD [93][94]. **Breakpoint**: if user exceeds 12 Nm, the wheel yields → flag "capped" | 3–5 × 3–5 s; 30–90 s total per session |
| 3 | **Isokinetic Sweep** (velocity-controlled) | Damper (torque ∝ speed) or closed-loop speed cap through lock-to-lock | Velocity-specific strength [34][101][102] | 3 speed bands; 6–12 sweeps each |
| 4 | **Yield** (eccentric / EQI) | Torque ramps above the user's hold capacity; wheel rotates at controlled slow speed (e.g. 10–30°/s) | Eccentric strength, tendon [98][103][104]. Only overloads in one-hand, hub, pinch or fatigued setups | 40–60 s per set (≈30 s hold + 10–30 s eccentric) |
| 5 | **Kerb Strike** (perturbation) | Random-timed torque impulses/steps (L or R) during a hold; cued vs uncued | Reactive wrist stiffness [113][114]; kickback awareness [46] | 10–20 strikes per set; amplitude capped |
| 6 | **Stint** (lap-shaped endurance) | Track-derived sequences: L/R load pulses at about 12 corners/min, high-load blocks (e.g. Maggotts–Becketts as fast alternations), straights as rest | Specific endurance [70][71][83] | 5–30 min "stints"; progress by laps, density, and lowered recovery |
| 7 | **Critical Torque** test | 4-min all-out 7:3 pushes (adapted from climbing critical-force) | Endurance threshold [109] | Valid only where the user cannot exceed 12 Nm (one hand, hub, pinch) |
| 8 | **Line Follow** (tracking under load) | Moving target angle on screen plus torque disturbances; RMS error scored | Precision under fatigue [92] | 1–3 min blocks, fresh vs post-stint |
| 9 | **Lights** (reaction under load) | Visual cue → steer to target or press wheel button while holding torque | Reaction under physical load [40][36] | 30–60 s blocks |
| 10 | **Tremor / road** (vibration) | Periodic effect overlaid on holds | Ferrari/Technogym machine used vibration [16][17]; evidence for benefit not found | Optional |

### Benchmarks a driver could track

Literature-anchored where possible; all thresholds are [D] unless cited.

- **Capped max test:** pass/fail at 12 Nm in two-hand, one-hand and hub configurations. Report the highest configuration passed.
- **True isolated-joint max** (hub pronation/supination, wrist deviation): this is where 12 Nm is not limiting [115][116]. It enables real %MVC prescription.
- **Time to failure at fixed torque:** e.g. 12 Nm two-hand at 0°, and 6 Nm one-hand. Compare against handgrip curve shapes [110].
- **Critical torque** (4-min 7:3) [109].
- **Fatigue index:** % drop in max push pre→post stint. Literature reference points: about 53 % MVC loss after intermittent grip protocols [83]; larger post-driving than post-rowing strength loss [76].
- **Left/right asymmetry** in all tests [35][79][80].
- **Perturbation score:** peak displacement and time to re-centre, cued vs uncued [113].
- **Tracking RMS error** fresh vs fatigued.
- **Off-device reference:** handgrip dynamometer; neck strength (not trainable on the wheel) [75][85].

---

## 5. Design implications for RimForce

1. **12 Nm is an endurance and precision device for two-hand steering.** It is a strength device only in isolated or leveraged setups.
   - At the rim it is about 8–18 % of adult maximal steering torque [62].
   - Default programming should be time-under-tension, density and specific-endurance work: Apex Hold, Stint, Critical Torque.
   - Strength-zone work (≥70–80 % MVC [93][94]) needs one-hand, hub-grip, pinch-grip or fatigued (EQI) setups. The app should know the grip setup (two-hand rim / one-hand rim / hub / pinch) and the rim radius, and convert torque to estimated hand force and joint moment [D].
2. **Calibrate per user and per setup, then prescribe in %MVC.**
   - Use a Wall Push max test (§4, mode 2) that flags a "capped" result.
   - Where capped, prescribe by time-to-failure or RPE instead of %MVC.
3. **Use the published dosing defaults** [93][94][95][96]:
   - Strength: 1–5 s holds, 30–90 s total.
   - Hypertrophy/endurance: 3–30 s holds, ≥80–150 s total, at least 36 sessions (about 12 weeks at 3 per week).
   - Hold angles spaced 20–30°, with extra work at extreme angles.
4. **Make lap-shaped stints the signature mode.**
   - Derive from FastF1 position+speed (curvature → lateral-g proxy) [121]: about 12 corners/min [70][71], left/right alternation, 1–5 s pulses, about 950 events per "race".
   - Real per-corner torque is unpublished. Scale relative to the user's calibrated max, not to "F1 Nm".
   - Present any absolute F1 number as an estimate, e.g. about 13 Nm average and about 20–26 Nm peaks from a trainer quote [25].
5. **Use short recoveries for forearm endurance.**
   - Duty cycles of 5:5 or 7:3; rests of 5–10 s drive specific fatigue [83][109].
   - Longer rests (≥60 s) for strength blocks.
6. **Progress when capped** using the levers in §3.7: TUT, density, unilateral, lever arm, EQI, long-length angles, ballistic intent, grip constraints, perturbations, dual-task.
   - Schedule a 5–7-day deload every 4–6 weeks and auto-deload on pain or performance drop [111].
7. **Build a safety layer into the product:**
   - health/BP screen and breathing cues [117]
   - elbow-pain tracking [118]
   - thumbs-out rule for snap drills
   - impulse caps, an E-stop and soft limits [46]
   - stricter caps for hub or wrist modes, where 12 Nm can exceed 100 % MVC [115][116]
   - ramped onset for every effect
8. **Track asymmetry and fatigue, not only peak numbers.** Report L/R balance, fatigue index, perturbation recovery and tracking error [35][79][83][113].
9. **Make cognitive-under-load drills simple and honest.**
   - Light/reaction tasks under load have motorsport RCT support [40].
   - Avoid claiming NeuroTracker-style far transfer [39].
10. **Neck, legs and cardio are outside the device's scope.** Optionally remind users of typical neck dosing (10–15 min, 2–3 per week [31]) and McLaren-style separation of cardio and strength [11].
11. **Claims to avoid:**
    - "Used by Red Bull/Hulkenberg lock-to-lock test" (not verified)
    - "Same as IsoMed steering attachment" (not found)
    - "Real F1 steering torque is X Nm" (unpublished)

---

## References

All accessed 2026-11-25 via search abstracts or snippets; see §0.

1. Red Bull — Peak performance: the fitness secrets of F1 drivers. https://www.redbull.com/gb-en/formula-one-drivers-fitness-plans
2. GPFans (ES) — Checo Pérez on his Red Bull training. https://www.gpfans.com/es/f1-noticias/82643/checo-perez-habla-sobre-el-duro-programa-de-entrenamiento-casi-me-rompo-el-cuello/
3. EssentiallySports — Performance coach talks about Max Verstappen's fitness regime. https://www.essentiallysports.com/f1-news-performance-coach-talks-about-max-verstappen-and-his-fitness-regime
4. Red Bull Performance — Services. https://www.redbullperformance.com/en/services/ ; Salzburg Research — Red Bull Athlete Performance Center. https://www.salzburgresearch.at/en/partner/red-bull-athlete-performance-center-en/
5. Dmarge — George Russell neck training. https://www.dmarge.com/?p=367517 ; Sportskeeda — Russell hangs 30 kg from head. https://www.sportskeeda.com/f1/news-watch-mercedes-george-russell-hangs-30-kg-head-part-training-2022-f1-season
6. Mercedes-AMG F1 — Explained: Driver Workload During a Lap. https://www.mercedesamgf1.com/news/explained-driver-workload-during-a-lap
7. Popular Science — The F1 steering wheel requires fighter jet components. https://www.popsci.com/f1-racing-steering-wheel-complicated/
8. Pitpass — A driver's workload over a lap of Monaco. https://www.pitpass.com/69723/A-drivers-workload-over-the-course-of-a-lap-of-Monaco
9. Mercedes-AMG F1 — How to approach a lap of Monaco. https://www.mercedesamgf1.com/news/how-to-approach-a-lap-of-monaco
10. Formula1.com — Ferrari SF15-T Monaco steering modifications. https://www.formula1.com/en/latest/article/ferrari-sf15-t-monaco-steering-modifications.2VI1MigPJ4tIvcrdtRNiiw
11. McLaren/Technogym — How to train like the McLaren Formula 1 team. https://www.mclaren.com/racing/partners/technogym/how-to-train-like-the-mclaren-formula-1-team/
12. Motorsport.com — Norris's "worst day of the year" in F1 winter training. https://www.motorsport.com/f1/news/norris-worst-day-year-f1-winter-training/10701122/
13. Formula1.com — Neck workouts, cardio sessions and mental exercises: how to train like an F1 driver. https://www.formula1.com/en/latest/article/neck-workouts-cardio-sessions-and-mental-exercises-how-to-train-like-an-f1.6c5HZRQvQv2MALjFF295Aq
14. McLaren — How Salus Optima is personalising human performance at McLaren. https://www.mclaren.com/racing/team/how-salus-optima-personalising-human-performance-mclaren/
15. Gizmodo — Formula One drivers stay fit with a car-shaped exercise machine. https://gizmodo.com/of-course-formula-one-drivers-stay-fit-with-a-car-shape-5994806
16. Designboom — Technogym: Formula 1 training machine. https://www.designboom.com/technology/technogym-f1-training-machine/
17. WhichCar — Ferrari's secret F1 gym. https://www.whichcar.com.au/news/motorsport/ferraris-secret-f1-gym
18. Ferrari — Scuderia Ferrari stays fit with Technogym. https://www.ferrari.com/en-EN/formula1/articles/scuderia-ferrari-stays-fit-with-technogym
19. Ferrari — Mental training at the Ferrari Driver Academy. https://www.ferrari.com/en-CA/magazine/articles/mental-training-at-the-ferrari-driver-academy
20. Hintsa — For sports. https://www.hintsa.com/for-sports/ ; Hintsa — Sebastian Vettel case. https://www.hintsa.com/cases/sebastian-vettel/
21. Autosport — The man who changed F1 driver performance. https://www.autosport.com/performance/feature/7309/the-man-who-changed-f1-driver-performance
22. Pirelli — Getting inside the driver's mind. https://www.pirelli.com/global/en-ww/race/racingspot/getting-inside-the-driver-s-mind-51373/
23. Autosport — Why F1 drivers are smarter than you think. https://www.autosport.com/performance/feature/8342/why-f1-drivers-are-smarter-than-you-think
24. Dive-Bomb — Heart rates, Leclerc and mental strength: Dr Riccardo Ceccarelli. https://www.dive-bomb.com/post/heart-rates-leclerc-and-mental-strength-a-conversation-with-dr-riccardo-ceccarelli
25. Autosport — No pain, no gain: 48 hours as a racing driver. https://www.autosport.com/f1/news/no-pain-no-gain-48-hours-as-a-racing-driver-5081710/5081710/
26. F1 Academy — Physical development workshop by Formula Medicine. https://www.f1academy.com/Latest/12LWs2BrhUeoA0NnDMMhDi/physical-development-workshop-brought-to-you-by-formula-medicine
27. F1 Academy — How F1 Academy and More than Equal are building a database. https://www.f1academy.com/Latest/2T8lsxK8JAYjckPmBFxTJL/how-f1-academy-and-greater-than-more-than-equal-are-building-a-database-to
28. Formula1.com — F1 Academy partners with More than Equal. https://www.formula1.com/en/latest/article/f1-academy-partners-with-more-than-equal-as-official-driver-performance-and.5LnObNX29F762hGv7cCwpP
29. Loughborough University — F1 Academy 2026 training camp. https://lboro.ac.uk/news-events/news/2026/august/f1-academy-training-camp/
30. Red Bull — How Thierry Neuville stays fit. https://www.redbull.com/gb-en/thierry-neuville-fit
31. ClickOnDetroit (AP) — Why F1 drivers' workouts might help "tech neck". https://www.clickondetroit.com/health/2026/04/29/why-the-workouts-of-formula-1-drivers-might-help-computer-users-with-tech-neck/
32. Jalopnik — How F1 drivers train to handle g-forces. https://www.jalopnik.com/2224084/how-f1-driver-train-to-handle-g-forces/
33. TUM — IsoMed 2000 equipment sheet. https://www.hs.mh.tum.de/fileadmin/w00bbr/mh/Lehr-_Lernzentrum/Englisch/IsoMed2000-EN.pdf
34. Analysis of Continuous Steering Movement Using a Motor-Based Quantification System. Sensors 2012;12:16008. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3571767/
35. Instrumented steering wheel for biomechanical measurements. ScienceDirect. https://www.sciencedirect.com/science/article/abs/pii/S095741581200030X
36. Williams F1 — Jenson Button breaks Guinness World Record on Batak machine. https://www.williamsf1.com/posts/2e787ea2-0d52-4458-a9bf-85c59b3a6098/jenson-button-breaks-guinness-world-record-on-batak-machine
37. The Checkered Flag — Young drivers take on Button (2010). https://www.thecheckeredflag.co.uk/2010/12/young-drivers-take-on-button/
38. NeuroTracker — Motor sports (vendor). https://www.neurotrackerx.com/use-cases/motor-sports
39. Vater C, Gray R, Holcombe AO. A critical systematic review of the Neurotracker perceptual-cognitive training tool. Psychon Bull Rev 2021;28:1458–1483. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8500884/
40. Horváth D et al. Reactive agility training using light-based stimuli in car racing drivers: an RCT. Sports Med Open 2022. https://sportsmedicine-open.springeropen.com/articles/10.1186/s40798-022-00509-9
41. The Race — F1's push for a female driver is at the heart of the new F2 car. https://www.the-race.com/formula-1/f1s-push-for-female-driver-is-at-the-heart-of-new-f2-car
42. RaceFans — F2 recognises need for 2024 chassis to suit women racers. https://www.racefans.net/2022/12/01/formula-2-recognises-need-for-new-2024-chassis-to-suit-women-racers-better
43. Autosport — Formula 2 much harder physically than Formula 1: Norris. https://www.autosport.com/f1/news/formula-2-much-harder-physically-than-formula-1-mclarens-norris-4990975/4990975/
44. RTR Sports — F1 power steering, and why F2 and F3 go without. https://rtrsports.com/en/do-f1s-have-power-steering/
45. The Race — "You're so tense": how Gen3 changed Formula E physicality. https://www.the-race.com/formula-e/youre-so-tense-how-gen3s-changed-formula-e-physicality
46. The Race — What Formula E is changing after a spate of driver hand injuries. https://www.the-race.com/formula-e/what-formula-e-changing-after-spate-of-driver-hand-injuries
47. Moog — F1 PAS valve case study. https://www.moog.com/content/dam/moog/literature/ICD/motorsport/F1_PAS_Valve_Case_Study.pdf
48. Moog — E243 PAS datasheet. https://www.moog.com/content/dam/moog/literature/ICD/motorsport/E243_PAS_Datasheet.pdf
49. Motorsport Week — Power steering failure triggered Sargeant's accident. https://motorsportweek.com/2023/08/27/power-steering-failure-triggered-sargeants-accident
50. RaceFans — Verstappen's full radio transcript, Bahrain GP 2022. https://clone.racefans.net/2022/03/22/i-dont-hold-back-verstappens-full-radio-transcript-from-his-luckless-bahrain-gp/
51. Formula1.com — Jolyon Palmer's analysis: how Norris managed a car with hydraulic issues. https://www.formula1.com/en/latest/article/jolyon-palmers-analysis-how-lando-norris-managed-a-car-with-hydraulic.229dYZv6iIkprNjbVcszGl
52. Motor Sport Magazine — Uncovering F1's new fitness challenge (2017). https://www.motorsportmagazine.com/articles/single-seaters/f1/updated-uncovering-f1-s-new-fitness-challenge
53. Sky Sports — What to expect from the 2017 rules. https://www.skysports.com/f1/news/12433/10529238/what-to-expect-from-2017-rules
54. F1Technical forum — Steering wheel torque: how much do drivers cope with? https://f1technical.net/forum/viewtopic.php?p=530310
55. Wiibook — How many Nm does an F1 steering wheel have? (unverified). https://wiibook.net/how-many-nm-does-a-f1-steering-wheel-have/
56. Overtake — Force feedback torque philosophy. https://www.overtake.gg/news/force-feedback-torque-philosophy-a-framework-for-accurate-sim-racing.3717/?page=2
57. Fanatec — How realistic is Fanatec torque compared to a real car? https://www.fanatec.com/us/en/explorer/products/racing-wheels-wheel-bases/how-realistic-is-fanatec-torque-compared-to-a-real-car/
58. Granite Devices community — How close to real life are iRacing steering forces? https://community.granitedevices.com/t/how-close-to-real-life-is-the-steering-forces-we-have-in-iracing/10267
59. Steam (Kart Racing Pro) — Maximum steering force Nm. https://steamcommunity.com/app/415600/discussions/0/1333474229067400402/
60. Mechatronics and remote driving control of the drive-by-wire for a go kart. Sensors 2020;20:1216. https://pmc.ncbi.nlm.nih.gov/articles/PMC7070477/
61. DesignJudges — Cockpit control forces. https://www.designjudges.com/articles/cockpit-control-forces
62. Isometric wheel-turning strength of 125 men and 125 women (Proc. Human Factors Society, 1992). https://journals.sagepub.com/doi/full/10.1177/154193129203601005
63. Al-Qaisi et al. Effect of handwheel diameter and orientation on torque production capabilities (2019; DOI 10.1080/24725838.2019.1624283). https://scholarworks.aub.edu.lb/items/fae3f458-953f-4359-9476-68a67e3ceb24
64. J Phys Ther Sci 2013;25(9) — Steering torque and seat distance. https://www.jstage.jst.go.jp/article/jpts/25/9/25_jpts-2013-126/_article/-char/en
65. Simagic — Alpha EVO series. https://simagic.com/pages/alpha-evo
66. Mercedes-AMG F1 — G-force and Formula One explained. https://mercedesamgf1.com/news/g-force-and-formula-one-explained
67. The Drive — F1 cars pulling 6 g through some Silverstone corners. https://www.thedrive.com/start-finish/12479/formula-one-cars-pulling-6g-through-some-silverstone-corners.md ; Motorsport.com — F1's top corners 30 km/h faster in 2017. https://motorsport.com/f1/news/f1-s-top-corners-30km-h-faster-in-2017-new-data-reveals-957808/
68. Mercedes-AMG F1 — Japanese Grand Prix 2023 preview. https://www.mercedesamgf1.com/races/japanese-grand-prix-2023
69. F1-Fansite — G-force glossary. https://www.f1-fansite.com/glossary/g-force/
70. Formula1.com — Circuit guide: Silverstone. https://www.formula1.com/en/latest/article/circuit-guide-everything-you-need-to-know-about-silverstone-2026.5Sl0O8g393enBWVIkjRzOr
71. Formula1.com — Circuit guide: Suzuka. https://www.formula1.com/en/latest/article/circuit-guide-everything-you-need-to-know-about-the-suzuka-circuit.2BbgsRdkeux78UBGbmYiZV
72. GSSI Sports Science Exchange — Cooling strategies for Formula 1 drivers. https://www.gssiweb.org/en/sports-science-exchange/article/my-engine-is-overheating!---cooling-strategies-for-formula-1-drivers
73. RTÉ Brainstorm — The sport physiology of Formula One. https://www.rte.ie/brainstorm/2021/0604/1224044-formula-one-sport-physiology
74. Pick AJ, Cole DJ. Measurement of driver steering torque using electromyography. ASME J Dyn Syst Meas Control 2006;128(4):960–968 (via citing works); Pick & Cole 2007, Dynamic properties of a driver's arms holding a steering wheel. https://journals.sagepub.com/doi/abs/10.1243/09544070JAUTO460 ; Correlation between muscular activity and vehicle motion during double lane change, Sensors 2024. https://doi.org/10.3390/s24185982 ; Function of shoulder muscles of driver in vehicle steering maneuver. https://www.researchgate.net/publication/257686282_Function_of_shoulder_muscles_of_driver_in_vehicle_steering_maneuver
75. Backman J, Häkkinen K et al. Neuromuscular performance characteristics of open-wheel and rally drivers. J Strength Cond Res 2005;19(4):777–784. https://journals.lww.com/nsca-jscr/abstract/2005/11000/neuromuscular_performance_characteristics_of.9.aspx
76. Backman J. Acute neuromuscular responses to car racing (MSc thesis, Univ. Jyväskylä, 2005). https://jyx.jyu.fi/jyx/Record/jyx_123456789_12567
77. Analysis of the effect of training driving on electromyographic parameters in trained karting drivers. J Funct Morphol Kinesiol 2025;10(2):190. https://www.mdpi.com/2411-5142/10/2/190
78. Effects of high-velocity elbow manipulation on forearm EMG recovery in karting drivers (RCT). J Clin Med 2026;15(11):4267. https://www.mdpi.com/2077-0383/15/11/4267
79. Mills, Blackmore, Wakefield, Neupert. Forearm muscle activity during motorsport: a case study (2025). https://researchportal.port.ac.uk/en/publications/forearm-muscle-activity-during-motorsport-a-case-study/ ; https://www.researchgate.net/publication/394651949_Forearm_Muscle_Activity_During_Motorsport_A_Case_Study
80. University of Portsmouth — Scientists make the track their lab (2019). https://www.port.ac.uk/news-events-and-blogs/news/scientists-make-the-track-their-lab-to-improve-the-performance-of-racing-drivers-and-riders
81. Durand S, Ripamonti M, Rahmani A, Beaune B. Is handgrip force changed following a 45-min kart driving session in competition? Comput Methods Biomech Biomed Engin 2013;16(sup1):112–113. https://hal.archives-ouvertes.fr/hal-02357867
82. Matsumura K et al. The effect of competition on heart rate during kart driving: a field study. BMC Res Notes 2011. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3180469/
83. Marina M, Torrado P, Bescós R. Recovery and fatigue behavior of forearm muscles during a repetitive power grip gesture in racing motorcycle riders. IJERPH 2021;18(15):7926. https://www.mdpi.com/1660-4601/18/15/7926
84. Marina M et al. Monitoring hand flexor fatigue in a 24-h motorcycle endurance race. J Electromyogr Kinesiol 2011;21:255–261. https://www.sciencedirect.com/science/article/abs/pii/S1050641110001811
85. McKnight PJ et al. VO2peak, body composition, and neck strength of elite motor racing drivers. Med Sci Sports Exerc 2019. https://www.pubmed.gov/30817714/
86. Tyler et al. Physiological and health demands of Formula 1 motor racing: a comprehensive review with driver performance coach insight. https://www.researchgate.net/publication/400929032_Physiological_and_health_demands_of_Formula_1_motor_racing_a_comprehensive_review_with_driver_performance_coach_insight ; summary: https://www.performancenutritiondigest.com/p/the-physiological-demands-of-formula
87. Review of the physiological responses to open-wheeled racing with current trends in testing and strength training. https://www.researchgate.net/publication/349443803_Review_of_the_Physiological_Responses_to_Open-Wheeled_Racing_with_Current_Trends_in_Testing_and_Strength_Training
88. Physiological demands and training recommendations for formula racing drivers: a brief review. Appl Sci 2026;16(15):7534. https://www.mdpi.com/2076-3417/16/15/7534
89. The role of physical preparation in motorsport athletes across different vehicle types: a narrative review. IJSPP 2026 (ahead of print). https://journals.humankinetics.com/view/journals/ijspp/aop/article-10.1123-ijspp.2026-0240/article-10.1123-ijspp.2026-0240.xml
90. A scoping review of the physiological profiles of motorsport drivers. https://www.researchgate.net/publication/374094958_A_Scoping_Review_of_the_Physiological_Profiles_of_Motorsport_Drivers_Implications_for_Athlete_Training
91. Effect of a halo-type structure on neck muscle activation of an open-cockpit race car driver under qualifying conditions. https://pmc.ncbi.nlm.nih.gov/articles/PMC6040518/
92. Microneedle array electrode-based wearable EMG system for detection of driver drowsiness through steering wheel grip. Sensors 2021;21:5091. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8347525/
93. Lum D, Barbosa TM. Brief review: effects of isometric strength training on strength and dynamic performance. Int J Sports Med 2019;40(6):363–375. https://www.researchgate.net/publication/332206319_Brief_Review_Effects_of_Isometric_Strength_Training_on_Strength_and_Dynamic_Performance
94. Oranchuk DJ, Storey AG, Nelson AR, Cronin JB. Isometric training and long-term adaptations: effects of muscle length, intensity, and intent. Scand J Med Sci Sports 2019;29(4):484–503. https://pubmed.ncbi.nlm.nih.gov/30580468/
95. Kitai TA, Sale DG. Specificity of joint angle in isometric training. Eur J Appl Physiol 1989. https://link.springer.com/article/10.1007/BF00637386
96. Noorkoiv M et al. Neuromuscular adaptations associated with knee joint angle-specific force change (2014). https://ro.ecu.edu.au/ecuworkspost2013/280 ; Alegre LM et al. 2014, Eur J Appl Physiol. https://link.springer.com/doi/10.1007/s00421-014-2967-x
97. Strength training: isometric training at a range of joint angles versus dynamic training (2005). https://pubmed.ncbi.nlm.nih.gov/16195033/
98. Oranchuk DJ et al. Scientific basis for eccentric quasi-isometric resistance training: a narrative review. J Strength Cond Res 2019. https://ro.ecu.edu.au/ecuworkspost2013/7028
99. Nelson A, Oranchuk D. Quasi-isometric eccentric training workshop (AUT SPRINZ). https://sprinz.aut.ac.nz/__data/assets/pdf_file/0005/121919/Quasi-isometric-Eccentric-Training-Workshop_AndreNelson_DustinOranchuk.pdf
100. Pushing vs holding isometric muscle actions: scoping/systematic review (medRxiv preprint, 2024). https://medrxiv.org/content/10.1101/2024.11.04.24316609v2.full.pdf
101. Behm DG, Sale DG. Velocity specificity of resistance training. Sports Med 1993;15:374–388. https://www.doi.org/10.2165/00007256-199315060-00003
102. Morrissey MC et al. Review of resistance-training specificity (1995), summary. https://coachsci.sdsu.edu/csa/vol21/morrisse.htm
103. Douglas J, Pearson S, Ross A, McGuigan M. Chronic adaptations to eccentric training: a systematic review. Sports Med 2017;47:917–941. https://www.pubmed.gov/27647157/
104. Eccentric-only vs concentric-only isokinetic training: systematic review and meta-analysis. Sports Med Open 2025. https://link.springer.com/article/10.1186/s40798-025-00887-w
106. Schoenfeld BJ, Grgic J, Ogborn D, Krieger JW. Strength and hypertrophy adaptations between low- vs high-load resistance training. J Strength Cond Res 2017. https://pubmed.ncbi.nlm.nih.gov/28834797/
108. Campos GER et al. Muscular adaptations in response to three different resistance-training regimens. Eur J Appl Physiol 2002. https://pubmed.ncbi.nlm.nih.gov/12436270/
109. Giles D et al. The determination of finger-flexor critical force in rock climbers. IJSPP 2019;14(7):972–979. https://www.researchgate.net/publication/329814457_The_Determination_of_Finger_Flexor_Critical_Force_in_Rock_Climbers ; Giles D et al. An all-out test to determine finger flexor critical force. IJSPP 2021;16(7):942–949. https://eprints.glos.ac.uk/8771/ ; validation of the 4-min all-out test (2024). https://www.ncbi.nlm.nih.gov/pmc/articles/PMC11365833/
110. West W et al. Sustained handgrip endurance at 30/50/75 % MVC. Eur J Appl Physiol 1995. https://dx.doi.org/10.1007/bf00240408
111. Bell L et al. Integrating deloading into strength and physique sports training programmes: an international Delphi consensus. Sports Med Open 2023. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10511399/
112. (a) Munn J, Herbert RD, Gandevia SC. Contralateral effects of unilateral resistance training: a meta-analysis. J Appl Physiol 2004. https://pubmed.ncbi.nlm.nih.gov/15075311/ ; (b) Cirer-Sastre R et al. Cross-education training loads meta-analysis. J Sports Sci Med 2017;16:180. https://www.jssm.org/jssm-16-180.xml-Fulltext
113. Forman DA et al. Investigating the muscular and kinematic responses to sudden wrist perturbations during a dynamic tracking task. Sci Rep 2020;10:4161. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC7058070/
114. Mannella K et al. The effects of isometric hand grip force on wrist kinematics and forearm muscle activity during radial and ulnar wrist joint perturbations. PeerJ 2022;10:e13495. https://peerj.com/articles/13495
115. Delp SL, Grierson AE, Buchanan TS. Maximum isometric moments generated by the wrist muscles in flexion-extension and radial-ulnar deviation. J Biomech 1996. https://pubmed.ncbi.nlm.nih.gov/8884484/
116. O'Sullivan LW, Gallwey TJ. Upper-limb surface electromyography at maximum supination and pronation torques. J Electromyogr Kinesiol 2002;12(4):275–285. https://pubmed.ncbi.nlm.nih.gov/12121684/
117. Acute blood pressure response to different types of isometric exercise: systematic review and meta-analysis (2024). https://pubmed.ncbi.nlm.nih.gov/39077420/ ; Isometric exercise training for blood pressure management: systematic review and meta-analysis (2014). https://pubmed.ncbi.nlm.nih.gov/24582191/ ; example 4 × 2-min 30 % MVC handgrip protocol (registered trial). https://cdn.clinicaltrials.gov/large-docs/14/NCT06215014/Prot_SAP_000.pdf
118. Effectiveness of different methods of resistance exercises in lateral epicondylosis: a systematic review. https://pubmed.ncbi.nlm.nih.gov/22075055/
120. Microsoft — Using force feedback (DirectInput). https://learn.microsoft.com/en-us/previous-versions/windows/desktop/bb153254(v=vs.85) ; Effect types. https://msdn.microsoft.com/en-us/library/ee417541(v=vs.85)
121. FastF1 — Car telemetry API reference. https://docs.fastf1.dev/api_reference/telemetry.html
122. F1Technical forum — When did F1 introduce power steering? https://www.f1technical.net/forum/viewtopic.php?t=29795 ; Miata.net forum thread. https://forum.miata.net/vb/showthread.php?t=779899
123. Motorsport.com — F1 driver training: workout regime, diet, cardio and more. https://www.motorsport.com/f1/news/f1-driver-training-workout-regime-diet-cardio/5847575/
124. Iron Neck — IndyCar. https://iron-neck.com/pages/indycar
125. Fanatec — Adjusting steering sensitivity for different racing disciplines. https://www.fanatec.com/ca/en/explorer/products/steering-wheel/adjusting-steering-sensitivity-for-different-racing-disciplines/
