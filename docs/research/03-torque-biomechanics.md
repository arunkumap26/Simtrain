# 03: Wheel torque, human strength, and live strength metrics

**Project:** RimForce (Simagic Alpha EVO as a steering strength trainer)
**Question:** Is "12 Nm / 0.15 m = 80 N, about 8 kgf, so it's only an endurance load" right? Why does a medium setting feel very hard? What should the app show live?
**Date:** 2026-11-25

---

## How this was researched, and its limits

- Facts come from web searches of the cited pages. Direct page fetches were blocked in this session for `simagic.com` and `pcgameshardware.de`, and the search budget ran out near the end. **Every number below therefore comes from a search-engine excerpt of the linked page, not from a full read of the paper.** Items marked **(verify)** had garbled or incomplete excerpts and should be checked against the primary source before shipping.
- Numbers marked **(derived)** are my own calculations from cited inputs. They are not published values.
- Strength norms vary a lot with the device, the posture, the handle and the population. Use them as rough priors that the user's own test results replace.

---

## 0. TL;DR

1. **The arithmetic is right.** 12 Nm on a 300 mm rim is 80 N of tangential force, about 8.2 kgf in total. With two hands at 9-and-3 that is about 40 N (about 4 kgf) per hand. One hand pushes up and the other pulls down.
2. **The conclusion "only an endurance load" does not follow** for most people or for the weakest links in the chain. The 80 N is delivered:
   - at the end of a roughly 0.45 m lever from the shoulder, on top of the arm's own weight;
   - through the wrist deviators or extensors, which are small muscles;
   - through a friction grip, because at 9 and 3 the force runs *along* the rim inside the fist.

   At 12 Nm (static, 50/50 split) the lifting shoulder works at about 33 % of MVC for an average man and about 60 % for an average woman. The wrist is at about 29 % and 48 %. If you try to hold the wheel by twisting the forearms, 12 Nm is about 75 % (men) or more than 100 % (women) of combined pronation and supination strength. **(derived)**, see §1.2.
3. **Measured against whole-body seated steering strength, 12 Nm is moderate.** Trained drivers produced 34–47 Nm on a 254 mm wheel, so 12 Nm is roughly 20–30 % of a strong man's two-handed maximum. For a fit man the base is an endurance-to-hypertrophy stimulus for the *shoulders*. It is a **genuine strength stimulus for the wrist, forearm and grip, and for smaller or untrained people.**
4. **"Medium" (about 6 Nm) feeling very, very hard is not explained by the static load alone** (about 2 kgf per hand). The likely contributors are:
   - velocity- or acceleration-dependent effects (damper, inertia, friction, game FFB, a stationary car);
   - actual torque above what the slider suggests;
   - weak postures (arms extended, hands at 12/6, one hand, wrist-twisting);
   - concentric "fighting" with co-contraction;
   - the movement being unfamiliar.

   The app should *measure* this (§1.3, §6.0).
5. **Use the right physics.** Treat torque as the training variable. Display rim force, per-hand force, an estimated grip demand, wrist and shoulder %MVC estimates, power, work, angular impulse, time under tension and a fatigue budget. Label each one with its assumptions (§6).

---

## 1. Verdict: is 8 kgf right, and why does it feel so hard?

### 1.1 The arithmetic (correct)

F_t = τ / r. With τ = 12 Nm and r = 0.15 m, F_t = 80 N, which is 8.16 kgf (÷ 9.80665).

