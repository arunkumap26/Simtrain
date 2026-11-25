# 02 — DirectInput 8 force feedback for RimForce (Simagic Alpha EVO)

Research for the RimForce device layer (C#/.NET 10, Windows), which drives a Simagic Alpha EVO wheelbase through
standard DirectInput 8 force feedback (FFB) for strength-training drills.

Research date: 2026-11-25. Nobody has tested any of this on real hardware. Section 9 is a hardware test checklist for the first session with the wheel.

## How to read this document

Each claim carries one of these evidence tags:

| Tag | Meaning |
|---|---|
| **[SRC]** | Verified by reading open-source code (file and line linked). |
| **[TEST]** | Verified from Wine's DirectInput conformance tests (`dlls/dinput/tests/force_feedback.c`). They encode Windows behaviour and are run against real Windows. |
| **[REFLECT]** | Verified in this session against the actual NuGet binaries, using reflection and ILSpy decompilation, or by compiling code against them. |
| **[DOC]** | Vendor or Microsoft documentation. Most of it was read through search-engine summaries, because `learn.microsoft.com` and `simagic.com` were not reachable from the research sandbox. Simagic's GitHub-hosted docs were read directly. |
| **[FORUM]** | Community reports: GitHub issues, guides, user anecdotes. |
| **[INFER]** | My inference from the evidence above. |
| **[UNKNOWN]** | Not established. Needs a hardware test. |

**Research limitations:** `learn.microsoft.com`, `simagic.com`, `cdn.shopify.com` (the Simagic manuals) and most forums were blocked by the sandbox network policy. Wherever possible, Microsoft-documented behaviour is cross-checked against Wine's conformance tests and SDL's source instead. The web-search budget ran out near the end of the research. Everything after that point comes from GitHub sources only. SimPro settings definitions and Simucube hands-off docs got less coverage as a result.

---

## 0. TL;DR (decisions for the device layer)

1. **Wrapper: `Vortice.DirectInput` 3.8.3** (MIT, NuGet updated 2026-03-04, targets net8.0/net9.0/net10.0, repo active in 2026). It exposes everything needed: `IDirectInputDevice8.CreateEffect`, `EffectParameters`, `ConstantForce`, `ConditionSet`/`Condition`, `PeriodicForce`, `SendForceFeedbackCommand`, `Properties.AutoCenter`, `Properties.ForceFeedbackGain`, and `GetObjectPropertiesByName("X").Range` [REFLECT].
   You must work around three traps:
   - (a) `ResultCode.InputLost`, `NotAcquired`, `Acquired` and `NotInitialized` have **wrong values**. They are missing the `0x8007` HRESULT prefix, so define your own constants [REFLECT].
   - (b) `EffectParameters.TriggerButton` must be set to `-1`. The default `0` makes `CreateEffect` fail with `DIERR_INVALIDPARAM` [FORUM, hardware-verified by another project].
   - (c) Call `SetDataFormat<RawJoystickState>()` before `Acquire()`.

   `SharpDX.DirectInput` 4.2.0 is archived (2019), but it works on .NET 8 and .NET 9 in shipping apps (MAIRA), and it builds and loads on .NET 10 [REFLECT]. It is an acceptable fallback.
2. **Sequence** (what irFFB, MAIRA, SDL and Unity-DirectInput do):
   1. `DirectInput8Create`
   2. `EnumDevices(GameControl, AttachedOnly|ForceFeedback)`
   3. `CreateDevice`
   4. `SetDataFormat(c_dfDIJoystick2)`
   5. `SetCooperativeLevel(hwnd, EXCLUSIVE|BACKGROUND)`
   6. Set `DIPROP_AUTOCENTER` = OFF and `DIPROP_RANGE`, both while unacquired
   7. Optionally set `DIPROP_FFGAIN` = 10000
   8. `Acquire`
   9. Optionally `SendForceFeedbackCommand(RESET)` and `SETACTUATORSON`
   10. `CreateEffect(GUID_ConstantForce, {CARTESIAN|OBJECTOFFSETS, X axis, dir {1}, INFINITE, gain 10000, DIEB_NOTRIGGER})`
   11. `Start(1)`
   12. Update with `SetParameters(DIEP_TYPESPECIFICPARAMS [| DIEP_NORESTART])` at **360–500 Hz**.
3. **Use `dwDuration = INFINITE` (0xFFFFFFFF, `-1` in C#). Never use a large finite value such as `int.MaxValue`.** Simagic's Windows driver stores durations as **16-bit milliseconds**, so a large finite duration wraps around and FFB "disappears" after about 35 s [FORUM, root-caused by a game-port maintainer].
4. **Architecture:** synthesize every drill (hold, spring toward any angle, pulses, vibration) in software, and stream it through **one constant-force effect**. This is how irFFB and MAIRA work. Native Spring, Damper and Friction are optional add-ons, with Simagic caveats:
   - The spring **center offset is ignored** by the firmware [SRC/FORUM].
   - The native effects are probably scaled by SimPro's "Game Spring/Damper/Friction/Inertia" sliders [INFER].
   - Only one effect per type should exist [SRC].
   - Periodic (Sine) support is shaky: only sine works, and frequent updates restart it [SRC/FORUM].
5. **Strength layering:** the commanded torque is roughly `DI magnitude/10000 × dwGain/10000 × FFGAIN/10000 × SimPro overall strength × base peak torque`. **No DirectInput path can exceed SimPro's strength setting.** Every DirectInput scalar is capped at 10000, and FFGAIN above 10000 returns `DIERR_INVALIDPARAM` [DOC/TEST]. The only way around it is to rewrite the base's `ff_strength` through Simagic's undocumented HID feature report, as the Linux driver does [SRC]. That is not recommended.
6. **Simagic gotchas:**
   - The force sign flips if SimPro's "Reverse FFB" is on (negative strength) [FORUM]. RimForce needs a direction-calibration step.
   - The wheel has no public SDK [FORUM]. A `SimagicHPR` library exists, but it is a third-party raw-HID library for pedals.
   - Exclusive acquisition is one-process-only, so games, irFFB and MAIRA must be closed [DOC/SRC].
   - Don't change the rotation range from the app [SRC].

---

## 1. The DirectInput 8 FFB sequence, step by step

### 1.1 Create and enumerate

| Step | Native | Vortice 3.8.3 | Notes |
|---|---|---|---|
| Create | `DirectInput8Create(hInst, DIRECTINPUT_VERSION, IID_IDirectInput8, &pDI, NULL)` | `IDirectInput8 di = DInput.DirectInput8Create();` | Vortice passes `GetModuleHandle(null)` [REFLECT]. |
| Enumerate | `pDI->EnumDevices(DI8DEVCLASS_GAMECTRL, cb, ctx, DIEDFL_ATTACHEDONLY \| DIEDFL_FORCEFEEDBACK)` | `di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly \| DeviceEnumerationFlags.ForceFeedback)` → `IList<DeviceInstance>` | irFFB uses exactly these flags ([irFFB.cpp L1954-1957](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L1954)) [SRC]. MAIRA 1.x uses `DeviceClass.All` with the same flags [SRC]. |
| Pick device | `DIDEVICEINSTANCE.guidInstance` | `DeviceInstance.InstanceGuid`, `.ProductName`, `.ProductGuid`, `.ForceFeedbackDriverGuid` | `ProductGuid` packs VID/PID: the first DWORD is VID\|PID<<16 and the last bytes are "PIDVID" (MAIRA 2.x `TryGetUsbIds`) [SRC]. Alternatively use `device.Properties.VendorId/ProductId` (DIPROP_VIDPID) [REFLECT]. |
| Open | `pDI->CreateDevice(guid, &dev, NULL)` | `IDirectInputDevice8 dev = di.CreateDevice(guid);` | |

Enumeration can occasionally take up to 60 s because of a Windows bug with absent hardware (USB DACs, Corsair keyboards). Run it off the UI thread ([Unity-DirectInput README](https://github.com/MrTimcakes/Unity-DirectInput)) [FORUM].

Simagic USB IDs: VID `0x3670` with PIDs `0x0500`, `0x0501` and `0x0502` for the EVO family. Older Alpha-family bases use VID `0x0483`, PID `0x0522` ([simagic-ff `hid-simagic.h`](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.h)) [SRC]. `3670:0500` is reported as the Alpha EVO Sport ([SimRig #22](https://github.com/Adiakys/SimRig/issues/22)) [FORUM]. Which of `0501` and `0502` is the EVO and which the Pro is [UNKNOWN].

### 1.2 Data format

- Use `c_dfDIJoystick2` (DIJOYSTATE2). In Vortice: `dev.SetDataFormat<RawJoystickState>()`. `RawJoystickState` mirrors DIJOYSTATE2, with X at offset 0 [REFLECT].
- SDL uses `c_dfDIJoystick2` ([SDL_dinputhaptic.c L313](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L313)), as does Unity-DirectInput. irFFB uses the older `c_dfDIJoystick` ([L1991](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L1991)) [SRC].
- **In Vortice, `Acquire()` fails with `E_INVALIDARG` (0x80070057) unless `SetDataFormat` was called first.** SharpDX's `Joystick` class sets it implicitly; Vortice's raw `CreateDevice` does not ([icsmoi CLAUDE.md](https://github.com/tuduce/icsmoi/blob/main/.claude/CLAUDE.md), verified on a real device) [FORUM].
- `DIJOFS_X == 0`. Effects reference the axis by this offset when they use `DIEFF_OBJECTOFFSETS`. In Vortice: `(int)JoystickOffset.X` == 0 [REFLECT].

### 1.3 Cooperative level: Exclusive|Background vs Exclusive|Foreground

- **Force feedback requires exclusive access.** Microsoft: "To use force-feedback effects, an application must have exclusive access to the device" ([DirectInput Cooperative Levels](https://msdn.microsoft.com/en-us/library/ee416848(v=vs.85)); [Step 2: Creating the Force-Feedback Device](https://learn.microsoft.com/en-us/previous-versions/ee418821(v=vs.85))) [DOC].
  Wine's conformance tests confirm the consequences, which pass on Windows. Without exclusive acquisition:
  - `SendForceFeedbackCommand` and `GetForceFeedbackState` return `DIERR_NOTEXCLUSIVEACQUIRED`.
  - `Download` and `Start` fail.
  - `SetParameters` silently returns `DI_DOWNLOADSKIPPED`, a success code.

  ([force_feedback.c L841-873, L3545-3559](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c#L3545)) [TEST].
- **Foreground** means the device is automatically unacquired when the window goes to the background ([SetCooperativeLevel](https://msdn.microsoft.com/en-us/library/ee417921(v=vs.85))) [DOC]. Forces then stop every time the user alt-tabs, clicks another monitor or opens a dialog.
- **Every real FFB tool uses `DISCL_EXCLUSIVE | DISCL_BACKGROUND`:**
  - irFFB ([L1995](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L1995)) [SRC]
  - MAIRA 1.x ([App.ForceFeedback.cs L323](https://github.com/mherbold/MarvinsAIRA/blob/main/App.ForceFeedback.cs#L323)) [SRC]
  - MAIRA 2.x ([DirectInput.cs L175](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/DirectInput.cs#L175)) [SRC]
  - SDL ([L303-306](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L303)) [SRC]
  - Unity-DirectInput ([DirectInputForceFeedback.cpp L100](https://github.com/MrTimcakes/Unity-DirectInput/blob/main/DirectInputForceFeedback~/DirectInputForceFeedback/DirectInputForceFeedback.cpp#L100)) [SRC]
  - icsmoi (Vortice) [SRC]

  The reason is that these tools must keep pushing forces while another window (the sim) has focus.
- **Recommendation for RimForce: Exclusive|Background**, plus an explicit app-level policy. Add an optional setting to "pause the drill when RimForce loses focus" that fades forces to zero instead of relying on DirectInput's abrupt unacquire.
- **Window handle:** pass a top-level window. Wine's implementation rejects `WS_CHILD` windows with `E_HANDLE` ([device.c L565](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c#L565)) [SRC/TEST]. SDL uses its own **message-only window** (`HWND_MESSAGE` parent) for exclusive+background FFB ([SDL_windows.c L120-125](https://github.com/libsdl-org/SDL/blob/main/src/core/windows/SDL_windows.c#L120)) [SRC]. RimForce's device thread can therefore own its own hidden window, independent of the WPF/WinUI window.
- **One exclusive owner at a time, across processes:**
  - A second `Acquire` fails with `DIERR_OTHERAPPHASPRIO`. That is `E_ACCESSDENIED` = 0x80070005, the same value as `DIERR_READONLY` and `DIERR_HANDLEEXISTS` ([dinput.h](https://github.com/wine-mirror/wine/blob/master/include/dinput.h#L162)) [SRC].
  - Non-exclusive *readers* are always allowed. "Creating force effects ... does need exclusive acquisition, which is only possible with no game running" ([Trueforce-For-All DirectInputWheel.cs header](https://github.com/Mhytee/Trueforce-For-All/blob/main/src/TrueforceForAll.Plugin/DirectInputWheel.cs)) [SRC/FORUM].
  - So the user must close iRacing, irFFB, MAIRA and other FFB apps before running RimForce. Whether SimPro Manager holds DirectInput exclusively is [UNKNOWN]. The Linux driver project shows SimPro configures the base over hidraw feature reports, not DirectInput [SRC], so SimPro should coexist [INFER].
- Set the cooperative level while **unacquired**, or you get `DIERR_ACQUIRED` ([device.c L579](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c#L579)) [TEST/SRC].

### 1.4 Properties: AUTOCENTER, FFGAIN, RANGE

| Property | What | When | Vortice |
|---|---|---|---|
| `DIPROP_AUTOCENTER` (DIPH_DEVICE) | `DIPROPAUTOCENTER_OFF` (0) disables the driver's built-in centering spring. MS tutorial recommends turning it off ([ee418821](https://learn.microsoft.com/en-us/previous-versions/ee418821(v=vs.85))) [DOC]. | **Before `Acquire`.** Setting it while acquired returns `DIERR_ACQUIRED` ([device.c L910-911](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c#L910)) [SRC]. Must be DIPH_DEVICE, otherwise DIERR_UNSUPPORTED ([tests L3521-3529](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c#L3521)) [TEST]. | `dev.Properties.AutoCenter = false;` (throws `SharpGenException` if unsupported) [REFLECT] |
| `DIPROP_FFGAIN` (DIPH_DEVICE) | Device-wide gain, 0..10000, which scales all effects. ">10000 → DIERR_INVALIDPARAM" ([ee417675](https://msdn.microsoft.com/en-us/library/ee417675(v=vs.85))) [DOC]; Wine rejects >10000 ([device.c L938](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c#L938)) [SRC]. | Can be set while acquired, and is sent to the device when exclusively acquired ([device.c L1312-1317](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c#L1312), [tests L3586-3596](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c#L3586)) [TEST]. Default is 10000 [TEST L3462-3464]. | `dev.Properties.ForceFeedbackGain = 10000;` |
| `DIPROP_RANGE` (per axis) | Reported axis range for X. Affects input only, not forces. | Before `Acquire`. | `dev.GetObjectPropertiesByName("X").Range = new InputRange(-32768, 32767);`. The name is the `RawJoystickState` field name, and is only valid after `SetDataFormat` [REFLECT]. |
| `DIPROP_LOGICALRANGE`, `DIPROP_GRANULARITY` | Read-only. Raw HID logical range, which reveals the true axis resolution. | Any time. | `.LogicalRange`, `.Granularity` [REFLECT] |

On Simagic, the Linux driver's autocenter hook is a no-op (`sm_set_autocenter` only logs). Its gain hook sends a vendor report `0x40` with `gain >> 8` ([hid-simagic.c](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.c)) [SRC]. This means the base has a device-gain command. Whether the Windows driver maps DIPROP_FFGAIN to it is [INFER: likely, UNKNOWN: verified].

SDL sets FFGAIN=10000 and AUTOCENTER=OFF *after* acquiring, and treats failure as "unsupported" ([L366-384](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L366)) [SRC]. Per the Wine tests, the autocenter call would fail when made while acquired. Set it before.

### 1.5 Acquire: what Windows does

- On exclusive `Acquire` of an FFB device, the HID PID path sends **Device Reset**. If autocenter is off it then sends **Stop All**, followed by the **device gain** report. Expected HID writes on Windows: `{1,0x01}` (reset), `{1,0x02}` (stop all), `{8,gain}`. Win8 sends the last two in reverse order ([tests L2553-2575](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c#L2553); implementation [joystick_hid.c L1154, L1330-1350](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/joystick_hid.c#L1330)) [TEST/SRC].
- **Reset unloads every created effect** ([joystick_hid.c L1330-1334](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/joystick_hid.c#L1330)) [SRC]. So after any re-acquire you must re-download and re-start your effects (§1.10).
- **Exclusive `Unacquire` also sends Device Reset** ([joystick_hid.c L1160-1173](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/joystick_hid.c#L1160)) [SRC]. Releasing the device stops all forces. That is good for safety.
- Caveat: Simagic's new firmware does not expose a standard HID PID descriptor. Its Windows DirectInput FFB goes through Simagic's own driver stack, and the Linux driver had to reverse-engineer an "obfuscated" protocol ([simagic-ff #3](https://github.com/JacKeTUs/simagic-ff/issues/3)) [FORUM]. The reset-on-acquire behaviour above is Microsoft's generic path. **Simagic's driver should behave equivalently, but this is [UNKNOWN].** Always re-download and re-start after re-acquiring, whatever the driver.

### 1.6 SendForceFeedbackCommand

| Command | Use | Vortice |
|---|---|---|
| `DISFFC_RESET` | Stops and unloads all effects and resets the device. SDL sends it right after acquiring "just in case" ([L337-343](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L337)). icsmoi sends StopAll and Reset, then waits 500 ms before creating effects. This works around a DIY stick whose queued reset wiped the first effect ([FfbDeviceManager.cs](https://github.com/tuduce/icsmoi/blob/main/src/icsmoi.Core/Services/DirectInput/FfbDeviceManager.cs)) [SRC]. | `dev.SendForceFeedbackCommand(ForceFeedbackCommand.Reset)` |
| `DISFFC_STOPALL` | Stops all playing effects without unloading them. This is the **emergency-stop primitive**. | `ForceFeedbackCommand.StopAll` |
| `DISFFC_SETACTUATORSON` / `OFF` | Enables or disables the motors. SDL enables actuators after the reset ([L346-350](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L346)) [SRC]. | `SetActuatorsOn` / `SetActuatorsOff` |
| `DISFFC_PAUSE` / `CONTINUE` | Pause or resume all effects. | `Pause` / `Continue` |

All of them require exclusive acquisition (`DIERR_NOTEXCLUSIVEACQUIRED` otherwise) [TEST]. In Vortice `SendForceFeedbackCommand` returns `void` and **throws** `SharpGenException` on failure [REFLECT].

`GetForceFeedbackState()` returns `ForceFeedbackState` flags: `Empty`, `Stopped`, `Paused`, `ActuatorsOn/Off`, `PowerOn/Off`, `SafetySwitchOn/Off`, `UserSafetySwitchOn/Off`, `DeviceLost` [REFLECT]. Whether Simagic's driver reports its kill switch through the SafetySwitch flags is [UNKNOWN]. Poll it at about 2–10 Hz and log it.

irFFB, MAIRA 1.x and MAIRA 2.x never call `SendForceFeedbackCommand`. They rely on Acquire's implicit reset [SRC].

### 1.7 CreateEffect and the DIEFFECT fields

The common DIEFFECT used by irFFB ([L631-651](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L631)), MAIRA 2.x ([L185-198](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/DirectInput.cs#L185)), Unity-DirectInput ([L272-283](https://github.com/MrTimcakes/Unity-DirectInput/blob/main/DirectInputForceFeedback~/DirectInputForceFeedback/DirectInputForceFeedback.cpp#L272)) and Trueforce-For-All [SRC]:

| DIEFFECT field | Value | Vortice `EffectParameters` | Notes |
|---|---|---|---|
| `dwSize` | `sizeof(DIEFFECT)` | set automatically | DX6 size, which includes `dwStartDelay` [REFLECT] |
| `dwFlags` | `DIEFF_CARTESIAN \| DIEFF_OBJECTOFFSETS` | `Flags = EffectFlags.Cartesian \| EffectFlags.ObjectOffsets` | SDL: OBJECTOFFSETS "Seems obligatory" ([L619](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L619)) |
| `dwDuration` | **`INFINITE` (0xFFFFFFFF)** | `Duration = -1` | See the Simagic duration bug below. MAIRA 2.x uses `-1` [SRC]. MAIRA 1.x used `int.MaxValue` [SRC]. |
| `dwSamplePeriod` | `0` (driver default) | `SamplePeriod = 0` | |
| `dwGain` | `DI_FFNOMINALMAX` = 10000 | `Gain = 10000` | Every reference app keeps 10000 and scales magnitudes itself. |
| `dwTriggerButton` | `DIEB_NOTRIGGER` (0xFFFFFFFF) | **`TriggerButton = -1`** | The Vortice default `0` names offset 0 (an axis) as the trigger, and **CreateEffect fails with DIERR_INVALIDPARAM**. "Every effect type/axis choice failed until this was set" ([icsmoi CLAUDE.md](https://github.com/tuduce/icsmoi/blob/main/.claude/CLAUDE.md)) [FORUM, verified on hardware] |
| `dwTriggerRepeatInterval` | 0 | `TriggerRepeatInterval = 0` | |
| `cAxes`, `rgdwAxes` | 1, `{DIJOFS_X}` | `Axes = new[] { (int)JoystickOffset.X }` | Axes cannot be changed after creation. |
| `rglDirection` | `{1}` | `Directions = new[] { 1 }` | MAIRA 2.x and TF4All use `{1}` for constant force. irFFB and MAIRA 1.x used `{0}`. For per-axis conditions the DIEFFECT docs say "Cartesian: 1, 0, …" (the doc text is mirrored in Vortice's `EffectParameters` XML docs). TF4All uses `{0}` for conditions [SRC]. |
| `lpEnvelope` | `NULL` | `Envelope = null` | |
| `cbTypeSpecificParams`, `lpvTypeSpecificParams` | see below | `Parameters = new ConstantForce{…}` / `ConditionSet` / `PeriodicForce` | |
| `dwStartDelay` | 0 | `StartDelay = 0` | |

Type-specific structures. Ranges follow the Microsoft DirectInput docs. The Vortice field names are [REFLECT]:

| Struct | Fields (Vortice) | Range and meaning |
|---|---|---|
| `DICONSTANTFORCE` | `ConstantForce.Magnitude` | −10000..10000. The sign plus the direction gives the push direction. |
| `DICONDITION` (one per axis) | `Condition.Offset`, `PositiveCoefficient`, `NegativeCoefficient`, `PositiveSaturation`, `NegativeSaturation`, `DeadBand` | `Offset` −10000..10000 (center point, normalized to the axis range). Coefficients −10000..10000. Saturations 0..10000 (max output each side). DeadBand 0..10000. Some devices lack the negative coefficient or saturation, and Windows then ignores those fields ([universal-pidff `hid-pidff.h`](https://github.com/JacKeTUs/universal-pidff/blob/main/hid-pidff.h), citing MS ee416601) [SRC]. |
| `DIPERIODIC` | `PeriodicForce.Magnitude`, `Offset`, `Phase`, `Period` | Magnitude 0..10000. Offset −10000..10000. Phase in hundredths of a degree. Period in µs. |

**Direction convention:**
- DirectInput's direction is where the force comes *from*, defined by the force the user must exert to counteract it ([Effect Direction](https://msdn.microsoft.com/en-us/library/ee417536(v=vs.85))) [DOC]. Wine's developers confirmed this mapping ([wine-devel 2014](https://list.winehq.org/pipermail/wine-devel/2014-March/103281.html)) [FORUM].
- In practice MAIRA 2.x writes a counter-clockwise-positive torque directly as `Magnitude` with `Directions=[1]`. Its developer notes say "a positive module output pushes the wheel LEFT", and that the DirectInput X axis is "positive = turned right" ([MAIRA CLAUDE.md](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/CLAUDE.md)) [SRC/FORUM].
- So expect **+Magnitude with Directions {+1} to push the wheel toward −X (left)** [INFER].
- **SimPro's "Reverse FFB" (negative `ff_strength`) flips this** ([simagic-ff #3](https://github.com/JacKeTUs/simagic-ff/issues/3)) [FORUM]. Detect the sign at runtime (§8 step 6).

**Simagic duration bug (critical):**
- A game port passed SDL's "infinite" as a finite 4,294,966,296 µs. On a Simagic EVO Pro and an Alpha Mini, FFB died after 33–35 s.
- The maintainer's root cause: "Your Simagic driver keeps the duration in 16 bits of milliseconds, so it becomes 35.1 s". The arithmetic checks out: 4,294,966 ms mod 65,536 = 35,126 ms. Thrustmaster drivers cap durations at 65.5 s instead ([driveclub-recomp #102](https://github.com/OverkillLabs3/driveclub-recomp/issues/102), [#239](https://github.com/OverkillLabs3/driveclub-recomp/issues/239)) [FORUM].
- PID devices use 16-bit ms durations with 0xFFFF meaning infinite. See OpenFFBoard `#define FFB_EFFECT_DURATION_INFINITE 0xffff // Duration in ms` ([ffb_defs.h](https://github.com/Ultrawipf/OpenFFBoard/blob/master/Firmware/FFBoard/Inc/ffb_defs.h)). simagic-ff maps a length of 0 to 0xffff ([hid-simagic.c L377](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.c#L377)) [SRC].
- MAIRA 1.x's `Duration = int.MaxValue` (2,147,483 ms → would wrap to about 50.3 s [INFER]) was masked by its habit of re-sending `DIEP_START` every 30 s ([L1805-1816](https://github.com/mherbold/MarvinsAIRA/blob/main/App.ForceFeedback.cs#L1805)) [SRC].
- **Use exactly INFINITE.** As a defensive health check, read `effect.Status` (GetEffectStatus) about once per second, and restart the effect if it is not `Playing` (§1.10).

**One effect per type on Simagic:**
- The Linux driver maps each effect type to a fixed effect-block ID: Constant=0x01, Sine=0x02, Damper=0x05, Spring=0x06, Friction=0x07, Inertia=0x09. Its comment on the Set Effect report reads "Always 1??? Multiple effects of one type simultaniously??" ([hid-simagic.c L23-37, L389](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.c#L389)) [SRC].
- On Windows, "the 2nd effect doesn't get the 'Set Effect Report' packet sent to the device" when two constant forces were played in sequence ([simagic-ff #3](https://github.com/JacKeTUs/simagic-ff/issues/3)) [FORUM].
- **Create each effect type at most once, at startup. Never create or destroy effects per pulse.** Modulate magnitudes instead.

### 1.8 Download and Start

- `CreateEffect` on an exclusively acquired device downloads the effect immediately. When not exclusively acquired, Windows adds `DIEP_NODOWNLOAD` ([device.c L1595](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c#L1595)) [SRC/TEST].
- Start the effect with `effect->Start(1, 0)`: one iteration of an INFINITE-duration effect. Unity-DirectInput, SDL and TF4All use `Start(1)` [SRC]. Alternatively, `SetParameters(..., DIEP_TYPESPECIFICPARAMS | DIEP_START)` downloads and starts in one call (irFFB [L2049](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L2049)) [SRC].
- Vortice: `effect.Start()` means `Start(-1)`, i.e. INFINITE iterations; `effect.Start(1)` means one iteration [SRC: [IDirectInputEffect.cs](https://github.com/amerkoleci/Vortice.Windows/blob/main/src/Vortice.DirectInput/IDirectInputEffect.cs)]. Both work with an INFINITE duration. Prefer `Start(1)`, the well-trodden path. Simagic's playback report clamps the loop count to 0xFF ([hid-simagic.c `sm_req_playback`](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.c)) [SRC].
- `Download()`, `Start()`, `Stop()` and `Unload()` return `Result` in Vortice and do not throw [REFLECT].

### 1.9 Updating a constant force fast

**What the reference apps do:**

| App | Update call | Rate | Source |
|---|---|---|---|
| irFFB (C++) | `SetParameters(&dieff, DIEP_TYPESPECIFICPARAMS \| DIEP_NORESTART)`, called **every loop iteration** of `readWheelThread`. That loop waits on the DirectInput event notification with a 1 ms timeout, so updates run at about 1 kHz plus one per wheel input report, even when the value is unchanged. It runs with `timeBeginPeriod(1)` and `THREAD_PRIORITY_HIGHEST`. Force samples come from 360 Hz telemetry (2760 µs steps), or 720 Hz interpolated (1380 µs steps), with spin-waits. | ~1 kHz writes, 360/720 Hz content | [L154](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L154), [L249](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L249), [L700-705](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L700), [L781](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L781), [L1092-1145](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L1092) [SRC] |
| MAIRA 1.x (C#, SharpDX) | `SetParameters(params, TypeSpecificParameters \| NoRestart)`, plus `\| Start` every 30 s. Driven by a `timeSetEvent` multimedia timer. | Period `18 − Frequency` ms (1–17 ms) | [L368-375](https://github.com/mherbold/MarvinsAIRA/blob/main/App.ForceFeedback.cs#L368), [L1795-1818](https://github.com/mherbold/MarvinsAIRA/blob/main/App.ForceFeedback.cs#L1795) [SRC] |
| MAIRA 2.x (C#, SharpDX) | `SetParameters(params, TypeSpecificParameters)` (no NoRestart). **Skips unchanged magnitudes**, but rewrites at least every 250 ms "as insurance against wheelbase drivers with inactivity watchdogs". | **360 Hz** on a high-resolution waitable timer. "The old ~500 Hz rate also pushed USB updates to the wheel right at the practical limit of many wheel bases; 360 Hz eases that pressure" | [PlayoutTimer.cs L13-23](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/PlayoutTimer.cs#L13), [DirectInput.cs L88-92, L657-703](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/DirectInput.cs#L657) [SRC] |
| SDL | Computes the minimal `DIEP_*` flags by diffing old and new parameters. No `DIEP_NORESTART`. | App-driven | [L1005-1075](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L1005) [SRC] |

**What `DIEP_NORESTART` means:**
- Without it, the driver may stop and restart an effect if it cannot change a parameter while the effect plays. With it, such a change fails with `DIERR_EFFECTPLAYING` [DOC].
- For constant force, magnitude is a dynamic parameter on normal PID drivers, so either variant works.
- For **periodic** effects it matters a lot. On a Simagic Alpha EVO Pro, SDL's per-frame sine updates without NORESTART made "DirectInput stop and restart an effect it cannot change while it plays". At 240 updates/s, a 40 Hz sine "never completes a period", and the vibration scaled with frame rate. The fix was to update the vibration at a fixed 30–60 Hz; 60 was "smoothest" on a CSR Elite and an Alpha EVO Pro ([ReIGlue PR #8](https://github.com/FH1-ReImagined/ReIGlue/pull/8)) [FORUM].
- `EffectInfo.DynamicParameters` (DIEFFECTINFO.dwDynamicParams) tells you which parameters the driver can change while playing [REFLECT]. Log it for each effect type at startup.

**Recommendation for RimForce:**
- Run the FFB loop at **360 Hz by default** (configurable 250–500 Hz; don't go above about 500 Hz without a hardware test).
- Use `TypeSpecificParameters | NoRestart`. On `DIERR_EFFECTPLAYING`, fall back to `TypeSpecificParameters` alone.
- Skip unchanged values, and refresh every 250 ms.
- Simagic's USB report-rate limit is [UNKNOWN]. The Linux driver could only overflow its output queue "by spamming WheelCheck.exe parameters, i did not see it in games" ([simagic-ff README](https://github.com/JacKeTUs/simagic-ff#known-issues-with-the-driver)) [SRC/FORUM].

**`SetParameters` while not acquired returns success.** On a non-exclusive or lost device it returns `DI_DOWNLOADSKIPPED` (0x00000003, a success code) ([tests L859-929](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c#L859); [joystick_hid.c L2964-2968](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/joystick_hid.c#L2964)) [TEST]. **Don't rely on `SetParameters` alone to detect device loss.** Also watch the input read (`GetDeviceState` / `GetCurrentJoystickState` throws on `DIERR_INPUTLOST`/`DIERR_NOTACQUIRED`), `GetForceFeedbackState`, and the effect status.

### 1.10 Errors, re-acquire, teardown

**Correct HRESULTs** ([Wine dinput.h L134-183](https://github.com/wine-mirror/wine/blob/master/include/dinput.h#L145)) [SRC]:

| Name | Value | Meaning |
|---|---|---|
| `DIERR_INPUTLOST` | 0x8007001E | Device lost (another app reset it, a driver reset, or an unplug) |
| `DIERR_NOTACQUIRED` | 0x8007000C | Not acquired |
| `DIERR_ACQUIRED` | 0x800700AA | The operation is not allowed while acquired |
| `DIERR_OTHERAPPHASPRIO` | 0x80070005 | Another app holds the device exclusively. Same value as `E_ACCESSDENIED`, `DIERR_READONLY` and `DIERR_HANDLEEXISTS` |
| `DIERR_NOTEXCLUSIVEACQUIRED` | 0x80040205 | FFB call made without exclusive acquisition |
| `DIERR_NOTDOWNLOADED` | 0x80040203 | The effect is not on the device (for example after a reset) |
| `DIERR_INCOMPLETEEFFECT` | 0x80040206 | Missing required parameters |
| `DIERR_EFFECTPLAYING` | 0x80040208 | `DIEP_NORESTART` change impossible while playing |
| `DIERR_UNPLUGGED` | 0x80040209 | Device unplugged |
| `DI_DOWNLOADSKIPPED` | 0x00000003 | Success, but nothing reached the device |
| `DI_EFFECTRESTARTED` | 0x00000004 | Success, and the effect was restarted |

> **Vortice bug [REFLECT]:** in Vortice.DirectInput 3.8.3 the values are wrong:
> - `ResultCode.InputLost` = 0x0000001E
> - `ResultCode.NotAcquired` = 0x0000000C
> - `ResultCode.Acquired` = 0x000000AA
> - `ResultCode.NotInitialized` = 0x00000015
>
> They are raw Win32 codes without `HRESULT_FROM_WIN32` ([ResultCode.cs](https://github.com/amerkoleci/Vortice.Windows/blob/main/src/Vortice.DirectInput/ResultCode.cs)), so `ex.ResultCode == ResultCode.InputLost` will **never** match. The macro-generated ones (`NotExclusiveAcquired`, `Unplugged`, `NotDownloaded`, `OtherApplicationHasPriority`, `InvalidParam`, `EffectPlaying`) are correct. SharpDX 4.2.0's values are all correct (InputLost = 0x8007001E, and so on) [REFLECT].

**Recovery patterns in the wild:**
- **irFFB:** on any non-OK `GetDeviceState` or `SetParameters`:
  1. `Unacquire()`
  2. `Acquire()`
  3. Recreate the effect if it is null.
  4. `SetParameters(DIEP_TYPESPECIFICPARAMS | DIEP_START)`

  ([`reacquireDIDevice` L2082-2110](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L2082)) [SRC].
- **SDL:** on `DIERR_NOTEXCLUSIVEACQUIRED`, it unacquires and sets the cooperative level again. On `DIERR_INPUTLOST` or `DIERR_NOTACQUIRED`, it calls `Acquire()` and retries `SetParameters` ([L1075-1091](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c#L1075)) [SRC].
- **MAIRA 2.x:**
  - Treats InputLost, NotAcquired, NotExclusiveAcquired, OtherAppHasPrio, NotDownloaded and Unplugged as "device lost".
  - On loss it shuts FFB down and re-initializes from scratch, with **exponential backoff from 1 s to a 15 s maximum**.
  - The failure counter resets after 10 s of healthy running.
  - Its init failure path releases everything, because "a joystick left acquired here would hold the wheel exclusively with no effect on it" ([DirectInput.cs L60-80, L226-245, L705-751](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/DirectInput.cs#L705)) [SRC].
  - It also listens for HID hot-plug to re-open the device after a replug (`HidHotPlugMonitor.cs`) [SRC].
- **Recommendation:** follow MAIRA 2.x: full re-init with backoff. On every recovery, **re-start forces from zero with a soft ramp**, never at the last magnitude.

**Teardown:**
1. Zero the magnitude.
2. `effect.Stop()`
3. `SendForceFeedbackCommand(StopAll)`
4. Dispose the effects.
5. `Unacquire()` (exclusive unacquire resets the device [SRC]).
6. Dispose the device and DirectInput.

irFFB's `releaseDirectInput` follows the same order: setFFB(0), Stop, Release, Unacquire, Release ([L2060-2080](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp#L2060)) [SRC].

### 1.11 irFFB: exactly what it does (summary)

Source: [nlp80/irFFB `irFFB/irFFB.cpp`](https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp) and [`settings.cpp`](https://github.com/nlp80/irFFB/blob/master/irFFB/settings.cpp) [SRC].

1. `DirectInput8Create`, then `EnumDevices(DI8DEVCLASS_GAMECTRL, …, DIEDFL_ATTACHEDONLY | DIEDFL_FORCEFEEDBACK)`. vJoy devices are skipped.
2. `CreateDevice(selected guid)`
3. `SetDataFormat(&c_dfDIJoystick)`
4. `SetCooperativeLevel(mainWnd, DISCL_EXCLUSIVE | DISCL_BACKGROUND)`
5. `EnumObjects` to count buttons and POVs.
6. `SetEventNotification(wheelEvent)`
7. Read `DIPROP_VIDPID`. Logitech wheels get their range set to 900° through a window message to Logitech Gaming Software.
8. `Acquire()`. **irFFB never sets DIPROP_AUTOCENTER or DIPROP_FFGAIN, and never calls SendForceFeedbackCommand.**
9. **`CreateEffect(GUID_Sine, …)` with `DIPERIODIC{dwMagnitude=0, dwPeriod=INFINITE, dwPhase=0, lOffset=force}`.** irFFB uses a sine effect's *offset* as its constant force: a zero-amplitude, infinite-period sine.
   The DIEFFECT is: `DIEFF_CARTESIAN|DIEFF_OBJECTOFFSETS`, `dwDuration=INFINITE`, `dwSamplePeriod=0`, `dwGain=DI_FFNOMINALMAX`, `DIEB_NOTRIGGER`, `cAxes=1`, `rgdwAxes={DIJOFS_X}`, `rglDirection={0}`, no envelope.
   Most modern C# tools (MAIRA, SDL, Unity, TF4All) use `GUID_ConstantForce` instead.
10. `SetParameters(DIEP_TYPESPECIFICPARAMS | DIEP_START)` to download and start.
11. A wheel thread at `THREAD_PRIORITY_HIGHEST` loops on `WaitForSingleObject(wheelEvent, 1)`:
    - on an event, `GetDeviceState`, forward the axes to vJoy, and compute a damping term from FIR-filtered wheel velocity;
    - then always set `pforce.lOffset = clamp(ffbMag + damping, ±10000)` and call `SetParameters(DIEP_TYPESPECIFICPARAMS | DIEP_NORESTART)`;
    - on any failure, `reacquireDIDevice()`.
12. Torque scaling: `scaleFactor = 10000 / maxForceNm`, and `DI = torqueNm × scaleFactor`, clamped to ±9996. iRacing is told the same max force through `irsdk_broadcastMsg(irsdk_BroadcastFFBCommand, irsdk_FFBCommand_MaxForce, maxForce)` ([settings.cpp L171-186](https://github.com/nlp80/irFFB/blob/master/irFFB/settings.cpp#L171)).
    Extras:
    - min force: small magnitudes are raised to a minimum
    - "reduce when parked": the magnitude is divided by 4 when stopped
    - soft bump stops near the steering lock
    - a clipping counter

### 1.12 Other open-source references (what each teaches)

| Project | Lang / wrapper | Takeaways |
|---|---|---|
| **MAIRA 2.x** ([MarvinsAIRARefactored](https://github.com/mherbold/MarvinsAIRARefactored)) | C# / .NET 9 / SharpDX.DirectInput 4.2.0 | Modern reference. Exclusive\|Background, a single ConstantForce, `Duration=-1`, `Directions=[1]`, 360 Hz high-resolution waitable timer, skip-unchanged plus a 250 ms refresh, device-loss classification, backoff re-init, separate non-exclusive device objects for button input, fade-in 2000 ms / fade-out 750 ms ([RacingWheel.cs L71-79](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/RacingWheel.cs#L71)). Sign conventions are in its CLAUDE.md [SRC] |
| **MAIRA 1.x** ([MarvinsAIRA](https://github.com/mherbold/MarvinsAIRA)) | C# / net8.0-windows / SharpDX 4.2.0 | Proves SharpDX.DirectInput works on .NET 8. Uses a multimedia timer, `DIEP_START` every 30 s, and a 1 s "cool down to prevent sore thumbs" ramp [SRC] |
| **SDL** ([SDL_dinputhaptic.c](https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c)) | C | Canonical open sequence (§1.3–1.6), message-only window, minimal-flag updates, recovery logic. SDL's finite "infinite" duration is the source of the Simagic 35 s bug [SRC/FORUM] |
| **Unity-DirectInput** ([MrTimcakes](https://github.com/MrTimcakes/Unity-DirectInput); maintained fork [imDanoush](https://github.com/imDanoush/Unity-DirectInput); archived [ATG-Simulator](https://github.com/ATG-Simulator/Unity-DirectInput)) | C++ native plugin and C# | c_dfDIJoystick2, Exclusive\|Background, effects per type with `Start(1)`, conditions updated with `DIEP_TYPESPECIFICPARAMS`. The imDanoush README lists **"Simagic Alpha-Mini ✅ Verified"**, with the caveat "not all devices support all the FFB effects!" [SRC/FORUM] |
| **Trueforce-For-All** ([DirectInputWheel.cs](https://github.com/Mhytee/Trueforce-For-All/blob/main/src/TrueforceForAll.Plugin/DirectInputWheel.cs)) | C# / SharpDX (SimHub plugin) | Native Damper/Spring/Friction/Inertia and periodic effects with `TriggerButton=-1`, `Duration=-1`, X axis. "Conditions ignore direction; force/periodic effects need a non-zero direction". About 1 kHz position polling. Explains the exclusive-vs-reader rule [SRC] |
| **icsmoi** ([FfbDeviceManager.cs](https://github.com/tuduce/icsmoi/blob/main/src/icsmoi.Core/Services/DirectInput/FfbDeviceManager.cs), [CLAUDE.md](https://github.com/tuduce/icsmoi/blob/main/.claude/CLAUDE.md)) | C# / **Vortice.DirectInput** | The only real-world Vortice FFB code found. It documents the Vortice traps: `TriggerButton=-1`, `SetDataFormat` before `Acquire`, `.CheckError()` on the `Result` returns, and the absence of an `Effect` class (use `CreateEffect`) [SRC/FORUM] |
| **BizHawk fork** ([DGamepad.cs](https://github.com/jlefebure/bizhawk-twitchplays/blob/master/src/BizHawk.Bizware.Input/DirectX/DGamepad.cs)) | C# / Vortice | Vortice input and range setup (`GetObjectPropertiesByName(...).Range = new(-1000, 1000)`), and `Poll().Failure` handling. Mentions Vortice issue #393 (`GetObjectPropertiesById` was broken before 3.x) [SRC] |
| **simagic-ff** ([JacKeTUs/simagic-ff](https://github.com/JacKeTUs/simagic-ff)) | Linux kernel C | **The best Simagic protocol reference.** Effect block IDs, which effects actually do something, center offset ignored, 16-bit duration, device gain report, and the settings feature reports (0x80/0x81) holding SimPro's settings (§3.3) [SRC] |
| **universal-pidff** ([JacKeTUs/universal-pidff](https://github.com/JacKeTUs/universal-pidff)) | Linux kernel C | Direct-drive quirks list: missing delay (old Simagic), periodic sine-only (PXN), missing negative coefficient or deadband (Asetek). Shows how many DD bases deviate from the PID spec [SRC] |
| **OpenFFBoard** ([Ultrawipf/OpenFFBoard](https://github.com/Ultrawipf/OpenFFBoard)) | Firmware C++ | Open HID-PID firmware. Durations in ms, with 0xFFFF meaning infinite ([ffb_defs.h](https://github.com/Ultrawipf/OpenFFBoard/blob/master/Firmware/FFBoard/Inc/ffb_defs.h)). FFBeast and others follow the same PID model [SRC] |
| **Wine dinput** ([joystick_hid.c](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/joystick_hid.c), [tests](https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c)) | C | Executable specification of Windows' HID PID DirectInput behaviour (§1.3–1.10). On Linux, Simagic's new firmware needs the simagic-ff kernel driver, because it has no standard PID descriptor [SRC] |
| **vJoy FFB** | — | Only relevant for intercepting a game's FFB (irFFB requires a vJoy device with X and all FFB effects enabled, [irFFB wiki](https://github.com/nlp80/irFFB/wiki/Configuration)). **Not needed for RimForce.** |

---

## 2. C# wrappers: Vortice.DirectInput vs SharpDX.DirectInput

### 2.1 Comparison

| | **Vortice.DirectInput** | **SharpDX.DirectInput** |
|---|---|---|
| Version | **3.8.3**, 2026-03-04 ([NuGet](https://www.nuget.org/packages/Vortice.DirectInput)) | 4.2.0, 2018-08-24 ([NuGet](https://www.nuget.org/packages/SharpDX.DirectInput)) |
| Maintenance | Active. The repo had commits through Oct 2026 ([commits](https://github.com/amerkoleci/Vortice.Windows/commits/main)), though DirectInput itself rarely changes. Depends on `SharpGen.Runtime` **2.4.2-beta** and `Vortice.DirectX` | **Archived** 2019-03-29 ("no longer being under development or maintenance") ([sharpdx/SharpDX](https://github.com/sharpdx/SharpDX)) |
| TFMs | net8.0, net9.0, net10.0 ([csproj](https://github.com/amerkoleci/Vortice.Windows/blob/main/src/Vortice.DirectInput/Vortice.DirectInput.csproj)) | net40, net45, netstandard1.3 |
| Works on .NET 8/9/10? | Yes (native TFM) [REFLECT: compiled here against net10.0-windows] | **Yes in practice:** MAIRA 1.x ships net8.0-windows, and MAIRA 2.x ships .NET 9 [SRC]. I restored, built and loaded it on net10.0 here, with no vulnerable transitive packages reported [REFLECT] (hardware not exercised) |
| Error model | **Mixed.** Returns `Result` for `Acquire`, `Unacquire`, `Poll`, `SetCooperativeLevel`, `SetDataFormat<T>`, `Start`, `Stop`, `Download`, `Unload`, and for the `CreateEffect(guid, p, out fx)` overload. **Throws** `SharpGenException` (`.ResultCode.Code`) for `SetParameters`, `SendForceFeedbackCommand`, `GetForceFeedbackState`, property setters, `GetCurrentJoystickState`, and `CreateEffect(guid, p)` [REFLECT: decompiled] | Everything throws `SharpDXException` (`.ResultCode`) [REFLECT] |
| Effect object | No `Effect` class: `IDirectInputEffect fx = dev.CreateEffect(EffectGuid.X, params)` | `new Effect(joystick, EffectGuid.X, params)` |
| Device object | `IDirectInputDevice8 dev = di.CreateDevice(guid)`, then `dev.SetDataFormat<RawJoystickState>()` yourself | `new Joystick(di, guid)`, which sets the joystick data format |
| HRESULT constants | **4 wrong values** (§1.10) | Correct |
| Example code | Rare: icsmoi, the BizHawk fork | Plenty: MAIRA 1/2, TF4All, NOFFB, x360ce, FreePIE (`gh search code "EffectGuid.ConstantForce"`) |

**Recommendation: Vortice.DirectInput 3.8.3.** It is maintained, has a first-class net10.0 build, and its API is a near-copy of SharpDX's. Wrap it in a thin `FfbDevice` class that:
- (a) defines correct HRESULT constants;
- (b) always sets `TriggerButton = -1` and `Duration = -1`;
- (c) calls `.CheckError()` on every `Result`;
- (d) catches `SharpGenException` in the hot path.

Keep the wrapper small enough that swapping to SharpDX would be about a day's work if a Vortice marshaling bug ever shows up.

### 2.2 Exact Vortice API names (verified by reflection on 3.8.3)

- **`DInput`**
  - `static IDirectInput8 DirectInput8Create()`
  - `static Result DirectInput8Create(out IDirectInput8 di)`
- **`IDirectInput8`**
  - `IList<DeviceInstance> GetDevices(DeviceClass, DeviceEnumerationFlags)`
  - `IDirectInputDevice8 CreateDevice(Guid)`
  - `Result CreateDevice(Guid, out IDirectInputDevice8)`
  - `bool IsDeviceAttached(Guid)`
- **`DeviceInstance`** fields: `InstanceGuid`, `ProductGuid`, `InstanceName`, `ProductName`, `ForceFeedbackDriverGuid`, `UsagePage`, `Usage`; properties `Type`, `Subtype`.
- **`IDirectInputDevice8`**
  - Setup: `Result SetDataFormat<T>()`, `Result SetCooperativeLevel(nint hwnd, CooperativeLevel)`, `Result Acquire()`, `Result Unacquire()`, `Result Poll()`
  - Input: `JoystickState GetCurrentJoystickState()`, `void GetCurrentJoystickState(ref JoystickState)`
  - Properties: `ObjectProperties Properties { get; }`, `ObjectProperties GetObjectPropertiesByName(string)`, `ObjectProperties GetObjectPropertiesByUsage(int)`, `ObjectProperties GetObjectPropertiesById(DeviceObjectId)`
  - Info: `Capabilities Capabilities`, `DeviceInstance DeviceInfo`, `IList<DeviceObjectInstance> GetObjects(DeviceObjectTypeFlags)`
  - Effects: `IList<EffectInfo> GetEffects()`, `IList<EffectInfo> GetEffects(EffectType)`, `IDirectInputEffect CreateEffect(Guid, EffectParameters)`, `Result CreateEffect(Guid, EffectParameters, out IDirectInputEffect)`, `IList<IDirectInputEffect> CreatedEffects`
  - FFB control: `void SendForceFeedbackCommand(ForceFeedbackCommand)`, `ForceFeedbackState GetForceFeedbackState()`, `Result SetEventNotification(WaitHandle?)`, `int Escape(int, byte[], byte[])`
- **`ObjectProperties`**: `bool AutoCenter`, `int ForceFeedbackGain`, `InputRange Range`, `InputRange LogicalRange`, `InputRange PhysicalRange`, `int Granularity`, `int DeadZone`, `int Saturation`, `int VendorId`, `int ProductId`, `string ProductName`, `string InterfacePath`, `int MemoryLoad` (DIPROP_FFLOAD).
- **`IDirectInputEffect`**
  - `void SetParameters(EffectParameters, EffectParameterFlags)`, `void SetParameters(EffectParameters)` (= All)
  - `EffectParameters GetParameters(EffectParameterFlags)`
  - `Result Start()` (= INFINITE iterations), `Result Start(int iterations)`, `Result Start(int, EffectPlayFlags)`
  - `Result Stop()`, `Result Download()`, `Result Unload()`
  - `EffectStatus Status` (`None`/`Playing`/`Emulated`), `Guid Guid`
- **`EffectParameters`**
  - Fields: `EffectFlags Flags`, `int Duration`, `int SamplePeriod`, `int Gain`, `int TriggerButton`, `int TriggerRepeatInterval`, `int StartDelay`
  - Properties: `int[] Axes`, `int[] Directions`, `Envelope Envelope`, `TypeSpecificParameters Parameters`
- **Type-specific classes**
  - `ConstantForce { int Magnitude }`
  - `ConditionSet { Condition[] Conditions }`, where the struct `Condition { int Offset, PositiveCoefficient, NegativeCoefficient, PositiveSaturation, NegativeSaturation, DeadBand }`
  - `PeriodicForce { int Magnitude, Offset, Phase, Period }`
  - `RampForce { Start, End }`
  - `Envelope { AttackLevel, AttackTime, FadeLevel, FadeTime }`
- **`EffectGuid`** (static readonly Guids): `ConstantForce`, `RampForce`, `Square`, `Sine`, `Triangle`, `SawtoothUp`, `SawtoothDown`, `Spring`, `Damper`, `Inertia`, `Friction`, `CustomForce`.
- **Enums**
  - `CooperativeLevel { Exclusive=1, NonExclusive=2, Foreground=4, Background=8, NoWinKey=0x10 }`
  - `EffectFlags { ObjectIds=1, ObjectOffsets=2, Cartesian=0x10, Polar=0x20, Spherical=0x40 }`
  - `EffectParameterFlags { Duration, SamplePeriod, Gain, TriggerButton, TriggerRepeatInterval, Axes, Direction, Envelope, TypeSpecificParameters=0x100, AllExceptDelay, StartDelay, All, Start=0x20000000, NoRestart=0x40000000, NoDownload=0x80000000 }`
  - `ForceFeedbackCommand { Reset=1, StopAll=2, Pause=4, Continue=8, SetActuatorsOn=0x10, SetActuatorsOff=0x20 }`
  - `DeviceClass.GameControl = 4`
  - `DeviceEnumerationFlags { AttachedOnly=1, ForceFeedback=0x100 }`
  - `JoystickOffset.X = 0`
- **`EffectInfo`**: `Guid Guid`, `EffectType Type`, `EffectParameterFlags StaticParameters`, `EffectParameterFlags DynamicParameters`, `string Name`.
- **`SharpGen.Runtime`**: `Result { int Code; bool Success; bool Failure; void CheckError(); }`, `SharpGenException.ResultCode`.

**Performance note [SRC]:** Vortice's `EffectParameters.__MarshalTo` makes three small `Marshal.AllocHGlobal` calls per `SetParameters` (axes, directions, type-specific block), and frees them right after ([EffectParameters.cs](https://github.com/amerkoleci/Vortice.Windows/blob/main/src/Vortice.DirectInput/EffectParameters.cs)). That is about 1–1.5k native allocations per second at 360–500 Hz. It is native heap churn, not GC pressure, and is fine. SharpDX does the same.

### 2.3 Minimal working example: constant force and spring (Vortice, compile-checked)

I compiled this code against `Vortice.DirectInput` 3.8.3, targeting `net10.0-windows`, in a scratch project [REFLECT]. It has **not** been run on hardware.

```csharp
using SharpGen.Runtime;
using Vortice.DirectInput;

public static class Di
{
    // Correct dinput.h HRESULTs. Vortice 3.8.3's ResultCode.InputLost/NotAcquired/Acquired/NotInitialized are wrong.
    public const int InputLost            = unchecked((int)0x8007001E);
    public const int NotAcquired          = unchecked((int)0x8007000C);
    public const int Acquired             = unchecked((int)0x800700AA);
    public const int OtherAppHasPrio      = unchecked((int)0x80070005);
    public const int NotExclusiveAcquired = unchecked((int)0x80040205);
    public const int NotDownloaded        = unchecked((int)0x80040203);
    public const int EffectPlaying        = unchecked((int)0x80040208);
    public const int Unplugged            = unchecked((int)0x80040209);
    public const int Infinite = -1, NoTrigger = -1, NominalMax = 10000;
    public static bool IsDeviceLoss(int hr) =>
        hr is InputLost or NotAcquired or OtherAppHasPrio or NotExclusiveAcquired or NotDownloaded or Unplugged;
}

// ---- open (on the device thread) ----
IDirectInput8 di = DInput.DirectInput8Create();
DeviceInstance wheel = di.GetDevices(DeviceClass.GameControl,
        DeviceEnumerationFlags.AttachedOnly | DeviceEnumerationFlags.ForceFeedback)
    .First(d => d.ProductName.Contains("SIMAGIC", StringComparison.OrdinalIgnoreCase));

IDirectInputDevice8 dev = di.CreateDevice(wheel.InstanceGuid);
dev.SetDataFormat<RawJoystickState>().CheckError();                      // before Acquire!
dev.SetCooperativeLevel(hwnd, CooperativeLevel.Exclusive | CooperativeLevel.Background).CheckError();
try { dev.Properties.AutoCenter = false; } catch (SharpGenException) { }   // while unacquired
try { dev.GetObjectPropertiesByName("X").Range = new InputRange(-32768, 32767); } catch (SharpGenException) { }
try { dev.Properties.ForceFeedbackGain = 10000; } catch (SharpGenException) { }
dev.Acquire().CheckError();
dev.SendForceFeedbackCommand(ForceFeedbackCommand.Reset);                // throws on failure
dev.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);

// ---- constant force (main channel) ----
var cf = new ConstantForce { Magnitude = 0 };
var cfParams = new EffectParameters
{
    Flags = EffectFlags.Cartesian | EffectFlags.ObjectOffsets,
    Duration = Di.Infinite,                 // INFINITE, never int.MaxValue (Simagic 16-bit ms wrap)
    SamplePeriod = 0,
    Gain = Di.NominalMax,
    TriggerButton = Di.NoTrigger,           // default 0 => DIERR_INVALIDPARAM
    TriggerRepeatInterval = 0,
    StartDelay = 0,
    Axes = new[] { (int)JoystickOffset.X },
    Directions = new[] { 1 },
    Parameters = cf,
};
IDirectInputEffect cfx = dev.CreateEffect(EffectGuid.ConstantForce, cfParams);
cfx.Start(1).CheckError();

// hot path, 360-500 Hz:
var flags = EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.NoRestart;
void SetConstant(int m)
{
    cf.Magnitude = Math.Clamp(m, -10000, 10000);
    try { cfx.SetParameters(cfParams, flags); }
    catch (SharpGenException ex) when (ex.ResultCode.Code == Di.EffectPlaying)
    { flags = EffectParameterFlags.TypeSpecificParameters; cfx.SetParameters(cfParams, flags); }
    catch (SharpGenException ex) when (Di.IsDeviceLoss(ex.ResultCode.Code))
    { /* signal recovery (backoff re-init), see §1.10 */ }
}

// ---- native spring (centered only on Simagic: Offset is ignored by the firmware) ----
var spring = new ConditionSet { Conditions = new[] { new Condition {
    Offset = 0, PositiveCoefficient = 3000, NegativeCoefficient = 3000,
    PositiveSaturation = 10000, NegativeSaturation = 10000, DeadBand = 0 } } };
var springParams = new EffectParameters
{
    Flags = EffectFlags.Cartesian | EffectFlags.ObjectOffsets, Duration = Di.Infinite, Gain = 10000,
    TriggerButton = Di.NoTrigger, Axes = new[] { (int)JoystickOffset.X }, Directions = new[] { 1 },
    Parameters = spring,
};
IDirectInputEffect sfx = dev.CreateEffect(EffectGuid.Spring, springParams);
sfx.Start(1).CheckError();
// change stiffness later:
spring.Conditions[0].PositiveCoefficient = spring.Conditions[0].NegativeCoefficient = 5000;
sfx.SetParameters(springParams, EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.NoRestart);

// ---- read wheel ----
var js = new JoystickState();
dev.Poll();                              // Result; DI_NOEFFECT on interrupt-driven HID is fine
dev.GetCurrentJoystickState(ref js);     // throws SharpGenException on DIERR_INPUTLOST/NOTACQUIRED
int x = js.X;                            // -32768..32767 after DIPROP_RANGE

// ---- shutdown / e-stop ----
cf.Magnitude = 0; try { cfx.SetParameters(cfParams, EffectParameterFlags.TypeSpecificParameters); } catch { }
cfx.Stop(); sfx.Stop();
try { dev.SendForceFeedbackCommand(ForceFeedbackCommand.StopAll); } catch { }
cfx.Dispose(); sfx.Dispose(); dev.Unacquire(); dev.Dispose(); di.Dispose();
```

**SharpDX equivalent names** for reference ([REFLECT] and MAIRA/TF4All [SRC]):
- Setup: `new DirectInput()`, `di.GetDevices(...)`, `new Joystick(di, guid)`, `joystick.SetCooperativeLevel(hwnd, CooperativeLevel.Exclusive | CooperativeLevel.Background)`, `joystick.Properties.AutoCenter = false`, `joystick.Acquire()`
- Effects: `new Effect(joystick, EffectGuid.ConstantForce, p)`, `effect.SetParameters(p, EffectParameterFlags.TypeSpecificParameters)`, `effect.Start()`, `effect.Download()`, `joystick.SendForceFeedbackCommand(...)`
- Errors: `catch (SharpDXException e) when (e.ResultCode == ResultCode.InputLost)` (correct codes)

---

## 3. How strength is layered: game, DirectInput, driver, SimPro, motor

### 3.1 The chain

```
drill torque target (Nm, RimForce)
  └─► DI magnitude  m ∈ [−10000, 10000]              (DICONSTANTFORCE.lMagnitude; app clamps)
      × effect gain  g_e = dwGain/10000 ≤ 1           (DIEFFECT.dwGain)
      × device gain  g_d = DIPROP_FFGAIN/10000 ≤ 1    (>10000 → DIERR_INVALIDPARAM)
      ─► Simagic driver → USB → base firmware
      × SimPro overall strength s ∈ [−100 %, 100 %]   (ff_strength; negative = reversed)
      (+ SimPro filter / slew-rate / "feedback detail" processing, mechanical damper/friction/inertia added)
      × base peak torque  T_peak (9 / 12 / 18 Nm)
  ≈ motor torque
```

All DirectInput scalars top out at 10000 = 100 %. SimPro's strength and the motor peak sit after DirectInput, so **an app cannot command more than `s × T_peak`** through DirectInput [DOC/TEST/INFER]. The only path around SimPro's cap is to rewrite the base's `ff_strength` field with Simagic's own HID feature report, as the Linux driver's sysfs `ff_strength` does: report 0x80 "settings1", range −100..100 ([hid-simagic-settings.h](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic-settings.h), [hid-simagic-sysfs.c](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic-sysfs.c)) [SRC]. That bypasses the user's own safety setting, relies on an undocumented protocol that "simagic would just change … at any time" ([simagic-ff #3](https://github.com/JacKeTUs/simagic-ff/issues/3)), and can conflict with SimPro. **Do not do it.** At most, *read* settings (§3.3) for display. Even that is optional and experimental.

How exactly `s` and `T_peak` combine (linear? any calibration curve?) is [UNKNOWN]. Treat the computed Nm as **approximate** and show it that way in the UI.

### 3.2 How games set strength (iRacing as the model)

- **iRacing "Max Force" (Nm):** the steering-column torque that maps to 100 % DirectInput output. Anything above clips. irFFB implements the same thing: `DI = torqueNm × 10000 / maxForceNm`, and it pushes the value into iRacing with `irsdk_FFBCommand_MaxForce` ([settings.cpp L171-186](https://github.com/nlp80/irFFB/blob/master/irFFB/settings.cpp#L171)) [SRC]. The irFFB wiki: "Set the force (in Nm) that corresponds to maximum output, lower values produce stronger output … The same as the equivalent option in iRacing (with linear mode …)" ([wiki](https://github.com/nlp80/irFFB/wiki/Configuration)) [DOC]. A Simucube forum example: max force 70 Nm means iRacing simulates column forces up to 70 Nm and clips above ([Granite Devices community](https://community.granitedevices.com/t/iracing-and-simucube-2/3049/147)) [FORUM].
- **iRacing "Wheel Force" (Nm):** tells iRacing the base's peak torque, so it can rescale its strength slider. Set it to the base peak, e.g. 25 Nm for a Simucube 2 Pro, and use linear mode on direct-drive bases ([driver61](https://driver61.com/sim-racing/iracing-force-feedback-setup/), [simracingcockpit](https://simracingcockpit.gg/iracing-ffb-settings/)) [FORUM].
- **RimForce analogue:** `DI = sign × T_target / (s × T_peak) × 10000`. The user enters the base model, which gives `T_peak`, and SimPro's overall strength, which gives `s`. This is the same "Nm ↔ 100 %" idea, with the denominator taken from the real base rather than a game constant.
- **`DIPROP_FFGAIN` and `dwGain`:** every reference app keeps both at 10000 and scales in software (irFFB, MAIRA, SDL default, Unity, TF4All) [SRC]. Simagic's Linux protocol hard-codes the Set Effect gain byte to 0xFF ([hid-simagic.c L392](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.c#L392)). Whether the Windows driver honours a per-effect `dwGain` < 10000 is [UNKNOWN]. **Recommendation:** keep `dwGain = 10000` and `FFGAIN = 10000`, and scale magnitudes in software.

### 3.3 What SimPro Manager settings do

These are SimPro's device-level settings, exposed by the Linux driver from the base's settings and status feature reports ([hid-simagic-settings.h](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic-settings.h)) [SRC]. Definitions come from Simagic's own (older, Alpha-era) Alpha Manager docs ([simagic.github.io source: alpha_manager.md](https://github.com/simagic/simagic.github.io/blob/main/_pages/base/alpha/alpha_manager.md)) [DOC] and from community guides [FORUM].

| Base field (Linux) | SimPro / Alpha Manager name | Range | Effect on RimForce |
|---|---|---|---|
| `ff_strength` | Overall / Total FFB strength ("TotalForce: Total power of the force feedback. Negative values invert the FFB") [DOC]. Guides list "max torque output: 70 to 80 %" [FORUM] | −100..100 | **The hard cap and the sign.** The user must enter it. A negative value or "Reverse FFB" flips the force direction [FORUM]. |
| `max_angle` | Angle / rotation range | 90..2520° ([sysfs clamp](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic-settings.c)) [SRC]; guides agree on 90–2520 [FORUM] | Determines how the X axis maps to degrees (§4.2). **Don't change it from RimForce.** Changing the range while the wheel is outside it kills FFB until a power cycle, and SimPro blocks the change unless the wheel is centered ([README](https://github.com/JacKeTUs/simagic-ff#known-issues-with-the-firmware)) [SRC]. |
| `angle_lock`, `angle_lock_strength` | Hard lock angle; "Limit strength" Soft/Normal/Firm [INFER from field names and guides] | 90..max_angle; 0..2 | The firmware's end stops. A safety backstop. |
| `wheel_rotation_speed` | "WheelSpeed: The maximal speed of the wheelbase in percent. If the speed requested from the game is above the percentage value, it will be cut" [DOC] | 0..100 | **A hardware speed limiter.** Recommend a moderate value for training. Guides use 25–50 for games [FORUM]. |
| `mechanical_centering`, `mechanical_damper`, `mechanical_friction`, `mechanical_inertia`, `center_damper` | WheelSpring, WheelDamper, WheelFriction, WheelInertia ("0 equals original weight") [DOC] | 0..100 | Always-on firmware effects that **add to** RimForce's forces. A drill expects them at 0, or at known small values. |
| `game_centering`, `game_damper`, `game_friction`, `game_inertia` | GameSpring, GameDamper, GameFriction, GameInertia (0..200 in Alpha Manager, undocumented) [DOC] | 0..200 | [INFER] These scale **DirectInput condition effects** (Spring, Damper, Friction, Inertia). If they are 0, RimForce's native springs and dampers do nothing. Guides: "individual spring, damper, and periodic effect sliders only matter in a few legacy titles" [FORUM]. This is another reason to keep drills on constant force. |
| `filter_level` | SmoothLevel / Filter ("how much of the FFB the game sends is cut off / trimmed") [DOC] | 0..10 | Smears pulses and vibration. Recommend a low value for RimForce. |
| `slew_rate_control` | Slew rate control ("how fast the force feedback corrects", 0 % = no limit) [FORUM] | % | Limits torque rise rate. Affects pulse sharpness. Also a safety feature. |
| `feedback_detail` | Feedback detail / "Suspension" [DOC/FORUM] | 0..9+ | Unknown processing [UNKNOWN]. |
| `firmware_version`, `wheel_channel`, `ring_light` | — | — | Informational. |

- **Game profiles:** SimPro supports saving and loading profiles (Alpha Manager: ReadBase, WriteStorage, SaveAs, Dropdown [DOC]). Whether SimPro 3 switches profiles automatically per game executable, and whether it would detect RimForce, is [UNKNOWN]. Ask the user to create a dedicated "RimForce" profile and document the expected values.
- **SimPro versions:** SimPro **3.x** drives the EVO bases, and 2.x drives the older Alpha, Mini and U bases ([simracingcockpit troubleshooting](https://simracingcockpit.gg/simagic-troubleshooting/)) [FORUM].

### 3.4 Simagic SDK, API or telemetry

- **No public Simagic wheelbase SDK or API was found.** For comparison, MOZA publishes one ([mozaracing.com/pages/sdk](https://mozaracing.com/pages/sdk)) [FORUM, search result].
- `SimagicHPR.dll`, used by MAIRA, is **mherbold's own raw-HID library for Simagic pedal haptics**, not an official SDK ([mherbold/SimagicHPR](https://github.com/mherbold/SimagicHPR)) [SRC].
- Simagic's own "telemetry functions (shift lights, FX-Pro display) … work only with proprietary soft[ware], which should get access to shared memory chunks from games" ([simagic-ff README](https://github.com/JacKeTUs/simagic-ff)) [SRC]. There is no app-facing API.
- The base's settings live behind vendor HID feature reports 0x80/0x81, reverse-engineered on Linux [SRC]. Reading report 0x81 on Windows (`HidD_GetFeature`) would give RimForce `max_angle`, `ff_strength` and `firmware_version`. That would be **experimental and firmware-dependent**. Optional, read-only, and off by default.

### 3.5 Which DirectInput effects Simagic actually supports

| Effect | Evidence | Verdict for RimForce |
|---|---|---|
| **ConstantForce** | Block ID 0x01 in simagic-ff [SRC]. Used by MAIRA and irFFB-style tools on every DD base. Simagic Alpha-Mini "verified" in Unity-DirectInput [FORUM]. | **Primary channel.** |
| **Spring** | Block 0x06 [SRC]. **The center offset is ignored by the firmware, "which is also a case for Windows … could be checked through WheelCheck.exe"** ([README](https://github.com/JacKeTUs/simagic-ff#known-issues-with-the-firmware)) [SRC/FORUM]. Probably scaled by Game Spring [INFER]. | Native only for *centered* springs. Springs toward an angle are done in software. |
| **Damper** | Block 0x05 [SRC]. Probably scaled by Game Damper [INFER]. | Optional "feel" layer. Firmware-rate, so stable. |
| **Friction** | Block 0x07 [SRC]. Probably scaled by Game Friction [INFER]. | Optional. Better native than software, where software friction chatters. |
| **Inertia** | Block 0x09 [SRC]. | Not needed. |
| **Sine** | Block 0x02, but the Linux driver says: "it doesn't look like firmware actually supports periodic effects; official driver uploads the basic effect, but not periodicity settings" [SRC L510-511]. A Windows test first saw "a sine wave effect appeared to do nothing", then got it working with a small DI app ([#3](https://github.com/JacKeTUs/simagic-ff/issues/3)) [FORUM]. An Alpha EVO Pro user felt SDL sine vibration, which restarted on every update ([ReIGlue PR #8](https://github.com/FH1-ReImagined/ReIGlue/pull/8)) [FORUM]. | **Synthesize vibration in the constant force** (≤ ~60 Hz at a 360 Hz update rate). Native Sine stays experimental: update ≤ 30–60 Hz with `NoRestart`. |
| Square, Triangle, SawtoothUp/Down, Ramp | "no effect seen on wheelbase" ([hid-simagic.c L31-37](https://github.com/JacKeTUs/simagic-ff/blob/master/hid-simagic.c#L31)) [SRC]; "lots of effect types that although are sent to the device, don't really do anything" [FORUM] | **Don't use.** |
| CustomForce | Not implemented in simagic-ff [SRC] | Don't use. |

**Other known Simagic issues:**
- The 16-bit ms duration wrap (§1.7).
- Repeated constant-force effects losing their Set Effect report (§1.7).
- Sign inversion through SimPro (§1.7).
- The range change that kills FFB (§3.3).
- Exclusive mode is the generic DirectInput rule (§1.3). No Simagic-specific exclusive-mode bug was found.
- A Simagic-specific update-rate limit is [UNKNOWN] (§1.9).

---

## 4. Simagic Alpha EVO family

### 4.1 Specs

| | Alpha EVO Sport | Alpha EVO | Alpha EVO Pro | (Alpha EVO Ultra) |
|---|---|---|---|---|
| Peak torque | **9 Nm** | **12 Nm** | **18 Nm** | 28 Nm |
| Encoder | 21-bit (2,097,152 counts/rev) | 21-bit | 21-bit | — |
| Rotation range (SimPro) | 90–2520° | 90–2520° | 90–2520° | — |
| USB | 3670:0500 [FORUM] | 3670:0501 or 0502 [SRC/UNKNOWN which] | 3670:0501 or 0502 | — |
| Software | SimPro Manager 3.x [FORUM] | | | |

Sources:
- Simagic's official spec table, "Max Torque · 9 Nm · 12 Nm · 18 Nm" and "Encoder Resolution · 21-bit" ([simagic.com/pages/alpha-evo](https://simagic.com/pages/alpha-evo); [Alpha EVO user manual](https://image.simagic.com/profile/upload/2025/04/28/SIMAGIC_Alpha%20EVO_UM_EN.pdf)) [DOC, via search summary]. One review claims 12 Nm for the Sport, which contradicts the official figure.
- [EVO Ultra 28 Nm page](https://simagic.com/pages/detail-simagic-alpha-evo-ultra-28nm-direct-drive-wheel-base) [DOC].
- The range comes from the sysfs clamp `90 .. 2520` [SRC] and from guides [FORUM].

### 4.2 Steering axis in DirectInput, and degrees

- **Axis:** X (`DIJOFS_X`, HID Generic Desktop X 0x30). This is the axis every FFB app binds effects to and reads steering from. The Linux driver configures ABS_X from the descriptor [SRC]. MAIRA reads `JoystickState.X` [SRC].
- **Range:** query it. MAIRA normalizes with the queried `Range` as `2*(x − min)/(max − min) − 1`, left = −1 ([DirectInput.cs L468-473](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/DirectInput.cs#L468)) [SRC]. The DirectInput default is 0..65535 [DOC]. Setting `DIPROP_RANGE` to −32768..32767 makes the center 0.
- **Resolution through DirectInput:** probably 16-bit, set by the HID logical range, not the 21-bit encoder. Check `GetObjectPropertiesByName("X").LogicalRange` [UNKNOWN]. At 900° and 16 bits, that is 0.0137° per count, which is plenty.
- **Degrees:** `angleDeg = x_normalized × (SimProAngle / 2)`. The full axis span equals SimPro's rotation "Angle" (`max_angle`). Positive is to the right (clockwise) [INFER]. MAIRA scales with the game's steering lock, not the base setting. RimForce must ask the user for SimPro's Angle, or read it experimentally (§3.4), then **verify it in calibration**: "turn to 90° right" should show 90°.
- The hard stop (`angle_lock`) may be set narrower than `max_angle` [INFER]. Never drive the wheel into the stop deliberately.

---

## 5. Safety practices (existing products) and RimForce requirements

### 5.1 What exists today

**Base hardware (Simagic):**
- **Kill switch / e-stop.** The EVO Ultra manual calls the kill switch "a legally required safety device". "Use the kill switch or power off button to stop the wheelbase and prevent rotation", and release it only after a complete stop ([EVO Ultra manual](https://cdn.shopify.com/s/files/1/0764/6756/8943/files/EVO_Ultra-User_Manual-EN.pdf)) [DOC, via search]. A retailer listing mentions a kill switch for the EVO Sport [FORUM]. **Check whether your base has one.**
- **Protection states:** OverCur, OverVol, OverSpeed, OutOfAng (Alpha Manager "System State") [DOC].
- **Speed limiter** (`WheelSpeed`) [DOC] and **slew-rate control** [FORUM].
- **Firmware end stops** (angle lock and strength) [SRC/INFER].
- Manual: "For first-time users, adjust the force feedback gradually to an appropriate level" [DOC].

**DirectInput level:**
- Exclusive `Unacquire` resets the device (§1.5) [SRC].
- `DISFFC_STOPALL` and `DISFFC_SETACTUATORSOFF` [DOC].
- The `ForceFeedbackState` safety-switch flags (Simagic support [UNKNOWN]).
- **Infinite effects keep playing while the app is alive but hung.** If the FFB thread stalls, the last magnitude persists. Process exit releases the device, which should reset it [INFER].

**Software (MAIRA, irFFB):**
- MAIRA 2.x fades in over 2000 ms and out over 750 ms, and fades on telemetry gaps (250/500 ms). It has crash and curb protection, clipping indicators, and an auto max-force setting [SRC]. MAIRA 1.x applies a "cool down to prevent sore thumbs" 1 s ramp from the last value when it resets the output [SRC].
- irFFB has "reduce when parked" (÷4), minimum-force clamping, soft bump stops near the lock, and clip counting [SRC].
- **Hands-off detection:** Simucube's True Drive/Tuner has a "hands-off protection" feature ([docs.simucube.com wheelbase effects](https://docs.simucube.com/Tuner/wheelbase/WheelbaseEffects.html)) [DOC link, page not fetched]. No Simagic equivalent was found [UNKNOWN].

### 5.2 RimForce safety design (recommended)

These are layered, so any one layer can stop the motor.

1. **Hard torque cap** in Nm, clamped every tick after all mixing. The default is low (e.g. 3 Nm), and the user raises it deliberately. Show it alongside the SimPro cap.
2. **Soft start and stop:**
   - Fade every drill in over 1–2 s (MAIRA: 2 s) and out over 0.3–0.75 s.
   - After a re-acquire or recovery, always restart from zero.
   - Pulses get explicit trapezoid edges of ≥10–20 ms.
3. **Slew-rate limit** on the commanded torque (e.g. 40 Nm/s). Designed pulses get a separate, higher but bounded budget.
4. **Hands-off and runaway detection.** At 360–500 Hz, from the X position, with velocity low-pass filtered (irFFB uses a 6-tap FIR):
   - |ω| > ω_max (e.g. 300–400°/s) while |torque| > a small threshold;
   - |θ − θ_hold| > Δθ_max during a hold (e.g. 30–45°), which suggests the user let go;
   - |θ| approaching the hard lock.

   Any of these trips a fast fade (~50 ms) to zero, pauses the drill and requires a re-arm. The SimPro WheelSpeed limit is the hardware backstop.
5. **E-stop paths, all calling the same `EmergencyStop()`:**
   - a large UI button;
   - a global hotkey (`RegisterHotKey`) that works while the app is unfocused;
   - an optional wheel button, read from the same device state;
   - window-close and session-end handlers.

   `EmergencyStop()` does: magnitude 0, then `Stop()` every effect, then `SendForceFeedbackCommand(StopAll)`. Escalate to `SetActuatorsOff` and `Unacquire` if needed. Remind the user that the base's kill switch is the real e-stop.
6. **Watchdog thread:**
   - The FFB loop increments a heartbeat. If the heartbeat is stale for more than 100 ms, the watchdog calls `EmergencyStop()`.
   - Also hook `AppDomain.ProcessExit`, `UnhandledException`, `SystemEvents.SessionEnding` and `PowerModeChanged` (suspend).
7. **Effect health:** about once per second, check `cfx.Status == EffectStatus.Playing` and `GetForceFeedbackState()`. Restart with a fade if the effect is not playing (Simagic duration wrap, driver reset).
8. **Startup state:** never apply force on connect. The user must press "Start drill" with hands on the wheel. Optionally require a button press on the wheel to confirm grip.
9. **Pre-flight checklist** shown in the UI:
   - SimPro profile: mechanical effects as expected, Game sliders known, Reverse FFB state, WheelSpeed limit set;
   - kill switch within reach;
   - no other FFB app running.

---

## 6. Recommended architecture

- **One device thread** owns every DirectInput call: create, acquire, read, update, recover.
  - This avoids cross-thread COM and DirectInput races. irFFB guards its effect with a critical section, and MAIRA 2.x locks around shared joystick reads [SRC].
  - The thread uses a message-only window, or the main window's HWND obtained once, for `SetCooperativeLevel`.
  - It runs at `ThreadPriority.Highest` (irFFB and MAIRA do the same) [SRC].
- **Timing:** a one-shot high-resolution waitable timer (`CreateWaitableTimerExW(..., CREATE_WAITABLE_TIMER_HIGH_RESOLUTION)`, Windows 10 1803+), re-armed against an absolute `Stopwatch` deadline ladder. MAIRA 2.x does this because periodic mode only supports whole milliseconds and cannot express 360 Hz ([PlayoutTimer.cs L20-23, L94-110](https://github.com/mherbold/MarvinsAIRARefactored/blob/main/MarvinsAIRARefactored/Components/PlayoutTimer.cs#L94)) [SRC]. Fallback: `timeBeginPeriod(1)` with sleep and spin (irFFB) [SRC]. Don't use `PeriodicTimer` or `Task.Delay`, which follow the system timer resolution, typically about 15.6 ms [INFER].
- **Signal path per tick:**
  1. read X
  2. compute degrees and filtered velocity
  3. compute the drill torque in Nm (software spring, hold, pulse or vibration)
  4. apply the safety governor
  5. convert Nm to DI units with the calibration
  6. call `SetConstant`, skipping unchanged values with a 250 ms refresh
- **Native condition effects** (Damper, Friction, centered Spring): created once, at coefficient 0, and changed rarely (≤ 10 Hz). Optional "texture" for drills.
- **Never create or destroy effects at runtime** (Simagic block IDs).

### 6.1 Drill implementations

| Drill | Implementation | Why |
|---|---|---|
| Constant-force hold | `T = const`, sign chosen by the drill. User resists. Hands-off detector active. | Simplest, and exact in Nm (up to the calibration). |
| Spring toward an angle θ0 | **Software:** `T = −k(θ − θ0) − c·ω`, saturated | Native spring can't offset on Simagic. Software gives Nm/deg units. Add damping `c`, plus optionally a small native Damper, for stability. Loop latency of a few ms limits the usable stiffness, so start low [INFER]. |
| Centered spring | Software as above, or native Spring with `Offset=0` | Native is firmware-rate and stable, but its scaling depends on Game Spring and the rotation range [INFER]. |
| Damper | Native Damper, or software `T = −c·ω` (filtered) | Native is smoother. Software is quantifiable. |
| Friction | Native Friction | Software sign(ω) friction chatters near zero velocity. |
| Short force pulses | Software trapezoid in the constant force (edges ≥10–20 ms) | Avoids per-pulse effects and the Simagic restart issues. |
| Vibration | Software `A·sin(2πft)` in the constant force, f ≤ ~60 Hz at 360 Hz | Native Sine is unreliable on Simagic, and restarts on update [SRC/FORUM]. |

---

## 7. Step-by-step implementation plan

1. **Project:** `net10.0-windows`, `<PackageReference Include="Vortice.DirectInput" Version="3.8.3" />`, x64. Keep all DirectInput code in one `RimForce.Device` assembly behind an `IFfbDevice` interface:
   - `Enumerate`, `Open`, `ReadX`, `SetConstant`, `SetDamper`, `SetFriction`, `SetSpring`, `EmergencyStop`, `TryReacquire`, `QueryState`
   - plus a **fake implementation** for UI work and unit tests. This matters because there is no hardware during development.
2. **Constants:** your own `Di` HRESULT table (§1.10), `Infinite = -1`, `NoTrigger = -1`, `NominalMax = 10000`.
3. **Device thread:** create a dedicated `Thread`, create the message-only window there or pass in the main window's HWND, then call `DInput.DirectInput8Create()`.
4. **Enumerate and select:**
   - `GetDevices(GameControl, AttachedOnly|ForceFeedback)`, run off the UI thread because it can be slow.
   - Pick by VID 0x3670 (from `ProductGuid` or `Properties.VendorId`), or let the user choose.
   - Persist `InstanceGuid`.
   - Log `ProductName`, `ForceFeedbackDriverGuid`, `Capabilities.ForceFeedbackSamplePeriod` and `ForceFeedbackMinimumTimeResolution`.
5. **Open:**
   1. `SetDataFormat<RawJoystickState>()`
   2. `SetCooperativeLevel(Exclusive|Background)`
   3. `AutoCenter=false`, X `Range=-32768..32767`, `ForceFeedbackGain=10000` (all best-effort, logged)
   4. `Acquire()`
   5. `Reset` and `SetActuatorsOn` (best-effort)
   6. `GetEffects()`: log each `Name`, `Type`, `StaticParameters` and `DynamicParameters`, and require ConstantForce
   7. create the ConstantForce effect (INFINITE, gain 10000, no trigger, X, dir {1}) and `Start(1)`
   8. optionally create Damper and Friction (and Spring) at coefficient 0 and `Start(1)`

   On `OtherApplicationHasPriority` (0x80070005), tell the user to close the game or FFB app.
6. **Calibration wizard** (persisted per base):
   - (a) model selection, which gives `T_peak` = 9, 12 or 18 Nm;
   - (b) SimPro overall strength %;
   - (c) SimPro Angle (degrees);
   - (d) **direction test:** wheel centered, hands off or very light. Apply +3 % DI (about 0.36 Nm on a 12 Nm base at 100 %) for 300 ms, ramped. Measure the sign of Δθ, store `ForceSign` so that +Nm moves toward +θ, and confirm with the user. Abort if |ω| exceeds a small limit.
   - (e) angle check: "turn to 90° right".
   - (f) optional native-effect check: enable the native Damper at 30 % and ask "do you feel resistance?". If not, the Game Damper slider is probably 0.
7. **FFB loop** (`HiResTimer` at 360 Hz):
   - `ReadX`, velocity filter, drill, `SafetyGovernor`, Nm to DI, `SetConstant` (skip unchanged, 250 ms refresh), heartbeat.
   - Any `SharpGenException` on read, or device loss on write, triggers `EmergencyStop` and recovery.
8. **Recovery:**
   - MAIRA-style backoff: 1 s, 2 s, 4 s … capped at 15 s; reset the counter after 10 s healthy.
   - Steps: `Unacquire` and `Acquire`, then `SetParameters(TypeSpecificParameters|Start)` at magnitude 0 for each effect. Or do a full re-init if that fails.
   - Resume only through a fade-in, and only if the drill is still armed. Otherwise wait for the user.
   - Also re-open on HID hot-plug (`RegisterDeviceNotification` / `WM_DEVICECHANGE`).
9. **Health checks** (1 Hz on the device thread): `cfx.Status`, `GetForceFeedbackState()`, input liveness (X changes, no exceptions), and the loop's actual rate and jitter statistics (MAIRA logs min/avg/max µs) [SRC].
10. **Shutdown:** fade out ≥300 ms, then `EmergencyStop`, dispose the effects, `Unacquire`, dispose the device and DirectInput. Run this on window close, app exit and session end.
11. **Watchdog thread:** a separate thread that calls `EmergencyStop()` if the heartbeat is stale for more than 100 ms. DirectInput calls from a second thread are a risk; serialize them with a lock that the watchdog takes with a timeout [INFER].

The code in §2.3 and the compile-checked sketch below cover steps 5, 7 and 10:

```csharp
// Units: DI = sign * Nm / (T_peak * SimProStrength) * 10000   (approximate)
public int NmToDi(double nm) =>
    (int)Math.Round(_cal.ForceSign * nm / (_cal.PeakTorqueNm * _cal.SimProStrength01) * 10000);
public double XToDeg(int x) => x / 32768.0 * (_cal.RotationRangeDeg / 2.0);   // after DIPROP_RANGE ±32768

public bool Tick(Func<double, double, double, double> drillNm)    // (deg, deg/s, t) -> Nm
{
    double dt = ...;                                               // from Stopwatch
    double deg;
    try { deg = XToDeg(_dev.ReadX()); } catch (SharpGenException) { _dev.EmergencyStop(); return false; }
    _vel += 0.2 * ((deg - _lastDeg) / dt - _vel); _lastDeg = deg;  // low-pass velocity
    double nm = _safety.Apply(drillNm(deg, _vel, t), _vel, dt);    // cap, fade, slew, runaway trip
    int di = NmToDi(nm);
    if (di != _lastSent || now - _lastSend > Stopwatch.Frequency / 4) {   // skip unchanged, 250 ms refresh
        if (!_dev.SetConstant(di)) return false;                   // device loss -> recovery w/ backoff
        _lastSent = di; _lastSend = now;
    }
    Heartbeat++;
    return true;
}

// Software spring toward any angle (native Spring can't offset on Simagic):
static Func<double,double,double,double> Spring(double centerDeg, double nmPerDeg, double damp, double maxNm) =>
    (deg, vel, t) => Math.Clamp(-nmPerDeg * (deg - centerDeg) - damp * vel, -maxNm, maxNm);
```

---

## 8. Open questions (need a hardware test)

1. Does Simagic's Windows driver honour `DIPROP_FFGAIN` and per-effect `dwGain`?
2. Does `DIEP_NORESTART` work for constant force, or does it return `DIERR_EFFECTPLAYING`? What does `EffectInfo.DynamicParameters` say?
3. What is the practical maximum update rate before USB queueing or jitter appears (360 / 500 / 1000 Hz)?
4. Does an INFINITE-duration effect keep playing for 10+ minutes? (It should. The bug only affects large finite durations.)
5. Sign convention: does +magnitude with dir {1} push left, as expected, with SimPro "Reverse FFB" off?
6. X axis `LogicalRange` and resolution. Does the full X span equal the SimPro Angle exactly?
7. Do the native Spring, Damper and Friction respond, how do they scale with SimPro's Game sliders, and what are their units in Nm/deg?
8. Native Sine at 5–60 Hz: does it work at all on the EVO?
9. Does the kill switch show up in `GetForceFeedbackState()`, cause `DIERR_INPUTLOST`, or neither? Does FFB resume cleanly after the kill switch is released?
10. Can SimPro Manager stay open while RimForce holds the device exclusively? Does SimPro push profile changes mid-session?
11. EVO PID mapping: which of 0x0501 and 0x0502 is which model?

## 9. Hardware test checklist (for the first session with the base)

Run with a low SimPro strength (e.g. 30 %), WheelSpeed limited, and hands on the wheel.

1. Enumerate and open. Log effects and DynamicParameters, the X LogicalRange, and the FFB driver GUID.
2. With another FFB app running, Acquire should fail with 0x80070005, and RimForce should show a clear message.
3. +5 % constant force for 1 s, then −5 %. Note the direction, and confirm the direction-calibration logic.
4. Ramp 0 → 30 % → 0 over 5 s. Check that the feel is linear.
5. Hold 10 % for 5 min. Check there are no dropouts (duration bug) and that `Status` stays Playing.
6. Rate test: a 1 Hz triangle at 250, 360, 500 and 1000 Hz update rates. Watch for stutter, and log the loop's jitter.
7. Pulses of 50/100/200 ms at 20 %. Check edge sharpness against SimPro's filter and slew settings.
8. Software vibration at 10, 20, 40 and 60 Hz.
9. Native Spring, Damper and Friction at 30 % with the Game sliders at 100 %, then at 0 %.
10. Native Spring with `Offset = 3000`. Expect it to be ignored.
11. Native Sine at 10 and 40 Hz, with and without `NoRestart` updates.
12. E-stop through the UI button, the hotkey, the wheel button, and the base kill switch. Check behaviour and recovery for each.
13. Unplug and replug USB, and power-cycle the base mid-drill. Recovery must restart from zero force only.
14. Hands-off test at a low hold torque. The detector should trip within the configured angle and speed.

---

## 10. References

**Source code (primary):**
- irFFB: https://github.com/nlp80/irFFB — `irFFB/irFFB.cpp` (https://github.com/nlp80/irFFB/blob/master/irFFB/irFFB.cpp), `irFFB/settings.cpp`, `irFFB/irFFB.h`; wiki https://github.com/nlp80/irFFB/wiki/Configuration
- MAIRA 1.x (SharpDX, net8): https://github.com/mherbold/MarvinsAIRA — `App.ForceFeedback.cs`, `App.Inputs.cs`, `MarvinsAIRA.csproj`
- MAIRA 2.x (SharpDX, .NET 9): https://github.com/mherbold/MarvinsAIRARefactored — `MarvinsAIRARefactored/Components/DirectInput.cs`, `PlayoutTimer.cs`, `RacingWheel.cs`, `CLAUDE.md`
- SDL DirectInput haptics: https://github.com/libsdl-org/SDL/blob/main/src/haptic/windows/SDL_dinputhaptic.c ; helper window: https://github.com/libsdl-org/SDL/blob/main/src/core/windows/SDL_windows.c
- Wine dinput (Windows-behaviour spec): https://github.com/wine-mirror/wine/blob/master/dlls/dinput/joystick_hid.c , https://github.com/wine-mirror/wine/blob/master/dlls/dinput/device.c , https://github.com/wine-mirror/wine/blob/master/dlls/dinput/tests/force_feedback.c , https://github.com/wine-mirror/wine/blob/master/include/dinput.h
- simagic-ff (Linux Simagic driver): https://github.com/JacKeTUs/simagic-ff — `hid-simagic.c`, `hid-simagic.h`, `hid-simagic-settings.h/.c`, `hid-simagic-sysfs.c`, README; issue https://github.com/JacKeTUs/simagic-ff/issues/3
- universal-pidff: https://github.com/JacKeTUs/universal-pidff (`hid-pidff.h` quirks)
- OpenFFBoard: https://github.com/Ultrawipf/OpenFFBoard (`Firmware/FFBoard/Inc/ffb_defs.h`); wiki https://github.com/Ultrawipf/OpenFFBoard/wiki
- Unity-DirectInput: https://github.com/MrTimcakes/Unity-DirectInput (`DirectInputForceFeedback~/DirectInputForceFeedback/DirectInputForceFeedback.cpp`); https://github.com/imDanoush/Unity-DirectInput ; https://github.com/ATG-Simulator/Unity-DirectInput
- Trueforce-For-All (SimHub plugin, SharpDX): https://github.com/Mhytee/Trueforce-For-All/blob/main/src/TrueforceForAll.Plugin/DirectInputWheel.cs
- icsmoi (Vortice FFB): https://github.com/tuduce/icsmoi/blob/main/src/icsmoi.Core/Services/DirectInput/FfbDeviceManager.cs , https://github.com/tuduce/icsmoi/blob/main/.claude/CLAUDE.md
- BizHawk fork (Vortice input): https://github.com/jlefebure/bizhawk-twitchplays/blob/master/src/BizHawk.Bizware.Input/DirectX/DGamepad.cs
- SimagicHPR (raw-HID pedal haptics): https://github.com/mherbold/SimagicHPR
- Simagic docs source (Alpha Manager settings): https://github.com/simagic/simagic.github.io/blob/main/_pages/base/alpha/alpha_manager.md

**Wrappers:**
- Vortice.DirectInput NuGet: https://www.nuget.org/packages/Vortice.DirectInput ; source https://github.com/amerkoleci/Vortice.Windows/tree/main/src/Vortice.DirectInput (`ResultCode.cs`, `EffectParameters.cs`, `IDirectInputEffect.cs`, `IDirectInputDevice8.cs`, `ObjectProperties.cs`, `Mappings.xml`); commits https://github.com/amerkoleci/Vortice.Windows/commits/main ; issue https://github.com/amerkoleci/Vortice.Windows/issues/472
- SharpDX.DirectInput NuGet: https://www.nuget.org/packages/SharpDX.DirectInput ; archived repo https://github.com/sharpdx/SharpDX

**Community reports (Simagic-specific):**
- Duration wrap (Simagic 16-bit ms): https://github.com/OverkillLabs3/driveclub-recomp/issues/102 , https://github.com/OverkillLabs3/driveclub-recomp/issues/239
- Sine restarts and update rate on Alpha EVO Pro: https://github.com/FH1-ReImagined/ReIGlue/pull/8 ; https://github.com/FH1-ReImagined/ForzaHorizon1-ReImagined/issues/273
- Alpha EVO Sport USB ID and settings on Linux: https://github.com/Adiakys/SimRig/issues/22

**Microsoft DirectInput docs** (read via search summaries; host blocked in the sandbox):
- Cooperative Levels https://msdn.microsoft.com/en-us/library/ee416848(v=vs.85)
- SetCooperativeLevel https://msdn.microsoft.com/en-us/library/ee417921(v=vs.85)
- Creating the FF device https://learn.microsoft.com/en-us/previous-versions/ee418821(v=vs.85)
- Effect Direction https://msdn.microsoft.com/en-us/library/ee417536(v=vs.85)
- DIEFFECT https://msdn.microsoft.com/en-us/library/windows/apps/ee416616(v=vs.85)
- Device gain https://msdn.microsoft.com/en-us/library/ee417675(v=vs.85)
- Device Properties https://msdn.microsoft.com/en-us/library/ee416595(v=vs.85)
- Periodic Effects https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee418719(v=vs.85)
- Wine direction thread https://list.winehq.org/pipermail/wine-devel/2014-March/103281.html

**Simagic and specs** (read via search summaries):
- Alpha EVO specs https://simagic.com/pages/alpha-evo
- Alpha EVO manual https://image.simagic.com/profile/upload/2025/04/28/SIMAGIC_Alpha%20EVO_UM_EN.pdf
- EVO Ultra manual https://cdn.shopify.com/s/files/1/0764/6756/8943/files/EVO_Ultra-User_Manual-EN.pdf
- EVO Ultra page https://simagic.com/pages/detail-simagic-alpha-evo-ultra-28nm-direct-drive-wheel-base
- Download center https://simagic.com/pages/download-center
- SimPro guides: https://simracingcockpit.gg/acc-simagic-settings/ , https://simracingcockpit.gg/simagic-troubleshooting/ , https://coachdaveacademy.com/tutorials/new-simagic-iracing-settings-guide/ , https://gonkyracing.com/en/ffb-guide/simagic-alpha-evo , https://simracercentral.com/simagic-simpro-manager-guide/

**iRacing strength:**
- https://driver61.com/sim-racing/iracing-force-feedback-setup/
- https://simracingcockpit.gg/iracing-ffb-settings/
- https://community.granitedevices.com/t/iracing-and-simucube-2/3049/147

**Other:**
- MOZA SDK (for comparison) https://mozaracing.com/pages/sdk
- Simucube wheelbase effects (hands-off protection) https://docs.simucube.com/Tuner/wheelbase/WheelbaseEffects.html
