import { useState } from 'react';
import { send, useAppState, useTick } from '../bridge';
import { Seg, Slider } from '../components/ui';
import type { AppSettings } from '../types';

const CHECKS = [
  ['Rig bolted down', 'Wheelbase and rig are firmly bolted. The wheel can produce a lot of torque.'],
  ['Thumbs outside the spokes', 'Never hook thumbs inside the spokes — a sudden kick can injure them.'],
  ['Other FFB apps closed', 'Close iRacing, irFFB, MAIRA, SimHub FFB etc. Only one app may drive the motor.'],
  ['Dedicated SimPro profile', 'A "RimForce" SimPro profile: overall strength 100%, mechanical effects low. RimForce does the scaling.'],
];

export function Onboarding() {
  const st = useAppState()!;
  const t = useTick(10);
  const [step, setStep] = useState(0);
  const [checks, setChecks] = useState<boolean[]>(CHECKS.map(() => st.settings.checklistAccepted));
  const [s, setS] = useState<AppSettings>(st.settings);
  const [err, setErr] = useState<string | null>(null);
  const steps = st.requireDirectionCheck ? 6 : 5;
  const dirActive = (t?.activity as any)?.kind === 'directionCheck';

  const finish = () => send('saveSettings', { settings: { ...s, directionVerified: st.settings.directionVerified, onboardingDone: true } }).catch((e) => setErr(e.message));
  const next = async () => {
    setErr(null);
    try {
      if (step === 1) await send('acceptChecklist');
      if (step === 3) await send('saveSettings', { settings: { ...s, checklistAccepted: true, directionVerified: st.settings.directionVerified } });
      setStep(step + 1);
    } catch (e: any) { setErr(e.message); }
  };

  return (
    <div className="modal-bg">
      <div className="modal fade-in">
        <div className="steps">{Array.from({ length: steps }).map((_, i) => <i key={i} className={i <= step ? 'on' : ''} />)}</div>
        {step === 0 && (
          <>
            <div className="row" style={{ gap: 18 }}>
              <img src="./icon.png" width={88} style={{ filter: 'drop-shadow(0 0 24px rgba(34,211,238,0.5))' }} />
              <div>
                <div className="head" style={{ fontSize: 44, fontWeight: 800, lineHeight: 1 }}>Welcome to RimForce</div>
                <div className="muted" style={{ marginTop: 6 }}>Your direct-drive wheel, turned into an F1-style steering strength trainer.</div>
              </div>
            </div>
            <p style={{ lineHeight: 1.6 }} className="muted">
              Holds, sprints, eccentrics, reaction drills and race stints — with a ghost wheel that shows exactly where to be, a road of
              what's coming, and live readouts of what your shoulders, wrists and grip are doing. {st.demo ? <b style={{ color: '#fbbf24' }}>You're in demo mode with a simulated wheel — nothing physical will move.</b> : null}
            </p>
          </>
        )}
        {step === 1 && (
          <>
            <div className="head" style={{ fontSize: 34, fontWeight: 800 }}>Safety first</div>
            <p className="muted">A direct-drive base can spin fast enough to hurt a wrist. Confirm each item.</p>
            <div className="col" style={{ gap: 8 }}>
              {CHECKS.map(([title, desc], i) => (
                <div key={title} className={`check ${checks[i] ? 'on' : ''}`} onClick={() => setChecks(checks.map((c, k) => (k === i ? !c : c)))}>
                  <div className="box">{checks[i] ? '✓' : ''}</div>
                  <div><div style={{ fontWeight: 600 }}>{title}</div><div className="dim" style={{ fontSize: 13 }}>{desc}</div></div>
                </div>
              ))}
            </div>
            <p className="dim" style={{ fontSize: 12 }}>Space or Esc stops all force instantly, anywhere in the app. Force always ramps in over 1.5 s, and stops if you let go, near the lock, or if the app loses focus.</p>
          </>
        )}
        {step === 2 && (
          <>
            <div className="head" style={{ fontSize: 34, fontWeight: 800 }}>Your wheel</div>
            <div className="col" style={{ gap: 16, marginTop: 10 }}>
              <div className="field"><label>Alpha EVO model</label>
                <Seg value={s.wheel.baseMaxTorqueNm} onChange={(v) => setS({ ...s, wheel: { ...s.wheel, baseMaxTorqueNm: v } })} options={[{ value: 9, label: 'Sport · 9 Nm' }, { value: 12, label: 'EVO · 12 Nm' }, { value: 18, label: 'Pro · 18 Nm' }]} /></div>
              <Slider label="SimPro overall strength (training profile)" value={s.wheel.simProStrengthPct} min={10} max={100} step={5} unit="%" onChange={(v) => setS({ ...s, wheel: { ...s.wheel, simProStrengthPct: v } })} />
              <div className="field"><label>Rim diameter (mm)</label>
                <Seg value={s.wheel.rimDiameterMm} onChange={(v) => setS({ ...s, wheel: { ...s.wheel, rimDiameterMm: v } })} options={[270, 280, 290, 300, 320, 330].map((v) => ({ value: v, label: `${v}` }))} /></div>
              <div className="field"><label>Rotation set in SimPro</label>
                <Seg value={s.wheel.rotationRangeDeg} onChange={(v) => setS({ ...s, wheel: { ...s.wheel, rotationRangeDeg: v } })} options={[540, 720, 900, 1080].map((v) => ({ value: v, label: `${v}°` }))} /></div>
            </div>
          </>
        )}
        {step === 3 && (
          <>
            <div className="head" style={{ fontSize: 34, fontWeight: 800 }}>About you</div>
            <p className="muted">Used only to express wheel torque as "% of a typical person's max" for each body link.</p>
            <div className="col" style={{ gap: 16 }}>
              <div className="field"><label>Strength norms to compare against</label>
                <Seg value={s.norms} onChange={(v) => setS({ ...s, norms: v })} options={[{ value: 'men', label: 'Men' }, { value: 'women', label: 'Women' }]} /></div>
              <div className="field"><label>How you'll mostly resist</label>
                <Seg value={s.technique} onChange={(v) => setS({ ...s, technique: v })} options={[{ value: 'arms', label: 'Arms & shoulders (push-pull)' }, { value: 'wrists', label: 'Wrists / forearms only' }]} /></div>
              <Slider label="Body mass" value={s.bodyMassKg ?? 80} min={40} max={140} step={1} unit="kg" onChange={(v) => setS({ ...s, bodyMassKg: v })} />
              <div className="field"><label>Show forces in</label>
                <Seg value={s.forceUnit} onChange={(v) => setS({ ...s, forceUnit: v })} options={[{ value: 'kgf', label: 'kgf' }, { value: 'N', label: 'Newtons' }, { value: 'lbf', label: 'lbf' }]} /></div>
            </div>
          </>
        )}
        {step === 4 && st.requireDirectionCheck && (
          <>
            <div className="head" style={{ fontSize: 34, fontWeight: 800 }}>Force direction check</div>
            <p className="muted"><b style={{ color: '#fbbf24' }}>Take your hands off the wheel.</b> RimForce will give a tiny clockwise nudge (4% for 0.2 s) and see which way it moves.</p>
            <div className="row">
              <button className="btn primary" disabled={dirActive} onClick={() => send('directionCheck').catch((e) => setErr(e.message))}>{dirActive ? 'Checking…' : 'Run check'}</button>
              <span className={`pill ${st.settings.directionVerified ? 'ok' : 'warn'}`}><span className="dot" />{st.settings.directionVerified ? 'Verified ✓' : 'Not verified yet'}</span>
            </div>
          </>
        )}
        {step === steps - 1 && (
          <>
            <div className="head" style={{ fontSize: 34, fontWeight: 800 }}>You're set</div>
            <ol className="howto" style={{ marginTop: 12 }}>
              <li>On the <b>Device</b> page, feel each effect at 3–5% first.</li>
              <li>Run a <b>Breakpoint Test</b> — every load scales from it.</li>
              <li>Build your <b>8-week plan</b> on Home and follow the ghost.</li>
              <li>Raise the safety cap (Settings) gradually as you get comfortable — 40% by default.</li>
            </ol>
          </>
        )}
        {err && <div style={{ color: '#fb7185', marginTop: 12 }}>{err}</div>}
        <div className="row" style={{ marginTop: 24 }}>
          {step > 0 && <button className="btn ghost" onClick={() => setStep(step - 1)}>Back</button>}
          <div className="spacer" />
          {step < steps - 1 ? (
            <button className="btn primary" disabled={step === 1 && !checks.every(Boolean)} onClick={next}>
              {step === 4 && st.requireDirectionCheck && !st.settings.directionVerified ? 'Skip for now' : 'Continue'}
            </button>
          ) : (
            <button className="btn go" onClick={finish}>Let's train</button>
          )}
        </div>
      </div>
    </div>
  );
}