- Simagic rates the Alpha EVO at 12 Nm "max torque" ([R1](https://simagic.com/pages/alpha-evo), [R8](https://simracingmanual.com/hardware/wheelbases/simagic-alpha-evo/)). The GT Neo rim is 300 mm in diameter, so r = 0.15 m ([R16](https://simagic.com/pages/details-gt-neo)).
- The hand grips the rim's cross-section, so the effective radius to the centre of the grip is slightly smaller (about 0.135–0.14 m), giving about 86–89 N **(derived)**.
- On a 290 mm FX rim ([R17](https://simagic.com/pages/details-fx)) the force is 82.8 N. On a 330 mm GT1S rim ([R20](https://www.apexsimracing.com/products/gt1s-round-d-shaped-wheel)) it is 72.7 N **(derived)**.
- The actual output may be higher than the nameplate. One reviewer measured the 9 Nm EVO Sport at 10.92 Nm and the 18 Nm EVO Pro at 21.42 Nm ([R7](https://simracingcockpit.gg/simagic-alpha-evo-range-specifications-and-buyers-guide); method unknown, **verify**). If the 12 Nm unit overshoots by a similar ~20 %, that would be about 14.5 Nm, or about 97 N at 0.15 m **(derived, speculative)**.

So "about 8 kgf at the rim" is right, give or take 10–20 %.

### 1.2 Why "only an endurance load" is wrong: a load audit at 12 Nm

"8 kg" sounds light because we picture carrying a shopping bag with the arm hanging, where the load runs along the arm. At the wheel the same force acts *perpendicular* to an arm reaching forward. It is split into roughly 4 kgf pushing up on one side and 4 kgf pulling down on the other. Before that it must pass through a friction grip and the wrist.

The table below uses the stated assumptions and cited norms. It assumes a static hold at 12 Nm, a 0.15 m radius, hands at 9-and-3 with a 50/50 split, and a thumb-up (neutral) grip. All percentages are **(derived)**.

| Link in the chain | Load at 12 Nm | Typical man | Typical woman | Basis / assumptions |
|---|---|---|---|---|
| **Shoulder, lifting hand** (flexors) | 40 N × 0.45 m + arm weight ≈ 25.7 Nm (M) / 24.2 Nm (F) | **≈33 % MVC** (vs 77 Nm) | **≈60 % MVC** (vs ~40 Nm, est.) | Shoulder-to-hand horizontal reach 0.45 m (assumed). Arm mass 4.9 % of body mass ([R44](https://biomed.bas.bg/en/wp-content/uploads/2012/02/MassInertial.html)), arm CoM 0.2 m ahead of the shoulder (assumed), body mass 80 kg (M) / 65 kg (F). Male flexion MVC 77 Nm at 90° ([R38](https://pmc.ncbi.nlm.nih.gov/articles/PMC12821543/)). The female value is an estimate at about 50 % of male ([R39](https://onlinelibrary.wiley.com/doi/10.1111/j.1748-1716.2009.02022.x), [R40](https://peerj.com/articles/16968/)). |
| Shoulder, pulling-down hand (extensors) | 18 Nm − arm weight ≈ 10–12 Nm | ≈11 % | ≈27 % | Gravity *helps* this hand. MVC from [R39](https://onlinelibrary.wiley.com/doi/10.1111/j.1748-1716.2009.02022.x) (91.9 / 43.5 Nm, young adults). |
| **Wrist**, radial deviation (lifting hand) | 40 N × 0.075 m ≈ 3.0 Nm | **≈29 %** (vs 10.3 Nm) | **≈48 %** (vs 6.2 Nm) | 0.075 m from wrist centre to centre of the fist (assumed). MVC from a power-grip study ([R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796)). |
| Wrist, ulnar deviation (pulling hand) | ≈3.0 Nm | ≈23 % (vs 13.0) | ≈39 % (vs 7.7) | [R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796) |
| Wrist, palms-down grip (extension) | ≈3.0 Nm | ≈32–46 % (vs ~6.5–9.3 Nm) | ≈50–75 % (est.) | Extension MVC 6.5 ± 1.5 Nm ([R32](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9410372/), attributed to Yoshii 2015) and about 9.3 Nm ([R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796)). Women at about 60–65 % of men ([R25](https://peerj.com/articles/16557/)). |
| **Grip** (friction) | ≈56 N squeeze per hand | ≈11 % of dynamometer grip (51 kg); ≈20 % of max steering-rim grip (280 N) | ≈18 % (31 kg); ≈29 % (193 N) | §4.3 model: G = 1.4 × F / (2 × 0.5). Norms from [R33](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0113637) and [R37](https://www.researchgate.net/publication/229381284_Steering-wheel_grip_force_characteristics_of_drivers_as_a_function_of_gender_speed_and_road_condition). |
| **Forearm twist only** (if you "wrist it") | 12 Nm shared by pronation + supination | **≈75 %** (vs 8.1 + 8.0 Nm) | **≈125 %**, i.e. not holdable (vs 4.7 + 4.9 Nm) | [R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796) |
| Whole-system steering capacity | 12 Nm | ≈22–30 % of 40–55 Nm | ≈36–60 % of ~20–33 Nm (est.) | 34–47 Nm on a 254 mm wheel ([R52](https://www.designjudges.com/articles/cockpit-control-forces)), scaled ×1.18 to a 300 mm rim. Female estimate at about 50–60 % of male, from handwheel studies ([R61](https://doi.org/10.1080/24725838.2019.1624283), [R62](https://www.researchgate.net/publication/245095718_Effects_of_wheel_design_on_the_torques_applied_to_large_hand_wheels)). |

**What those percentages mean in time.** Rohmert's classic endurance curve ([R45](https://arxiv.org/pdf/1104.1190)) gives about 2 minutes at 33 % MVC, about 48 s at 60 % and about 70 s at 48 % **(derived)**. The shoulder is the *most* fatigable joint in the large meta-analysis by Frey Law & Avin ([R46](https://www.researchgate.net/publication/41002103_Endurance_time_is_joint-specific_A_modelling_and_meta-analysis_investigation)). Rohmert's curve has also been found to overestimate endurance at low %MVC ([R48](https://stacks.cdc.gov/view/cdc/188634)). Holding 12 Nm continuously is therefore roughly "a hard 1–3 minute set" for the weakest link, not an all-day load.

**Arm weight alone is a baseline load.** Even at 0 Nm, holding the arm out to the wheel costs about 10 % (M) to 16 % (F) of shoulder flexion MVC **(derived)**.

### 1.3 Why a *medium* setting can feel "very, very hard"

At 6 Nm (static, two hands) the per-hand force is about 20 N, roughly a 2 kg weight. That puts the lifting shoulder at about 22 % (M) or 38 % (F) and the wrist at about 15 % or 24 % **(derived)**. That is noticeable but not crushing. If 6 Nm felt *very* hard, one or more of the following was probably going on. They are ordered by how likely they are and how easy they are to test.

1. **Velocity- or acceleration-dependent torque was on top of the slider value.** SimPro has separate mechanical damper, friction and inertia settings ([R14](https://simracingcockpit.gg/acc-simagic-settings/), [R15](https://hotlap.cc/en/ffb-settings/simagic-alpha/beamng/)). A damper adds torque in proportion to how fast you turn, so fast turning feels far heavier than holding still.
   - A sim with the car stationary also gives heavy steering. Fanatec cites 10–12 Nm for real cars when stationary ([R72](https://www.fanatec.com/us/en/explorer/products/racing-wheels-wheel-bases/how-realistic-is-fanatec-torque-compared-to-a-real-car/)). A non-assisted Daewoo Matiz measured about 26–33 Nm when parking (static) ([R78](https://annals.fih.upt.ro/pdf-full/2013/ANNALS-2013-2-19.pdf), **verify**).
   - A game can saturate (clip) the base at full torque.
2. **The slider is not a calibrated Nm readout.** SimPro's "Overall Strength / Maximum Torque" (called "Force Feedback / Total Force" on EVO) caps the base's output ([R13](https://gonkyracing.com/en/ffb-guide/simagic-alpha-evo), [R14](https://simracingcockpit.gg/acc-simagic-settings/)). Simagic does not document a sustained-torque figure or the exact percent-to-Nm mapping ([R8](https://simracingmanual.com/hardware/wheelbases/simagic-alpha-evo/)). Bases may also exceed their rating ([R7](https://simracingcockpit.gg/simagic-alpha-evo-range-specifications-and-buyers-guide)).
3. **Weak postures.**
   - Torque capacity falls as the arms extend. Drivers produced the most torque close to the chest ([R52](https://www.designjudges.com/articles/cockpit-control-forces)), and maximum torque came at a seat distance of about 50 % of arm length ([R55](https://www.jstage.jst.go.jp/article/jpts/25/9/25_jpts-2013-126/_article)).
   - When the wheel has turned about 90°, the hands sit near 12 and 6. The force is then horizontal and loads the weaker shoulder rotators and horizontal adductors (§4.1).
   - Holding with one hand doubles the per-hand load.
   - Resisting by twisting the wrists rather than pushing and pulling with the shoulders can exceed forearm capacity (see the table in §1.2).
4. **Concentric fighting, co-contraction and unfamiliarity.**
   - Muscles produce less torque while shortening than in an isometric hold. This is the force–velocity relation; it is textbook physiology and was not sourced in this session.
   - Drivers co-contract antagonists to stiffen the arm ([R58](https://www.scilit.net/publications/43bdd2070a1e817209f3d35af3c76dcb)). That costs effort without adding net torque.
   - Steering is produced by the deltoid, infraspinatus, latissimus, subscapularis and triceps working together ([R60](https://www.researchgate.net/publication/265734098_Muscle_activity_and_co-contraction_of_musculoskeletal_model_during_steering_maneuver)), and their roles depend on the direction of rotation and torque ([R59](https://link.springer.com/article/10.1007/s11431-012-5045-9)). Unpractised coordination is costly.
5. **Effort follows relative intensity, and the arms are raised.** Perceived exertion tracks %MVC: about 30 % MVC rated as 2/10 and about 90 % as 10/10 in one elbow study ([R85](https://bura.brunel.ac.uk/bitstream/2438/8654/2/Fulltext.pdf)). Small muscles reach high %MVC quickly. With the forearm raised above heart level, handgrip endurance at 15 % MVC fell by about 66 s, from about 456 s to about 390 s ([R50](https://www.nature.com/articles/s41598-024-83939-7)).

**How to settle it (the app can do this):**
- Turn SimPro's damper, friction and inertia to 0 and let the app own all effects.
- Verify the real torque with a luggage scale (§6.0).
- Compare a static hold with fast turning at the same commanded torque.
- Log angle and velocity to see which hand positions felt worst.

### 1.4 Bottom line

The previous analysis got the physics right and the physiology wrong. The 12 Nm EVO cannot max out a fit man's whole-body two-handed steering strength, which is roughly 35–55 Nm. For him it is mostly a muscular-endurance and hypertrophy-by-volume tool for the shoulders. For the wrist, the forearm (especially grip near the hub, §7) and the grip, and for most women and smaller or untrained users, the same 12 Nm reaches 30–125 % of MVC. That is real strength-training territory. "Felt very hard at medium" probably also involved dynamic effects or a non-linear slider. That is a claim to measure, not to argue about.

---

## 2. Hardware facts: Simagic Alpha EVO and rims

### 2.1 Alpha EVO family

| Model | Rated torque | Notes |
|---|---|---|
| Alpha EVO Sport | 9 Nm | One reviewer measured 10.92 Nm ([R7](https://simracingcockpit.gg/simagic-alpha-evo-range-specifications-and-buyers-guide), **verify** the method). Retailer guides describe the same chassis capped at 9 Nm ([R12](https://apex300.com/blogs/news/which-simagic-alpha-evo-should-you-buy), [R11](https://www.directdrive.it/en-eu/blogs/news/simagic-alpha-evo-sport-evo-pro-and-ultra-which-to-choose)). |
| **Alpha EVO** | **12 Nm** | Simagic spec-table "max torque" ([R1](https://simagic.com/pages/alpha-evo), [R2](https://simagic.com/blogs/news/alpha-evo-series-out-now-html)). Simagic states no sustained figure ([R8](https://simracingmanual.com/hardware/wheelbases/simagic-alpha-evo/)). Retailers disagree, labelling it "peak torque 12 Nm" ([R9a](https://gperformance.eu/products/simagic-alpha-evo)) or "12 Nm (constant)" ([R9b](https://thefrenchsimracer.com/en/products/simagic-alpha-evo-12nm)). Simagic says active cooling ensures you "always get 100 % of available torque" ([R1](https://simagic.com/pages/alpha-evo)). |
| Alpha EVO Pro | 18 Nm, then 21 Nm | Since SimPro Manager 3.2.3 a free software update allows up to 21 Nm. Simagic called the earlier 18 Nm "maximum torque" and calls the new figure "Wheel Base Torque" ([R4](https://www.pcgameshardware.de/Lenkraeder-Hardware-255542/News/Simagic-Alpha-EVO-Pro-21-Nm-Update-1556804/), [R5](https://www.thesimracingden.com/news-simagic-evo-pro-free-21nm-upgrade)). Traxion calls the 18 Nm continuous, not peak ([R6](https://traxion.gg/simagic-alpha-evo-pro-review-a-powerful-direct-drive-bargain/)). One reviewer measured 21.42 Nm ([R7](https://simracingcockpit.gg/simagic-alpha-evo-range-specifications-and-buyers-guide)). |
| Alpha EVO Ultra | 28 Nm | Added to the line later ([R10](https://www.tweaktown.com/reviews/11368/simagic-alpha-evo-ultra-28nm-wheelbase/index.html), [R11](https://www.directdrive.it/en-eu/blogs/news/simagic-alpha-evo-sport-evo-pro-and-ultra-which-to-choose)). |

**Peak or sustained?** Simagic does not publish a separate continuous rating for the EVO family. Treat 12 Nm as the ceiling, and treat any long-hold capability as unverified. The app should check delivered torque itself (§6.0).

### 2.2 SimPro strength settings and torque

- SimPro Manager's top-level strength control is "Overall Strength / Maximum Torque". On EVO bases it appears as "Force Feedback / Total Force". It is a percentage cap on the base's maximum output ([R13](https://gonkyracing.com/en/ffb-guide/simagic-alpha-evo), [R14](https://simracingcockpit.gg/acc-simagic-settings/)).
- Simagic's ACC preset sets it to 100 % and lowers in-game gain instead ([R14](https://simracingcockpit.gg/acc-simagic-settings/)).
- Mechanical damper, friction and inertia are separate sliders, and community presets for them vary ([R14](https://simracingcockpit.gg/acc-simagic-settings/), [R15](https://hotlap.cc/en/ffb-settings/simagic-alpha/beamng/)).
- **Working model for the app:** τ_max,eff ≈ T_rated × s_overall. App force commands then scale within τ_max,eff. **This mapping is assumed linear and is undocumented.** Calibrate it (§6.0).

### 2.3 Rim diameters (Simagic)

| Rim | Diameter | Source |
|---|---|---|
| GT Neo | 300 mm (one review says 310 mm) | Simagic ([R16](https://simagic.com/pages/details-gt-neo)); review ([R24](https://simracingcockpits.com/simagic-gt-neo-steering-wheel-review/)) |
| FX (formula) | 290 mm | Simagic ([R17](https://simagic.com/pages/details-fx)) |
| FX Pro | 290 mm (one source only, **verify**) | [R23](https://simgasm.com/blogs/articles/choosing-a-simagic-steering-wheel-gt-neo-vs-fx-pro-vs-round-rims) |
| Zeus Formula | 280 mm | Simagic ([R18](https://simagic.com/pages/detail-simagic-zeus-formula-steering-wheel)) |
| Zeus GT | 300 mm default; interchangeable grips | Simagic ([R19](https://simagic.com/pages/detail-simagic-zeus-gt-steering-wheel)) |
| GT1S round / D | 330 mm | Retailer ([R20](https://www.apexsimracing.com/products/gt1s-round-d-shaped-wheel)) |
| GT1 / GT Pro rims | 325 mm C or D shape, 330 mm round | Retailer ([R21](https://simmotion.com/products/simagic-steering-wheel)) |
| GTS | 300 mm | Retailer ([R22](https://www.rseat-europe.com/simagic-products/simagic-steering-wheels)) |

---

## 3. Human strength norms

All values are isometric maxima (MVC) of healthy adults unless noted. Handles and postures differ between studies, which explains much of the spread.

### 3.1 Forearm pronation and supination torque (Nm)

| Source | Men | Women | Notes |
|---|---|---|---|
| Power-grip study ([R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796), [RG](https://www.researchgate.net/publication/270595529_The_interacting_effects_of_forearm_rotation_and_exertion_direction_on_male_and_female_wrist_strength)) | Pronation 8.1, supination 8.0 | Pronation 4.7, supination 4.9 | **Recommended default prior.** |
| Axelsson 2018, n = 499, ages 15–85 ([R27](https://www.jhandsurg.org/article/S0363-5023(17)30770-0/fulltext)) | Men about 70 % stronger than women | | Overall means: supination 7.2, pronation 6.1 Nm (shovel handle), as cited by [R28](https://www.handmicro.org/journal/view.php?doi=10.12790%2Fahm.24.0054). **Derived split:** M ≈ 8.9 / 7.6, F ≈ 5.3 / 4.5 Nm. |
| Korean norms 2024, n = 500 ([R28](https://www.handmicro.org/journal/view.php?doi=10.12790%2Fahm.24.0054)) | Higher than women at every age (p < .05) | Dominant hand ≈ 2.78 supination / 2.65 pronation | T-handle gives much lower values (overall about 3.3 Nm). Peak strength at ages 30–39. |
| Matsuoka 2006, ages 22–45 (via [R25](https://peerj.com/articles/16557/)) | Pronation ≈ 7.2–11.9 | Pronation ≈ 3.3–6.0 | Depends strongly on forearm position (**verify**, the table is garbled). |
| O'Sullivan & Gallwey 2002, men ([R29](https://pubmed.ncbi.nlm.nih.gov/12121684/)) | Supination up to 16.2 (forearm 75 % prone); pronation 13.1 (neutral, elbow 45°) | | Best-posture maxima |
| Sex ratio ([R25](https://peerj.com/articles/16557/)) | | About 55–60 % of male (range 45–98 %) | Systematic review of 25 studies |

### 3.2 Wrist torque (Nm)

| Direction | Men | Women | Source |
|---|---|---|---|
| Radial deviation | 10.3 | 6.2 | Power grip ([R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796)) |
| Ulnar deviation | 13.0 (another study: 9.3 ± 2.9) | 7.7 | [R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796); Plewa 2016 ([R30](https://ir.lib.uwo.ca/boneandjointpub/350), posture not confirmed) |
| Flexion | ≈11.9 ± 2.9 (another study: 12.3) | ≈4.6 (one study, **verify**) | [R32](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9410372/) (Yoshii 2015); the 12.3 / 4.6 values appear in a copy of [R25](https://www.researchgate.net/publication/376530624_Sex_differences_in_wrist_strength_a_systematic_review) without a clear study attribution |
| Extension | ≈6.5 ± 1.5 to 9.3 | ≈3.3–3.7 | [R32](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9410372/), [R26](https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796), Ikeda 2023 (6.1 M / 3.3 F) ([R31](https://pubmed.ncbi.nlm.nih.gov/36573481/)) |
| Sex ratios | | Flexion/extension about 60–65 %, radial/ulnar about 60–70 % | [R25](https://peerj.com/articles/16557/) |

Wrist strength depends on the posture of the other wrist and forearm axes ([R30](https://ir.lib.uwo.ca/boneandjointpub/350)). Ikeda 2023 links sex differences in wrist-extensor torque and endurance to lateral epicondylitis ("tennis elbow") ([R31](https://pubmed.ncbi.nlm.nih.gov/36573481/)). That is relevant to programming high-volume gripping.

### 3.3 Grip strength (kg, dynamometer)

| Source | Men | Women |
|---|---|---|
| Dodds 2014, 12 UK studies, about 50 k people ([R33](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0113637), [PubMed](https://pubmed.ncbi.nlm.nih.gov/25474696/)) | Peak median **51 kg** (ages 29–39) | Peak median **31 kg** (ages 26–42) |
| SRALab summary, Jamar, right hand ([R34](https://www.sralab.org/rehabilitation-measures/hand-held-dynamometer-grip-strength)) | About 47 kg at ages 20–49 (older ages not in the excerpt) | 30 / 31 / 29 / 28 / 24 / 20 kg for the 20s, 30s, 40s, 50s, 60s and 70+ |
| Tromsø 2015–16, n = 7,824, Jamar+ ([R35](https://link.springer.com/article/10.1007/s10433-023-00791-w)) | 55.7 kg (ages 40–44); 48.9 kg (ages 60–64) | 33.3 kg (40–44); 28.7 kg (60–64) |
| EWGSOP2 "low grip" cut-off ([R36](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8113334/)) | < 27 kg | < 16 kg |
| **Max grip force on a steering wheel** ([R37](https://www.researchgate.net/publication/229381284_Steering-wheel_grip_force_characteristics_of_drivers_as_a_function_of_gender_speed_and_road_condition)) | 280.5 N | 192.5 N |

Grip on a rim reaches only about 55–65 % of dynamometer grip **(derived** by comparing [R37](https://www.researchgate.net/publication/229381284_Steering-wheel_grip_force_characteristics_of_drivers_as_a_function_of_gender_speed_and_road_condition) with [R33](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0113637); these are different populations, so the figure is rough).

### 3.4 Shoulder and elbow (Nm)

| Joint / direction | Men | Women | Source |
|---|---|---|---|
| Shoulder flexion, arm at 90° | 77 ± 13.8 (n = 14); 43 at 160° | not found; about 40 estimated **(derived)** | Aronis 2025 ([R38](https://pmc.ncbi.nlm.nih.gov/articles/PMC12821543/), [PDF](https://repositum.tuwien.at/bitstream/20.500.12708/225483/1/Aronis-2025-Journal%20of%20Functional%20Morphology%20and%20Kinesiology-vor.pdf)) |
| Shoulder extension | 91.9 ± 19.7 (youngest group); 69.8 (oldest) | 43.5 ± 8.5 (youngest); 37.7 (oldest) | Danneskiold-Samsøe / Bartels 2009, n = 174 ([R39](https://onlinelibrary.wiley.com/doi/10.1111/j.1748-1716.2009.02022.x)) |
| Shoulder adduction | 89.6 (youngest); 68.1 (oldest) | — | [R39](https://onlinelibrary.wiley.com/doi/10.1111/j.1748-1716.2009.02022.x) |
| Internal / external rotation | Isokinetic at 120°/s, controls: IR ≈ 33.7, ER ≈ 32.3 (isokinetic values are lower than isometric) | About 53 % (IR) and 57 % (ER) of male | Abdelraouf 2022 ([R42](https://onlinelibrary.wiley.com/doi/10.1155/2022/6106943)); Roy 2009 via the review ([R40](https://peerj.com/articles/16968/), [R41](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC2663552/)) |
| Elbow flexion | 69.5 (peak, at about 120°) | 35.4 (peak, at about 90°) | Untrained adults ([R43](https://experts.mcmaster.ca/display/publication236014)) |

The female/male shoulder gap is largest in flexion and in internal/external rotation ([R40](https://peerj.com/articles/16968/)). The female shoulder-flexion value in this doc is an estimate and the weakest number here. Replace it with the user's own test.

### 3.5 Whole-arm steering torque capacity

| Data | Value | Comment |
|---|---|---|
| FSAE drivers, seated, two hands, 254 mm wheel ([R52](https://www.designjudges.com/articles/cockpit-control-forces), [R53](https://www.fsaeonline.com/content/cockpit%20control%20forces%20si%20sae.pdf)) | **34–47 Nm** static peak, varying with reach (extended, mid, close) | About 134–185 N per hand **(derived)**. Closer to the chest gives more torque. **Best seated reference.** |
| NHTSA DOT HS 809 875, double lane change ([R54](https://manuals.plus/m/5ce3edc0e3438f2a29177023d7cb16e53d13941355f3b6a05a46ad5be3907463)) | Highest instantaneous peak 46.0 Nm; filtered peaks about 19–20.5 Nm; about **27.4 Nm** sustainable for 500 ms | Mirror copy (**verify**). "Drivers are unable to sustain large steering inputs for long periods." |
| Sanders 1981, 269 truck/bus drivers, truck wheel ([R51](https://journals.sagepub.com/doi/abs/10.1177/001872088102300602)) | Mean peak 147 Nm. Decays 12 % at 5 s, 17 % at 10 s and 21 % at 15 s | Large-diameter wheel and a strong, mostly male cohort. Shows how fast torque decays. |
| Seat distance study, n = 27 ([R55](https://www.jstage.jst.go.jp/article/jpts/25/9/25_jpts-2013-126/_article)) | Max torque at seat distance about 50 % of arm length; men > women | Nm values not retrieved |
| Standing valve handwheels ([R61](https://doi.org/10.1080/24725838.2019.1624283), [R62](https://www.researchgate.net/publication/245095718_Effects_of_wheel_design_on_the_torques_applied_to_large_hand_wheels)) | Men 109–152 Nm, women 66–91 Nm (large wheels). Females at 45–51 % or about 60–66 % of male. A 70 cm wheel gives +89.7 % vs a 35 cm wheel | Standing, whole body. **Upper bounds only, not comparable to sitting.** |

**One-handed capacity.** No direct seated one-hand steering norm was found. As a prior use about 45–50 % of the two-hand value, which is the per-hand figure from the FSAE data **(derived)**. It is probably lower for the hand pushing up against gravity (§4.1).

### 3.6 Endurance-time relationships (isometric)

**Rohmert (1960)**, as tabulated in [R45](https://arxiv.org/pdf/1104.1190), for 0.15 ≤ f ≤ 1 where f is the fraction of MVC:

ET [min] = −1.5 + 2.1/f − 0.6/f² + 0.1/f³

| %MVC | 15 | 20 | 25 | 30 | 40 | 50 | 60 | 70 | 80 | 100 |
|---|---|---|---|---|---|---|---|---|---|---|
| ET (derived) | 15.5 min | 6.5 min | 3.7 min | 2.5 min | 94 s | 66 s | 48 s | 34 s | 23 s | 6 s |

Caveats:
- **Joint-specific.** Frey Law & Avin's meta-analysis of 194 studies found that a power model fits best and that a single generic curve does not fit all joints. The shoulder is most fatigable, followed by knee, grip and elbow, trunk and ankle ([R46](https://www.researchgate.net/publication/41002103_Endurance_time_is_joint-specific_A_modelling_and_meta-analysis_investigation), [Ergonomics](https://www.tandfonline.com/doi/full/10.1080/00140130903389068)). Their per-joint coefficients were **not retrieved**; get them from the paper before using a joint-specific model.
- **Large spread.** Individual endurance times vary about 29–47 % around the mean curve ([R46](https://www.researchgate.net/publication/41002103_Endurance_time_is_joint-specific_A_modelling_and_meta-analysis_investigation)).
- **Rohmert can be optimistic at low effort.** A NIOSH shoulder study found that Rohmert overestimates endurance at low %MVC ([R48](https://stacks.cdc.gov/view/cdc/188634)). The old rule that "below 15 % MVC can be held indefinitely" has been challenged (Mathiassen & Åhsberg 1999, as discussed in [R45](https://arxiv.org/pdf/1104.1190)).
- A fitted design formula for 5–40 % MVC is t = 20.5·e^(−0.068·%MVC) minutes ([R49](https://health.usf.edu/media/zculuw1w/swdesignm31.pdf)).
- **Raised forearm.** Handgrip endurance at 15 % MVC was about 14.5 % shorter with the forearm 27.5 cm above heart level ([R50](https://www.nature.com/articles/s41598-024-83939-7)).
- A three-compartment fatigue model (Frey-Law, Looft, Heitsman 2012) predicts joint-specific endurance and is a better long-term model than Rohmert ([R47](https://pmc.ncbi.nlm.nih.gov/articles/PMC3397684/)).

---

## 4. Biomechanics of holding a wheel against torque

### 4.1 From wheel torque to hand forces

- **Statics.** τ_h = Σ_i (r_i × F_i) + Σ_i M_i,axial. The first term is the hands' tangential forces times the radius. The second is any free moment a hand applies about the column axis, which comes from forearm pronation/supination when the forearm is roughly parallel to the column. The lever term dominates. 40 N at 0.15 m is 6 Nm per hand, while forearm "twist" can add at most about 4–8 Nm per arm (§3.1).
- **Direction depends on clock position.** Measure β from 12 o'clock, clockwise. For a clockwise (right-turn) torque, each hand's force direction is (cos β, −sin β), with x to the right and y up.
  - At 9 o'clock the force is straight **up** (shoulder flexion, elbow, wrist radial deviation in a thumb-up grip).
  - At 3 o'clock it is straight **down** (shoulder extension and adduction, wrist ulnar deviation).
  - At 12 and 6 it is horizontal (shoulder horizontal ad/abduction and internal/external rotation, which are weaker; §3.4).
- **Hand positions move with the wheel.** If the hands don't slide, β_i(t) = β_i0 + φ(t). So *the muscles doing the work change with wheel angle*. A ±90° rep at 9-and-3 sweeps the load from up/down to sideways.
- **The split between hands is not 50/50.** In bimanual steering the arm moving *with* gravity exerts more steering force than the arm moving against it. People also perceive gravity-assisted forces as smaller than they are ([R56](https://link.springer.com/article/10.1007/s00221-013-3676-0); unimanual follow-up [R57](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0223930)).
- **Muscles involved.** A musculoskeletal steering model names the deltoid, infraspinatus, latissimus dorsi, subscapularis and triceps heads as the steering-torque generators ([R60](https://www.researchgate.net/publication/265734098_Muscle_activity_and_co-contraction_of_musculoskeletal_model_during_steering_maneuver)). EMG of 10 shoulder muscles shows their function depends on the rotation and torque direction ([R59](https://link.springer.com/article/10.1007/s11431-012-5045-9)).

### 4.2 Where each hand's force is felt (static moments)

- **Shoulder:** M_sh ≈ F_vert × L_h ± arm weight × L_cm. L_h is the horizontal distance from shoulder to hand. Arm weight adds to the lifting side and subtracts from the pulling-down side.
- **Wrist:** M_w ≈ F × d_wg, where d_wg is the distance from the wrist centre to the centre of the fist. It acts as radial/ulnar deviation for a thumb-up grip and as flexion/extension for a palms-down grip.
- **Elbow:** depends on elbow angle. It is not modelled live because it needs arm geometry.
- **Reach matters.** Peak driver torque rises as the wheel comes closer to the chest ([R52](https://www.designjudges.com/articles/cockpit-control-forces)), and maximum torque came at a seat distance of about 50 % of arm length ([R55](https://www.jstage.jst.go.jp/article/jpts/25/9/25_jpts-2013-126/_article)).

### 4.3 Grip and friction: why the rim taxes the hand

At 9 and 3 the rim runs vertically through the fist, and the tangential force runs **along** the rim. Unless the thumb hooks a spoke, the only thing stopping the hand from sliding is friction.

- **Model.** Axial capacity is the sum of μ × normal force over the contact areas ([R66](https://stacks.cdc.gov/view/cdc/215050/cdc_215050_DS1.pdf)). With palm and finger sides both loaded, total normal force is about 2G, where G is the squeeze a dynamometer would read. This gives G_req ≈ SM × F / (2μ).
- **Friction coefficients:**

  | Surface | μ | Source |
  |---|---|---|
  | Finger pad on aluminium | ≈0.6 | [R63](https://www.researchgate.net/publication/24416570_Friction_coefficients_in_a_longitudinal_direction_between_the_finger_pad_and_selected_materials_for_different_normal_forces_and_curvatures) |
  | Finger pad on rubber | ≈0.9 | [R63](https://www.researchgate.net/publication/24416570_Friction_coefficients_in_a_longitudinal_direction_between_the_finger_pad_and_selected_materials_for_different_normal_forces_and_curvatures) |
  | Palm | 0.62 ± 0.22 | [R65](https://www.researchgate.net/publication/44624351_Frictional_properties_of_different_hand_skin_areas_and_grasping_techniques) |
  | Suede, dry / moist | 0.39 / 0.66 | NIOSH-hosted study ([R64](https://stacks.cdc.gov/view/cdc/215047/cdc_215047_DS1.pdf), **verify** the attribution) |
  | Leather on steel (not skin) | static ≈0.40 | [R68](http://nanovea.com/App-Notes/leather-friction.pdf) |
  | Skin, grip experiments generally | > 0.2 | Review ([R70](https://www.researchgate.net/profile/Mati-Ullah-Shah/post/Damping_coefficients_for_skin_and_fat/attachment/624346cdce371e4882545751/AS:1139025314365445@1648576205160/download/159150092.pdf)) |

  Friction falls as normal force rises (−46 % from 1 to 20 N on the finger pad) ([R63](https://www.researchgate.net/publication/24416570_Friction_coefficients_in_a_longitudinal_direction_between_the_finger_pad_and_selected_materials_for_different_normal_forces_and_curvatures)). Moisture raises friction on porous materials such as suede ([R64](https://stacks.cdc.gov/view/cdc/215047/cdc_215047_DS1.pdf)). Moisture and smoothness drive grip comfort on leather rims ([R69](https://www.jstage.jst.go.jp/article/jte/57/6/57_6_139/_article)). No measured skin-on-Alcantara value was found, so use about 0.4–0.6 as the default range.
- **Safety margin.** People grip harder than the slip limit. The grip:load ratio is kept above a friction-determined "slip ratio" by a margin ([R67](https://www.diva-portal.org/smash/get/diva2:864178/FULLTEXT01.pdf)). SM = 1.4 is used here as an **assumed** default.
- **Consequence.** At 40 N per hand with μ = 0.5 and SM = 1.4, G_req ≈ 56 N, about 5.7 kgf per hand, held continuously and with raised arms (§1.2, §3.6). Hooking the thumbs on the spokes turns part of the load into bearing contact and lowers grip demand by an unknown amount.

### 4.4 Co-contraction, stiffness and perception

- Opposing a steady offset torque and voluntarily co-contracting both raise arm stiffness and damping ([R58](https://www.scilit.net/publications/43bdd2070a1e817209f3d35af3c76dcb)). Users "brace", which costs metabolic effort without adding net torque.
- Perceived exertion follows relative intensity, %MVC ([R85](https://bura.brunel.ac.uk/bitstream/2438/8654/2/Fulltext.pdf)). This is why a load that is small in absolute terms feels hard when the limiting muscle is small.

---

## 5. Real-car reference points (for the "this is like…" overlay)

| Situation | Steering-wheel torque | Confidence / source |
|---|---|---|
| Road car, normal driving | 0–2 Nm | Steer-by-wire thesis ([R71](https://ddl.stanford.edu/sites/g/files/sbiybj25996/files/media/file/2005_thesis_yih_steer_by_wire_implications_for_vehicle_handling_and_safety_0.pdf), [page](http://www-cdr.stanford.edu/dynamic/bywire/steering)) |
| Sedan / performance road car | 2–3 / 5–7 Nm | Vendor ([R72](https://www.fanatec.com/us/en/explorer/products/racing-wheels-wheel-bases/how-realistic-is-fanatec-torque-compared-to-a-real-car/)); everyday 4–6 Nm (forum, [R75](https://www.eng-tips.com/threads/steering-torque-required.84489/)) |
| Road car at the grip limit (steady) | ≈5 Nm | Forum, anecdotal ([R76](https://www.f1technical.net/forum/viewtopic.php?t=20074)) |
| Road car emergency manoeuvre | up to 15 Nm | [R71](https://ddl.stanford.edu/sites/g/files/sbiybj25996/files/media/file/2005_thesis_yih_steer_by_wire_implications_for_vehicle_handling_and_safety_0.pdf) |
| Measured double lane change (NHTSA) | Peak 46 Nm; about 27 Nm sustainable for 0.5 s | [R54](https://manuals.plus/m/5ce3edc0e3438f2a29177023d7cb16e53d13941355f3b6a05a46ad5be3907463) (**verify**) |
| Legal limit, intact steering (UNECE R79, M1) | ≤ 150 N rim effort to reach a 12 m radius in 4 s. With a failed system: ≤ 300 N to reach 20 m in 4 s | [R77](https://unece.org/sites/default/files/2024-04/R079r5e.pdf), [EUR-Lex](https://eur-lex.europa.eu/eli/reg/2025/3/oj/eng). At an assumed 0.185 m wheel radius that is about 28 and 56 Nm **(derived)**. |
| Parking, non-assisted small car (Daewoo Matiz) | ≈26–33 Nm (static) | [R78](https://annals.fih.upt.ro/pdf-full/2013/ANNALS-2013-2-19.pdf) (**verify**, the table is garbled) |
| Stationary vs moving, column torque (one vehicle, no hydraulic assist) | 4.55 vs 0.55 Nm | SAE 2018 ([R79](https://saemobilus.sae.org/papers/measurement-loads-applied-a-steering-system-tie-rod-steering-column-2018-36-0280)) |
| Vintage and club race cars without power steering | Lotus 20 4.0–4.5 Nm (330 mm wheel); Lola T332 5.5–8.5 Nm (318 mm); Autokraft Midget 6.0 Nm (380 mm). Dodge Viper SRT-10 4.5 Nm with power steering. FSAE cars about 4–11 Nm at standstill | Static torque table ([R52](https://www.designjudges.com/articles/cockpit-control-forces)) |
| GT3 | 6–8 Nm (vendor); about 10 Nm; 10–12 Nm (telemetry estimates) | [R72](https://www.fanatec.com/us/en/explorer/products/racing-wheels-wheel-bases/how-realistic-is-fanatec-torque-compared-to-a-real-car/), [R74](https://simracingmanual.com/hardware/wheelbase-torque-guide/), [R73](https://racingrigguide.com/blog/fanatec-clubsport-dd-review-is-more-nm-always-better-2026). A forum claim of about 60 Nm ([R84](https://community.granitedevices.com/t/how-close-to-real-life-is-the-steering-forces-we-have-in-iracing/10267)) is unverified. |
| Open-wheel / formula cars | "More than 30 Nm" (review claim) | [R73](https://racingrigguide.com/blog/fanatec-clubsport-dd-review-is-more-nm-always-better-2026), **low confidence** |
| F1 | **No reliable measured figure found.** Hydraulic assist is allowed only to reduce effort, and electronic power steering has been banned since 2002 ([R80](https://rtrsports.com/en/do-f1s-have-power-steering/)). Unsourced estimate of 20–40 Nm ([R81](https://wiibook.net/how-many-nm-does-a-f1-steering-wheel-have/)). A secondhand "Honda study: > 350 N" effort claim surfaced in search results with no primary found. | **Very low confidence. Do not display as fact.** |
| Karts (no power steering) | No measured human-at-wheel data found. Design calculations give about 6.7 Nm static and 16.8 Nm under full braking at the steering arm ([R82](https://www.sciencedirect.com/science/article/abs/pii/S2214785320371297)), and about 5.9 Nm column torque (≈39 N effort) ([R83](https://www.researchgate.net/publication/354522895_NUMERICAL_INVESTIGATION_OF_LADDER_FRAME_KART)) | Low confidence |

The display should use bands rather than single numbers: road (0–5), road emergency / GT3 (6–15), parking without assist and NHTSA peaks (25–46). F1 should be labelled as unknown.

---

## 6. Live metrics: formulas for the app

### 6.0 Inputs, sign conventions and calibration

**Per-sample inputs (≥ 200 Hz suggested):**
- u(t) ∈ [−1, 1]: the app's own constant-force command
- φ(t): wheel angle (rad), from the axis value × half the rotation range
- ω = dφ/dt and α = dω/dt, both low-pass filtered (e.g. 10–20 Hz)

**Configuration:**
- T_rated (9 / 12 / 18 / 21 / 28 Nm)
- s_sp: SimPro overall-strength fraction
- k_cal: calibration factor, default 1
- r_g: effective grip radius. Default D/2 − 0.015 m (rim centreline). Offer "D/2" for simplicity.
- Hand mode: two-hand with β_L0, β_R0 (default −90° / +90°), one-hand β_0, or hub-grip (forearm trainer, §7)
- User data: sex, age, body mass BM, measured grip G_MVC, reach L_h (default 0.45 m), d_wg (default 0.075 m), and τ_max,user from the tests in §7

**Motor torque:** τ_m(t) = u · T_rated · s_sp · k_cal

**Human torque** (from the wheel's equation of motion I_w·α = τ_m + τ_h):
τ_h(t) = I_w·α(t) − τ_m(t) [− damper/friction terms if SimPro's are on, so set them to 0]
- Positive τ_h means the user pushes clockwise.
- In a static hold τ_h = −τ_m.

**Calibration routines:**
1. **Torque check with a luggage scale** (sets k_cal).
   - Hook a digital luggage scale to the rim at 3 o'clock and pull vertically to hold the wheel still while the app commands a constant u (e.g. 0.25, 0.5, 0.75).
   - τ_meas = m_scale · g · r_hook, so k_cal = τ_meas / (u · T_rated · s_sp).
   - Do this with SimPro damper, friction and inertia set to 0. This directly tests "is 50 % really 6 Nm?"
2. **Inertia I_w (hands off).**
   - Command a small step torque, e.g. 0.5–1 Nm, inside a ±30° window with soft limits, and measure the initial α.
   - I_w ≈ τ_step / α_0, after a friction correction from the coast-down.
   - A rim alone is at most m·r², e.g. 1.1 kg GT Neo ([R16](https://simagic.com/pages/details-gt-neo)) × 0.14² ≈ 0.022 kg·m² **(derived)**. The rotor and quick-release add more. At α = 30 rad/s² that is about 0.65 Nm, so inertia matters only for fast reps.

### 6.1 Rim force
F_t = |τ_h| / r_g [N]; kgf = F_t / 9.80665

### 6.2 Per-hand force and direction
- **Split:** F_i = w_i · F_t, with w_L + w_R = 1.
  - Default w = 0.5 / 0.5.
  - An optional "realistic" mode gives the hand moving *with* gravity (pulling down) w ≈ 0.55–0.6. This is a **heuristic**: the direction comes from [R56](https://link.springer.com/article/10.1007/s00221-013-3676-0), but no exact share was retrieved.
  - One-hand mode: w = 1.
- **Hand angle:** β_i(t) = β_i0 + φ(t), assuming no hand shuffling.
- **Components** (s = sign(τ_h)):
  - Vertical: F_vert,i = s · F_i · (−sin β_i) (+ is up)
  - Horizontal: F_horz,i = s · F_i · cos β_i (+ is right)
- **Display:** per-hand force (N / kgf), plus a "lift / pull / push-sideways" label from the dominant component.

### 6.3 Estimated grip demand
- G_i ≈ SM · F_i / (k · μ), with SM = 1.4, k = 2, μ = 0.5. μ is selectable per rim material: suede/Alcantara 0.4, leather 0.5, rubber 0.9. If the thumbs are on the spokes, show "≤ value".
- **Display:** G_i in kgf and as %G_MVC, using the user's own dynamometer value or the §3.3 norms. Label it "estimate".

### 6.4 Estimated wrist load
- M_w,i = F_i · d_wg
- **Display:** as %MVC of radial/ulnar deviation (thumb-up grip) or extension/flexion (palms-down grip), using §3.2 norms or the user's test. Label it "estimate".

### 6.5 Estimated shoulder load
- Lifting hand: M_sh,up = F_vert,up · L_h + m_arm · g · L_cm
- Pulling hand: M_sh,down = |F_vert,down| · L_h − m_arm · g · L_cm, floored at 0
- m_arm = 0.049 · BM ([R44](https://biomed.bas.bg/en/wp-content/uploads/2012/02/MassInertial.html)); L_cm ≈ 0.45 · L_h (assumption)
- **Display:** %MVC against shoulder flexion and extension norms (§3.4). When |F_horz| dominates (hands near 12 or 6), show "shoulder rotators / horizontal" and warn that those muscles are weaker.

### 6.6 Forearm rotation load
- **Hub-grip mode** (hands on a coaxial handle, r ≈ 0): M_fa = |τ_h| shared by both forearms (pronation on one side, supination on the other). One-handed: M_fa = |τ_h|. Display it as %MVC against §3.1 norms.
- **Rim mode:** the axial share is **not observable** without hand load cells, so do not show a number. Optionally show the educational ratio "if held by twisting alone: |τ_h| / (P_MVC + S_MVC)".

### 6.7 Honest "equivalent weight"
- **Dumbbell held at the same hand position (hammer grip):** m_eq = |F_vert,up| / g [kg].
  - This is mechanically exact for the static shoulder, elbow and wrist-deviation moments of the lifting hand, because the force and the point of application are the same.
  - It is **not** equivalent for grip. A dumbbell's plate bears on the fist, while the rim needs friction (§4.3).
- **Straight-arm front raise:** m_front = m_eq · L_h / L_arm, with L_arm being the shoulder-to-fist distance with a straight arm (default 0.6 m).
  - Example: 4.1 kg at the wheel is about 3.1 kg in a straight-arm front raise **(derived)**.
- **Pulling hand:** "like a straight-arm cable pull-down of m_eq kg".
- **Do not** call 12 Nm "8 kg" without saying that it is split between two hands and acts at the hand.

### 6.8 Mechanical power and work
- P(t) = τ_h · ω [W]
  - P > 0 is concentric: the user overpowers the motor and does positive work.
  - P < 0 is eccentric: the motor wins and the user resists while yielding.
- W_con = ∫ max(P, 0) dt and W_ecc = ∫ max(−P, 0) dt [J]
- Report peak and mean power per rep. Example: 12 Nm at 2 rad/s (115°/s) is 24 W; at 2π rad/s it is about 75 W **(derived)**.
- Do not convert to kcal: muscle efficiency and the cost of isometric holding were not sourced.

### 6.9 Impulse (torque–time integral)
- Angular impulse H = ∫ |τ_h| dt [N·m·s]
- Rim impulse J = H / r_g [N·s]; per hand J_i = ∫ F_i dt
- This is the best single "volume" metric for isometric work.

### 6.10 Time under tension
TUT = ∫ 1[|τ_h| ≥ τ_thr] dt, with τ_thr = max(1 Nm, 0.10 · τ_max,user). Also report TUT within %max bands (e.g. < 30, 30–60, > 60 %).

### 6.11 Percent of estimated max
- f(t) = |τ_h| / τ_max,user(mode, direction, angle band)
- If no test has been done, use these priors:
  - Two-hand: τ_max ≈ 2 · F_hand,max · r_g, with F_hand,max ≈ 135–185 N for men (FSAE drivers, [R52](https://www.designjudges.com/articles/cockpit-control-forces), **derived**) and about 50–60 % of that for women (est., [R61](https://doi.org/10.1080/24725838.2019.1624283), [R62](https://www.researchgate.net/publication/245095718_Effects_of_wheel_design_on_the_torques_applied_to_large_hand_wheels)).
  - Hub-grip: τ_max ≈ P_MVC + S_MVC (§3.1).
  - Show the prior as a range and label it.

### 6.12 Fatigue budget (heuristic)
- D(t) = ∫ dt / ET(f(t)), with ET from Rohmert (§3.6).
- For f < 0.15, use ET(0.15) = 15.5 min as a conservative floor, because Rohmert can overestimate at low effort ([R48](https://stacks.cdc.gov/view/cdc/188634)).
- Predicted time left at the current load: t_left ≈ ET(f) · (1 − D).
- Fit a per-user correction over time from observed failures, since individual endurance varies about ±30–50 % ([R46](https://www.researchgate.net/publication/41002103_Endurance_time_is_joint-specific_A_modelling_and_meta-analysis_investigation)).
- Upgrade path: the three-compartment model ([R47](https://pmc.ncbi.nlm.nih.gov/articles/PMC3397684/)) with shoulder or grip parameters.

### 6.13 Worked example (12 Nm, 300 mm rim, 9-and-3, static, 50/50)

All values are **(derived)**.

| Metric | Male, 80 kg | Female, 65 kg |
|---|---|---|
| F_t; per hand | 80 N (8.2 kgf); 40 N (4.1 kgf) | same |
| m_eq at the hand; straight-arm front raise | 4.1 kg; ≈3.1 kg | same |
| Grip estimate | 56 N (5.7 kgf): ≈11 % dynamometer, ≈20 % rim max | ≈18 %; ≈29 % |
| Wrist radial deviation | 3.0 Nm: ≈29 % | ≈48 % |
| Lifting shoulder | ≈25.7 Nm: ≈33 % (Rohmert ET ≈ 2 min) | ≈24.2 Nm: ≈60 % (≈48 s) |
| Whole-system | ≈22–30 % of 40–55 Nm | ≈36–60 % of ~20–33 Nm |
| 10 × 10 s holds | H = 1200 N·m·s; J = 8000 N·s; TUT = 100 s | same |

### 6.14 What not to show
- No "calories".
- No single "muscle %" for rim mode.
- No F1 comparisons presented as fact.
- No claim that the equivalent weight matches grip demand.
- Always show a small "estimate, assumptions" link for §6.3–6.7 and §6.11–6.12.

---

## 7. The breakpoint test and training modes (practical implications)

- **The 12 Nm ceiling limits the two-hand breakpoint test.** Expected two-hand maxima for men are about 40–55 Nm on a 300 mm rim (§3.5). A two-hand ramp-to-failure on a 12 Nm base will mostly return "> 12 Nm" for men. Options:
  1. **Hold-to-failure at a known torque and invert the endurance curve.** For example, holding 12 Nm for 66 s gives f ≈ 0.5, so τ_max ≈ 24 Nm **(derived; ±30–50 %)**.
  2. **Test weaker configurations the base can saturate:** one hand, hands at 12 or 6, arms more extended, or hub-grip forearm rotation (12 Nm is above most people's pronation + supination maximum, §3.1).
  3. **Shrink the radius.** The same torque on a smaller grip radius means more force at the hand (F = τ / r), e.g. 12 Nm at r = 0.10 m is 120 N. A small rim or handle lets the 12 Nm base load grip and wrist harder.
- **Breakpoint definition.** Ramp τ_m slowly (e.g. 0.5 Nm/s). Failure is when |Δφ| > 10° from the hold angle, or when |ω| stays above about 0.5 rad/s for more than 0.3 s against the user's intended direction. Record it per direction (CW / CCW) and per hand mode, because the up-pushing hand is the weak side (§4.1).
- **Modes that make the most of 12 Nm:**
  - Two-hand isometric endurance: shoulders, 20–40 % MVC.
  - One-hand or hub-grip strength work: forearm and wrist, 50–100 % MVC.
  - Eccentric "yielding" sets: log W_ecc.
  - Angle sweeps that move the load across shoulder muscle groups.

---

## 8. Safety notes

- Direct-drive bases can move the rim violently. Ramp torque, cap the angle range, keep a big stop button and a dead-man rule (release, then torque → 0), and keep the thumbs outside the spokes.
- In hub-grip mode 12 Nm can exceed forearm maxima (§3.1). Start at 10–20 % and increase progressively.
- High-volume gripping loads the wrist extensors. Watch for lateral elbow pain ([R31](https://pubmed.ncbi.nlm.nih.gov/36573481/)).

---

## 9. Open items to verify

1. Delivered torque versus the SimPro slider on the user's own unit (luggage-scale test, §6.0).
2. Simagic's official EVO manual for any continuous-torque or thermal-derating figure ([R3](https://image.simagic.com/profile/upload/2025/04/28/SIMAGIC_Alpha%20EVO_UM_EN.pdf), not readable in this session).
3. Frey Law & Avin's joint-specific power-model coefficients ([R46](https://www.tandfonline.com/doi/full/10.1080/00140130903389068)), to replace the generic Rohmert curve.
4. Female shoulder-flexion isometric torque in Nm (e.g. Hughes et al. 1999; Holzbaur 2007 at <https://nmbl.stanford.edu/publications/pdf/Holzbaur2007.pdf>, not retrieved).
5. Seated one-hand maximum push-up and pull-down forces at reach. Military anthropometric strength tables would be the source; not retrieved because the search budget ran out.
6. Measured skin-on-Alcantara friction.
7. The garbled tables in [R54](https://manuals.plus/m/5ce3edc0e3438f2a29177023d7cb16e53d13941355f3b6a05a46ad5be3907463), [R78](https://annals.fih.upt.ro/pdf-full/2013/ANNALS-2013-2-19.pdf) and Matsuoka 2006 (via [R25](https://peerj.com/articles/16557/)).

---

## 10. References

Hardware
- R1 Simagic, Alpha EVO page: https://simagic.com/pages/alpha-evo
- R2 Simagic news, Alpha EVO series: https://simagic.com/blogs/news/alpha-evo-series-out-now-html
- R3 Simagic Alpha EVO user manual (PDF): https://image.simagic.com/profile/upload/2025/04/28/SIMAGIC_Alpha%20EVO_UM_EN.pdf
- R4 PC Games Hardware, EVO Pro 21 Nm update: https://www.pcgameshardware.de/Lenkraeder-Hardware-255542/News/Simagic-Alpha-EVO-Pro-21-Nm-Update-1556804/
- R5 The SimRacing Den, EVO Pro 21 Nm upgrade: https://www.thesimracingden.com/news-simagic-evo-pro-free-21nm-upgrade
- R6 Traxion, Alpha EVO Pro review: https://traxion.gg/simagic-alpha-evo-pro-review-a-powerful-direct-drive-bargain/
- R7 Simracingcockpit.gg, EVO range specs and buyer's guide: https://simracingcockpit.gg/simagic-alpha-evo-range-specifications-and-buyers-guide
- R8 Simracing Manual, Alpha EVO review: https://simracingmanual.com/hardware/wheelbases/simagic-alpha-evo/
- R9a G-Performance listing: https://gperformance.eu/products/simagic-alpha-evo
- R9b The French Simracer listing: https://thefrenchsimracer.com/en/products/simagic-alpha-evo-12nm
- R10 TweakTown, Alpha EVO Ultra 28 Nm: https://www.tweaktown.com/reviews/11368/simagic-alpha-evo-ultra-28nm-wheelbase/index.html
- R11 DirectDrive.it, Sport/EVO/Pro/Ultra: https://www.directdrive.it/en-eu/blogs/news/simagic-alpha-evo-sport-evo-pro-and-ultra-which-to-choose
- R12 Apex300, which EVO to buy: https://apex300.com/blogs/news/which-simagic-alpha-evo-should-you-buy
- R13 Gonky Racing, Alpha EVO AMS2 FFB guide: https://gonkyracing.com/en/ffb-guide/simagic-alpha-evo
- R14 Simracingcockpit.gg, ACC Simagic settings: https://simracingcockpit.gg/acc-simagic-settings/
- R15 Hotlap, Simagic Alpha BeamNG settings: https://hotlap.cc/en/ffb-settings/simagic-alpha/beamng/
- R16 Simagic GT Neo: https://simagic.com/pages/details-gt-neo
- R17 Simagic FX: https://simagic.com/pages/details-fx
- R18 Simagic Zeus Formula: https://simagic.com/pages/detail-simagic-zeus-formula-steering-wheel
- R19 Simagic Zeus GT: https://simagic.com/pages/detail-simagic-zeus-gt-steering-wheel
- R20 Apex Sim Racing, GT1S: https://www.apexsimracing.com/products/gt1s-round-d-shaped-wheel
- R21 Simmotion, Simagic rim only: https://simmotion.com/products/simagic-steering-wheel
- R22 RSeat Europe, Simagic wheels: https://www.rseat-europe.com/simagic-products/simagic-steering-wheels
- R23 SIMGASM, GT Neo vs FX Pro vs round: https://simgasm.com/blogs/articles/choosing-a-simagic-steering-wheel-gt-neo-vs-fx-pro-vs-round-rims
- R24 Simracingcockpits, GT Neo review: https://simracingcockpits.com/simagic-gt-neo-steering-wheel-review/

Strength norms
- R25 Sex differences in wrist strength: a systematic review (PeerJ 2023): https://peerj.com/articles/16557/ (PMC: https://pmc.ncbi.nlm.nih.gov/articles/PMC10725665/)
- R26 The interacting effects of forearm rotation and exertion direction on male and female wrist strength (Int J Ind Ergon): https://www.sciencedirect.com/science/article/abs/pii/S0169814114001796 ; https://www.researchgate.net/publication/270595529_The_interacting_effects_of_forearm_rotation_and_exertion_direction_on_male_and_female_wrist_strength
- R27 Axelsson et al., Forearm torque and lifting strength: normative data (J Hand Surg 2018): https://www.jhandsurg.org/article/S0363-5023(17)30770-0/fulltext
- R28 Normative forearm torque data: Korean population (Arch Hand Microsurg): https://www.handmicro.org/journal/view.php?doi=10.12790%2Fahm.24.0054
- R29 Upper-limb surface EMG at maximum supination and pronation torques (2002): https://pubmed.ncbi.nlm.nih.gov/12121684/
- R30 Plewa, Potvin, Dickey 2016, Wrist rotations about one or two axes affect maximum wrist strength: https://ir.lib.uwo.ca/boneandjointpub/350 ; https://www.sciencedirect.com/science/article/abs/pii/S0003687015300776
- R31 Ikeda et al. 2023, Sex differences in wrist torque and endurance (lateral epicondylitis): https://pubmed.ncbi.nlm.nih.gov/36573481/
- R32 Eschweiler et al., reply (cites Yoshii 2015 wrist torques): https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9410372/
- R33 Dodds et al. 2014, Grip strength across the life course: https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0113637 ; https://pubmed.ncbi.nlm.nih.gov/25474696/
- R34 SRALab, hand-held dynamometer / grip strength: https://www.sralab.org/rehabilitation-measures/hand-held-dynamometer-grip-strength
- R35 Tromsø study, Jamar+ reference values: https://link.springer.com/article/10.1007/s10433-023-00791-w
- R36 Hand grip reference values, older adults, Poland (EWGSOP2 cut-offs): https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8113334/
- R37 Eksioglu & Kizilaslan, Steering-wheel grip force characteristics: https://www.researchgate.net/publication/229381284_Steering-wheel_grip_force_characteristics_of_drivers_as_a_function_of_gender_speed_and_road_condition
- R38 Aronis et al. 2025, Maximum shoulder torque during standing arm flexion: https://pmc.ncbi.nlm.nih.gov/articles/PMC12821543/
- R39 Danneskiold-Samsøe et al. 2009, Isokinetic and isometric muscle strength in a healthy population: https://onlinelibrary.wiley.com/doi/10.1111/j.1748-1716.2009.02022.x
- R40 Sex differences in strength at the shoulder: a systematic review (PeerJ 2024): https://peerj.com/articles/16968/
- R41 Roy et al. 2009, Rotational strength in unaffected shoulders: https://www.ncbi.nlm.nih.gov/pmc/articles/PMC2663552/
- R42 Abdelraouf 2022, Isokinetic shoulder ratios in recreational weightlifters: https://onlinelibrary.wiley.com/doi/10.1155/2022/6106943
- R43 Elbow flexion strength curves in untrained men and women and male bodybuilders: https://experts.mcmaster.ca/display/publication236014
- R44 Geometrical and mass-inertial characteristics of the upper human limb: https://biomed.bas.bg/en/wp-content/uploads/2012/02/MassInertial.html

Endurance and fatigue
- R45 Ma et al., A novel approach for determining fatigue resistances (Rohmert equation table): https://arxiv.org/pdf/1104.1190
- R46 Frey Law & Avin 2010, Endurance time is joint-specific: https://www.researchgate.net/publication/41002103_Endurance_time_is_joint-specific_A_modelling_and_meta-analysis_investigation ; https://www.tandfonline.com/doi/full/10.1080/00140130903389068
- R47 Frey-Law, Looft, Heitsman 2012, Three-compartment fatigue model: https://pmc.ncbi.nlm.nih.gov/articles/PMC3397684/
- R48 The effect of MVC on endurance times for the shoulder girdle (NIOSH/CDC): https://stacks.cdc.gov/view/cdc/188634
- R49 USF, Design of static effort: https://health.usf.edu/media/zculuw1w/swdesignm31.pdf
- R50 Forearm elevation impairs local static handgrip endurance (Sci Rep 2025): https://www.nature.com/articles/s41598-024-83939-7
- R51 Sanders 1981, Peak and sustained isometric forces applied to a truck steering wheel: https://journals.sagepub.com/doi/abs/10.1177/001872088102300602

Steering biomechanics
- R52 DesignJudges, Cockpit control forces: https://www.designjudges.com/articles/cockpit-control-forces
- R53 FSAE, Cockpit control forces (PDF): https://www.fsaeonline.com/content/cockpit%20control%20forces%20si%20sae.pdf
- R54 NHTSA DOT HS 809 875, An assessment of human driver steering capability (mirror): https://manuals.plus/m/5ce3edc0e3438f2a29177023d7cb16e53d13941355f3b6a05a46ad5be3907463
- R55 Yoo et al. 2013, Maximal torque and seat distance from the steering wheel: https://www.jstage.jst.go.jp/article/jpts/25/9/25_jpts-2013-126/_article
- R56 Sakajiri et al. 2013, Gravity and arm-movement direction in bimanual steering: https://link.springer.com/article/10.1007/s00221-013-3676-0
- R57 Force perceptual bias in unimanual steering (PLOS ONE 2019): https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0223930
- R58 Pick & Cole, Dynamic properties of a driver's arms holding a steering wheel: https://www.scilit.net/publications/43bdd2070a1e817209f3d35af3c76dcb
- R59 Function of shoulder muscles of driver in vehicle steering maneuver: https://link.springer.com/article/10.1007/s11431-012-5045-9
- R60 Muscle activity and co-contraction during steering (musculoskeletal model): https://www.researchgate.net/publication/265734098_Muscle_activity_and_co-contraction_of_musculoskeletal_model_during_steering_maneuver
- R61 Effect of handwheel diameter and orientation on torque production (2019): https://doi.org/10.1080/24725838.2019.1624283 ; https://www.researchgate.net/publication/333473873_Effect_of_Handwheel_Diameter_and_Orientation_on_Torque_Production_Capabilities
- R62 Effects of wheel design on the torques applied to large hand wheels: https://www.researchgate.net/publication/245095718_Effects_of_wheel_design_on_the_torques_applied_to_large_hand_wheels

Friction and grip
- R63 Seo & Armstrong, Friction coefficients between finger pad and materials: https://www.researchgate.net/publication/24416570_Friction_coefficients_in_a_longitudinal_direction_between_the_finger_pad_and_selected_materials_for_different_normal_forces_and_curvatures
- R64 NIOSH-hosted palmar skin friction study (suede wet/dry): https://stacks.cdc.gov/view/cdc/215047/cdc_215047_DS1.pdf
- R65 Frictional properties of different hand skin areas and grasping techniques: https://www.researchgate.net/publication/44624351_Frictional_properties_of_different_hand_skin_areas_and_grasping_techniques
- R66 Seo, Armstrong et al., Handle friction and maximum axial push force: https://stacks.cdc.gov/view/cdc/215050/cdc_215050_DS1.pdf
- R67 Johansson, "To grip and not to slip" (slip ratio and safety margin): https://www.diva-portal.org/smash/get/diva2:864178/FULLTEXT01.pdf
- R68 Nanovea, Leather coefficient of friction: http://nanovea.com/App-Notes/leather-friction.pdf
- R69 Influence of surface coating on grip comfort of leather steering wheels: https://www.jstage.jst.go.jp/article/jte/57/6/57_6_139/_article
- R70 Tribology of skin: review and analysis of experimental results: https://www.researchgate.net/profile/Mati-Ullah-Shah/post/Damping_coefficients_for_skin_and_fat/attachment/624346cdce371e4882545751/AS:1139025314365445@1648576205160/download/159150092.pdf

Real-car references
- R71 Yih 2005, Steer-by-wire: implications for vehicle handling and safety (Stanford): https://ddl.stanford.edu/sites/g/files/sbiybj25996/files/media/file/2005_thesis_yih_steer_by_wire_implications_for_vehicle_handling_and_safety_0.pdf ; http://www-cdr.stanford.edu/dynamic/bywire/steering
- R72 Fanatec, How realistic is Fanatec torque compared to a real car?: https://www.fanatec.com/us/en/explorer/products/racing-wheels-wheel-bases/how-realistic-is-fanatec-torque-compared-to-a-real-car/
- R73 Racing Rig Guide, ClubSport DD review: https://racingrigguide.com/blog/fanatec-clubsport-dd-review-is-more-nm-always-better-2026
- R74 Simracing Manual, wheelbase torque guide: https://simracingmanual.com/hardware/wheelbase-torque-guide/
- R75 Eng-Tips forum, steering torque required: https://www.eng-tips.com/threads/steering-torque-required.84489/
- R76 F1Technical forum, steering wheel torque: https://www.f1technical.net/forum/viewtopic.php?t=20074
- R77 UNECE Regulation No. 79 Rev.5: https://unece.org/sites/default/files/2024-04/R079r5e.pdf ; EUR-Lex 2025/3: https://eur-lex.europa.eu/eli/reg/2025/3/oj/eng
- R78 Analysis of the torque applied to the steering wheel in static conditions (Annals FIH 2013): https://annals.fih.upt.ro/pdf-full/2013/ANNALS-2013-2-19.pdf
- R79 SAE 2018-36-0280, Measurement of the loads applied to a steering system: https://saemobilus.sae.org/papers/measurement-loads-applied-a-steering-system-tie-rod-steering-column-2018-36-0280
- R80 RTR Sports, F1 power steering: https://rtrsports.com/en/do-f1s-have-power-steering/
- R81 Wiibook, F1 steering Nm (unsourced estimate): https://wiibook.net/how-many-nm-does-a-f1-steering-wheel-have/
- R82 Design and analysis of electric go-kart: https://www.sciencedirect.com/science/article/abs/pii/S2214785320371297
- R83 Numerical investigation of ladder frame kart: https://www.researchgate.net/publication/354522895_NUMERICAL_INVESTIGATION_OF_LADDER_FRAME_KART
- R84 Granite Devices community, iRacing steering forces: https://community.granitedevices.com/t/how-close-to-real-life-is-the-steering-forces-we-have-in-iracing/10267

Perception
- R85 Brunel, ratings vs %MVC (elbow flexion): https://bura.brunel.ac.uk/bitstream/2438/8654/2/Fulltext.pdf
