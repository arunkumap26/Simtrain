export interface Strength {
  torqueNm: number; rimForceN: number; rimForceKgf: number; perHandKgf: number;
  gripKgf: number; gripPct: number; wristNm: number; wristPct: number; shoulderNm: number; shoulderPct: number;
  forearmTwistPct: number; systemPct: number; percentOfMax: number; frontRaiseKg: number; powerW: number;
  limitingLink: string; limitingPct: number; enduranceS: number; budgetUsedPct: number; hands: number;
}

export interface Guide {
  targetDeg: number; zoneDeg: number; path: number[]; pathStepS: number; cue: string; subCue?: string | null;
  pullDir: number; targetSpeedDps?: number | null; repProgress: number; showGhost: boolean;
  hand?: string | null; grip?: string | null; action?: string | null; markerLeft?: number | null; markerRight?: number | null;
}

export interface SessionSnap {
  kind: 'session'; title: string; programRef?: string | null;
  phase: 'ready' | 'countdown' | 'work' | 'setdone' | 'rest' | 'paused' | 'complete';
  phaseElapsedS: number; phaseLenS: number; itemIndex: number; itemCount: number; setIndex: number; setCount: number;
  role: string;
  drill: { id: string; name: string; category: string; accent: string; icon: string; resistanceProfile: string; scoreLabel: string; tagline: string; howTo: string[] };
  hand?: string | null; dir?: string | null; grip?: string | null; loadNm: number; loadPct: number; refMaxNm?: number | null; refIsBreakpoint: boolean;
  workS: number; restS: number; elapsedS: number; guide?: Guide | null; live?: Record<string, number> | null; pausedReason?: string | null;
  last?: { drillName: string; setIndex: number; score: number; scoreLabel: string; completed: boolean; metrics: Record<string, number>; loadNm: number; note?: string | null } | null;
  next?: { name: string; set: number } | null;
  results: { drillName: string; setIndex: number; score: number; scoreLabel: string; completed: boolean; role: string }[];
}

export interface DeviceTestSnap { kind: 'deviceTest'; effect: string; pct: number; nm: number; angle: number; velocity: number; motorNm: number }
export interface DirectionSnap { kind: 'directionCheck'; result: string; angle: number; displacementDeg: number }
export type ActivitySnap = SessionSnap | DeviceTestSnap | DirectionSnap;

export interface Tick {
  t: number; angleDeg: number; velocityDps: number; motorTorqueNm: number; userTorqueNm: number; commandPct: number; envelope: number;
  safety: 'Disarmed' | 'Armed' | 'Tripped'; tripCode?: string | null; tripMessage?: string | null; loopHz: number; maxLoopGapMs: number;
  intensity: number; globalCapNm: number; maxTorqueNm: number; connected: boolean; deviceName?: string | null; simulated: boolean;
  strength: Strength; activity?: ActivitySnap | null;
}

export interface ParamOption { value: string; label: string }
export interface ParamSpec { key: string; label: string; default: number; min: number; max: number; step: number; unit?: string | null; options?: ParamOption[] | null; help?: string | null }
export interface DrillDef {
  id: string; name: string; category: string; tagline: string; description: string; howTo: string[];
  targets: Record<string, number>; resistanceProfile: string; params: ParamSpec[];
  defaultSets: number; defaultWorkS: number; defaultRestS: number; defaultLoadPct: number; drillCapPct: number;
  usesHandChoice: boolean; usesDirection: boolean; direction: string; isTest: boolean; endless: boolean; scoreLabel: string; accent: string; icon: string; why?: string | null;
}

export interface DrillConfig {
  drillId: string; loadPct: number; sets: number; workS: number; restS: number; hand: 'Both' | 'Left' | 'Right'; direction: 'Right' | 'Left' | 'Alternate';
  params: Record<string, number>; role?: string;
}

export interface WheelProfile {
  baseMaxTorqueNm: number; simProStrengthPct: number; rimDiameterMm: number; rotationRangeDeg: number; inertiaKgM2: number;
  handRimFriction: number; torqueCalibration: number; invertForce: boolean; invertAngle: boolean; centerOffsetDeg: number;
}
export interface SafetySettings {
  globalCapPct: number; rampUpS: number; rampDownS: number; minPulseRampS: number; handsOffSpeedDps: number; handsOffTimeS: number;
  runawayDetection: boolean; runawaySpeedDps: number; runawayTimeS: number; runawayMinNm: number; overspeedFadeStartDps: number;
  lockMarginDeg: number; watchdogS: number; finiteEffectDurations: boolean; effectDurationS: number; requireUiHeartbeat: boolean;
  uiHeartbeatTimeoutS: number; stopOnFocusLoss: boolean; armMaxSpeedDps: number;
}
export interface AppSettings {
  wheel: WheelProfile; safety: SafetySettings; checklistAccepted: boolean; directionVerified: boolean; deviceId?: string | null;
  forceUnit: 'kgf' | 'N' | 'lbf'; audioEnabled: boolean; audioVolume: number; voiceCues: boolean; sessionsPerWeek: number; loopHz: number;
  cooperativeLevel: string; nativeConditionEffects: boolean; buttons: Record<string, string>; intensity: number; technique: 'arms' | 'wrists';
  bodyMassKg?: number | null; norms: 'men' | 'women'; onboardingDone: boolean;
}

export interface ProgramSessionView {
  key: string; week: number; number: number; template: string; focus: string; deload: boolean; retest: boolean; baseline: boolean; done: boolean;
  items: { drillId: string; name: string; role: string; sets: number; workS: number; restS: number; loadPct: number; hand: string; direction: string }[];
}
export interface ProgramView {
  createdUtc: string; weeks: number; sessionsPerWeek: number; progress: number; completed: string[]; next?: string | null;
  workingLoadPct: Record<string, number>; workMultiplier: Record<string, number>; log: string[]; sessions: ProgramSessionView[];
}

export interface DeviceInfo { id: string; name: string; isSimulated: boolean; effects: string }

export interface AppState {
  version: string; host: string; demo: boolean; settings: AppSettings; devices: DeviceInfo[]; device?: DeviceInfo | null; deviceId?: string | null;
  requireDirectionCheck: boolean; maxTorqueNm: number; capNm: number; drills: DrillDef[];
  laps: { id: string; name: string; description: string; lapS: number; segments: { name: string; apexDeg: number; duration: number; loadFrac: number }[] }[];
  freeRideProfiles: string[]; breakpoints: Record<string, number>; referenceMaxNm?: number | null;
  program?: ProgramView | null; session?: { title: string; programRef?: string | null; items: number } | null; bindPending?: string | null;
}

export interface History {
  sessions: { id: number; startedUtc: string; endedUtc?: string | null; title: string; programRef?: string | null; sets: number; completed: number; impulseNms: number; workJ: number; tutS: number }[];
  breakpoints: { id: number; measuredUtc: string; hand: string; dir: number; torqueNm: number; estimated: boolean }[];
  weekly: { week: string; sessions: number; sets: number; impulseNms: number; workJ: number; tutS: number }[];
  sets: { id: number; sessionId: number; drillId: string; drillName: string; role: string; hand: string; direction: string; loadNm: number; score: number; scoreLabel: string; completed: boolean; startedUtc: string; metrics: Record<string, number> }[];
}
