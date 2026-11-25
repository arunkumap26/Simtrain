import { useEffect, useState } from 'react';
import { send, useAppState } from '../bridge';
import { Card, Seg, Slider, Toggle } from '../components/ui';
import type { AppSettings } from '../types';

export function SettingsPage() {
  const st = useAppState()!;
  const [s, setS] = useState<AppSettings>(st.settings);
  const [saved, setSaved] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  useEffect(() => setS(st.settings), [st.settings]);
  const dirty = JSON.stringify(s) !== JSON.stringify(st.settings);
  const w = s.wheel, sf = s.safety;
  const setW = (p: Partial<typeof w>) => setS({ ...s, wheel: { ...w, ...p } });
  const setSf = (p: Partial<typeof sf>) => setS({ ...s, safety: { ...sf, ...p } });
  const save = () => send('saveSettings', { settings: s }).then(() => { setSaved(true); setErr(null); setTimeout(() => setSaved(false), 1500); }).catch((e) => setErr(e.message));
  const maxNm = w.baseMaxTorqueNm * w.simProStrengthPct / 100 * w.torqueCalibration;

  return (
    <div className="page fade-in">
      <div className="row" style={{ alignItems: 'flex-end' }}>
        <div className="grow"><h2 className="page-title">Settings</h2><p className="page-sub">Everything that turns percent into real Newton-metres — and keeps your wrists safe.</p></div>
        {err && <span style={{ color: '#fb7185' }}>{err}</span>}
        <button className="btn primary" disabled={!dirty} onClick={save}>{saved ? 'Saved ✓' : 'Save changes'}</button>
      </div>
      <div className="grid" style={{ gridTemplateColumns: '1fr 1fr 1fr', gap: 16, alignItems: 'start' }}>
        <Card title="Wheelbase & rim">
          <div className="col" style={{ gap: 14 }}>
            <div className="field"><label>Alpha EVO model</label>
              <Seg value={w.baseMaxTorqueNm} onChange={(v) => setW({ baseMaxTorqueNm: v })} options={[{ value: 9, label: 'Sport 9 Nm' }, { value: 12, label: 'EVO 12 Nm' }, { value: 18, label: 'Pro 18 Nm' }]} /></div>
            <Slider label="SimPro overall strength" value={w.simProStrengthPct} min={10} max={100} step={5} unit="%" onChange={(v) => setW({ simProStrengthPct: v })} hint="Must match your SimPro training profile. 100% recommended." />
            <div className="field"><label>Rim diameter</label>
              <Seg value={w.rimDiameterMm} onChange={(v) => setW({ rimDiameterMm: v })} options={[270, 280, 290, 300, 320, 330].map((v) => ({ value: v, label: `${v}` }))} /></div>
            <div className="field"><label>Rotation (SimPro)</label>
              <Seg value={w.rotationRangeDeg} onChange={(v) => setW({ rotationRangeDeg: v })} options={[540, 720, 900, 1080].map((v) => ({ value: v, label: `${v}°` }))} /></div>
            <Slider label="Hand–rim friction μ" value={w.handRimFriction} min={0.3} max={1.2} step={0.05} onChange={(v) => setW({ handRimFriction: v })} hint="Used for the grip-squeeze estimate (gloves/alcantara ≈ 0.6–0.9)." />
            <div className="card tight mono" style={{ fontSize: 13 }}>Max torque {maxNm.toFixed(1)} Nm · {(maxNm / (w.rimDiameterMm / 2000) / 9.81).toFixed(1)} kgf at the rim</div>
          </div>
        </Card>

        <Card title="Safety" accent="#f43f5e">
          <div className="col" style={{ gap: 14 }}>
            <Slider label="Global force cap" value={sf.globalCapPct} min={5} max={100} step={5} unit="%" onChange={(v) => setSf({ globalCapPct: v })}
              hint={<span style={{ color: sf.globalCapPct > 60 ? '#fbbf24' : undefined }}>{(maxNm * sf.globalCapPct / 100).toFixed(1)} Nm. Raise in small steps; {sf.globalCapPct > 60 ? 'high loads — thumbs outside the spokes!' : 'first run default is 40%.'}</span>} />
            <Slider label="Hands-off speed" value={sf.handsOffSpeedDps} min={200} max={700} step={25} unit="°/s" onChange={(v) => setSf({ handsOffSpeedDps: v })} />
            <Slider label="Runaway speed (motor winning)" value={sf.runawaySpeedDps} min={90} max={sf.handsOffSpeedDps} step={10} unit="°/s" onChange={(v) => setSf({ runawaySpeedDps: v })} />
            <Slider label="Stop near lock" value={sf.lockMarginDeg} min={10} max={90} step={5} unit="°" onChange={(v) => setSf({ lockMarginDeg: v })} />
            <Toggle on={sf.runawayDetection} onChange={(v) => setSf({ runawayDetection: v })} label="Runaway detection" hint="Stops when the motor is spinning the wheel on its own." />
            <Toggle on={sf.stopOnFocusLoss} onChange={(v) => setSf({ stopOnFocusLoss: v })} label="Stop when RimForce loses focus" />
            <Toggle on={sf.finiteEffectDurations} onChange={(v) => setSf({ finiteEffectDurations: v })} label="Effect lapse safety (experimental)" hint="Finite 250 ms effects restarted every 100 ms so a frozen app lets force lapse. May pulse on Simagic — test on the Device page." />
          </div>
        </Card>

        <div className="col" style={{ gap: 16 }}>
          <Card title="You">
            <div className="col" style={{ gap: 14 }}>
              <div className="field"><label>Compare against norms for</label>
                <Seg value={s.norms} onChange={(v) => setS({ ...s, norms: v })} options={[{ value: 'men', label: 'Men' }, { value: 'women', label: 'Women' }]} /></div>
              <div className="field"><label>Technique</label>
                <Seg value={s.technique} onChange={(v) => setS({ ...s, technique: v })} options={[{ value: 'arms', label: 'Arms (push-pull)' }, { value: 'wrists', label: 'Wrists only' }]} />
                <span className="dim" style={{ fontSize: 12 }}>Wrists only = elbows tucked, forearm rotation. Turns 12 Nm into a max-strength load.</span></div>
              <Slider label="Body mass" value={s.bodyMassKg ?? 80} min={40} max={140} step={1} unit="kg" onChange={(v) => setS({ ...s, bodyMassKg: v })} hint="Arm weight adds to the shoulder load estimate." />
              <div className="field"><label>Force unit</label>
                <Seg value={s.forceUnit} onChange={(v) => setS({ ...s, forceUnit: v })} options={[{ value: 'kgf', label: 'kgf' }, { value: 'N', label: 'N' }, { value: 'lbf', label: 'lbf' }]} /></div>
            </div>
          </Card>
          <Card title="Audio">
            <div className="col" style={{ gap: 12 }}>
              <Toggle on={s.audioEnabled} onChange={(v) => setS({ ...s, audioEnabled: v })} label="Sound cues" />
              <Toggle on={s.voiceCues} onChange={(v) => setS({ ...s, voiceCues: v })} label="Voice countdown & coaching" />
              <Slider label="Volume" value={Math.round(s.audioVolume * 100)} min={0} max={100} step={5} unit="%" onChange={(v) => setS({ ...s, audioVolume: v / 100 })} />
            </div>
          </Card>
          <Card title="Wheel buttons">
            <div className="col" style={{ gap: 8 }}>
              {['IntensityUp', 'IntensityDown', 'Next', 'Stop'].map((a) => {
                const bound = Object.entries(st.settings.buttons).find(([, v]) => v === a)?.[0];
                return (
                  <div key={a} className="row" style={{ justifyContent: 'space-between' }}>
                    <span>{a.replace('Intensity', 'Resistance ').replace('Up', '+').replace('Down', '−')}</span>
                    <span className="row"><span className="mono dim">{bound != null ? `button ${Number(bound) + 1}` : '—'}</span>
                      <button className={`btn small ${st.bindPending === a ? 'primary' : ''}`} onClick={() => send('bindButton', { action: a })}>{st.bindPending === a ? 'Press a wheel button…' : 'Bind'}</button></span>
                  </div>
                );
              })}
              <button className="btn small ghost" onClick={() => send('clearButtons')}>Clear bindings</button>
            </div>
          </Card>
          <Card title="Engine">
            <div className="col" style={{ gap: 12 }}>
              <Slider label="Force loop rate" value={s.loopHz} min={250} max={500} step={10} unit="Hz" onChange={(v) => setS({ ...s, loopHz: v })} hint="360 Hz like irFFB/MAIRA. Higher may overload some bases." />
              <div className="field"><label>DirectInput mode</label>
                <Seg value={s.cooperativeLevel} onChange={(v) => setS({ ...s, cooperativeLevel: v })} options={[{ value: 'foreground', label: 'Foreground (safer)' }, { value: 'background', label: 'Background' }]} /></div>
              <button className="btn small ghost" onClick={() => send('saveSettings', { settings: { ...st.settings, onboardingDone: false } })}>Re-run first-time setup</button>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}
