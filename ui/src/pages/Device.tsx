import { useState } from 'react';
import { send, useAppState, useTick } from '../bridge';
import { Card, Slider, Toggle } from '../components/ui';
import { WheelGauge } from '../components/WheelGauge';

const EFFECTS: [string, string, string][] = [
  ['constant-left', 'Constant ◀', 'Pulls left'],
  ['constant-right', 'Constant ▶', 'Pulls right'],
  ['spring', 'Spring', 'Pulls to centre'],
  ['damper', 'Damper', 'Heavier when fast'],
  ['friction', 'Friction', 'Like sand'],
  ['vibration', 'Vibration', '20 Hz buzz'],
];

export function Device() {
  const st = useAppState()!;
  const t = useTick(15);
  const [pct, setPct] = useState(6);
  const [err, setErr] = useState<string | null>(null);
  const [kg, setKg] = useState('');
  const [calPct, setCalPct] = useState(10);
  const act: any = t?.activity;
  const testing = act?.kind === 'deviceTest' ? act.effect : null;
  const dir = act?.kind === 'directionCheck' ? act : null;
  const run = (p: Promise<any>) => p.then(() => setErr(null)).catch((e) => setErr(e.message));
  const s = st.settings;
  const save = (patch: any) => run(send('saveSettings', { settings: { ...s, ...patch } }));

  return (
    <div className="page fade-in">
      <h2 className="page-title">Device</h2>
      <p className="page-sub">Calibrate, verify force direction and feel each effect alone at ≤ 10% before any drill.</p>
      {err && <div className="card tight" style={{ borderColor: 'rgba(244,63,94,0.5)', marginBottom: 12 }}>{err}</div>}
      <div className="grid" style={{ gridTemplateColumns: '1.1fr 1fr', gap: 16, alignItems: 'start' }}>
        <div className="col" style={{ gap: 16 }}>
          <Card title="Live wheel" right={<span className="pill mono">{(t?.loopHz ?? 0).toFixed(0)} Hz · max gap {(t?.maxLoopGapMs ?? 0).toFixed(1)} ms</span>}>
            <div style={{ height: 420 }}><WheelGauge guided={false} /></div>
            <div className="grid" style={{ gridTemplateColumns: 'repeat(4,1fr)', marginTop: 8 }}>
              <div className="kpi"><span className="l">Angle</span><span className="v" style={{ fontSize: 20 }}>{(t?.angleDeg ?? 0).toFixed(1)}°</span></div>
              <div className="kpi"><span className="l">Speed</span><span className="v" style={{ fontSize: 20 }}>{(t?.velocityDps ?? 0).toFixed(0)}<span className="u">°/s</span></span></div>
              <div className="kpi"><span className="l">Motor</span><span className="v" style={{ fontSize: 20 }}>{(t?.motorTorqueNm ?? 0).toFixed(2)}<span className="u">Nm</span></span></div>
              <div className="kpi"><span className="l">Command</span><span className="v" style={{ fontSize: 20 }}>{(t?.commandPct ?? 0).toFixed(1)}<span className="u">%</span></span></div>
            </div>
          </Card>
          <Card title="Wheel">
            <div className="row" style={{ gap: 10 }}>
              <select style={{ flex: 1 }} value={st.deviceId ?? ''} onChange={(e) => run(send('selectDevice', { id: e.target.value }))}>
                {st.devices.map((d) => <option key={d.id} value={d.id}>{d.name}{d.isSimulated ? '' : ''}</option>)}
              </select>
              <span className={`pill ${t?.connected ? 'ok' : 'warn'}`}><span className="dot" />{t?.connected ? 'Connected' : 'Not connected'}</span>
            </div>
            <div className="dim" style={{ fontSize: 12, marginTop: 8 }}>
              Max torque now: <b className="mono">{st.maxTorqueNm.toFixed(1)} Nm</b> ({s.wheel.baseMaxTorqueNm} Nm × SimPro {s.wheel.simProStrengthPct}% × calibration {s.wheel.torqueCalibration.toFixed(2)}) · safety cap <b className="mono">{st.capNm.toFixed(1)} Nm</b>
            </div>
          </Card>
        </div>

        <div className="col" style={{ gap: 16 }}>
          <Card title="1 · Centre & angle direction">
            <p className="muted" style={{ marginTop: 0, fontSize: 13 }}>Hold the wheel straight and set the centre. Then turn right: the gauge must go to <b>+</b>. If not, invert the angle.</p>
            <div className="row">
              <button className="btn" onClick={() => run(send('setCentre'))}>Set centre here</button>
              <div className="grow" />
              <Toggle on={s.wheel.invertAngle} onChange={(v) => save({ wheel: { ...s.wheel, invertAngle: v } })} label="Invert angle" />
            </div>
          </Card>
          <Card title="2 · Force direction check" accent={s.directionVerified ? '#34d399' : '#fbbf24'}>
            <p className="muted" style={{ marginTop: 0, fontSize: 13 }}>
              <b style={{ color: '#fbbf24' }}>Hands OFF the wheel.</b> RimForce gives a tiny clockwise nudge (4% for 0.2 s) and checks which way the wheel moved.
              A reversed direction would turn springs into anti-springs, so arming is blocked until this passes.
            </p>
            <div className="row">
              <button className="btn primary" disabled={!!dir && dir.result === 'running'} onClick={() => run(send('directionCheck'))}>{dir?.result === 'running' ? 'Checking…' : 'Run direction check'}</button>
              <span className={`pill ${s.directionVerified ? 'ok' : 'warn'}`}><span className="dot" />{s.directionVerified ? 'Verified' : st.requireDirectionCheck ? 'Not verified' : 'Not needed (simulator)'}</span>
              {s.wheel.invertForce && <span className="tag">force inverted</span>}
            </div>
          </Card>
          <Card title="3 · Feel each effect (≤ 10%)">
            <Slider label="Strength" value={pct} min={1} max={10} step={1} unit="%" onChange={setPct} hint={`${(st.maxTorqueNm * pct / 100).toFixed(2)} Nm — start at 3-5% the first time`} />
            <div className="grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)', gap: 8, marginTop: 12 }}>
              {EFFECTS.map(([id, label, hint]) => (
                <button key={id} className={`btn ${testing === id ? 'primary' : ''}`} style={{ height: 56, flexDirection: 'column', gap: 0 }} onClick={() => run(send('deviceTest', { effect: id, pct }))}>
                  <span>{label}</span><span style={{ fontSize: 11, opacity: 0.7, fontWeight: 400 }}>{hint}</span>
                </button>
              ))}
            </div>
            <button className="btn danger" style={{ width: '100%', marginTop: 10 }} onClick={() => run(send('deviceTest', { effect: 'none' }))}>Stop test</button>
          </Card>
          <Card title="4 · Torque calibration (optional)">
            <p className="muted" style={{ marginTop: 0, fontSize: 13 }}>Hook a luggage scale on the rim at 3 o'clock, pull it tangentially until the wheel holds still during "Constant ◀" at {calPct}% and enter the reading.</p>
            <div className="row">
              <input type="number" placeholder="kg on the scale" value={kg} onChange={(e) => setKg(e.target.value)} style={{ width: 150 }} />
              <select value={calPct} onChange={(e) => setCalPct(Number(e.target.value))}>{[5, 8, 10].map((v) => <option key={v} value={v}>{v}%</option>)}</select>
              <button className="btn" onClick={() => run(send('calibrateTorque', { measuredKg: Number(kg), appliedPct: calPct }))}>Apply</button>
              <span className="dim mono">factor {s.wheel.torqueCalibration.toFixed(3)}</span>
            </div>
          </Card>
          <Card title="SimPro Manager setup">
            <ol className="howto">
              <li>Create a profile called <b>RimForce</b>: overall strength 100%, rotation 900°.</li>
              <li>Mechanical damper / friction / inertia low (≤ 10%) — RimForce adds its own.</li>
              <li>Close iRacing, irFFB, MAIRA and other FFB apps — only one app can hold the wheel.</li>
              <li>Don't change rotation while the wheel is off-centre.</li>
            </ol>
          </Card>
        </div>
      </div>
    </div>
  );
}
