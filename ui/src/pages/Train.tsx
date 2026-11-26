import { useRef, useState } from 'react';
import { post, send, useAppState, useTick } from '../bridge';
import { BodyMap, LinkBars } from '../components/BodyMap';
import { DrillIcon } from '../components/Icons';
import { ForceMeter, Runway, TelemetryTrace } from '../components/Instruments';
import { Card, Ring, Seg } from '../components/ui';
import { WheelGauge } from '../components/WheelGauge';
import { force, mmss, rimKgf } from '../format';
import type { SessionSnap, Tick } from '../types';

export const METRIC_META: Record<string, { label: string; unit?: string; d?: number }> = {
  timeInZonePct: { label: 'In zone', unit: '%', d: 0 },
  meanErrDeg: { label: 'Avg error', unit: '°', d: 1 },
  impulseNms: { label: 'Impulse', unit: 'N·m·s', d: 0 },
  workJ: { label: 'Work', unit: 'J', d: 0 },
  tutS: { label: 'Under tension', unit: 's', d: 0 },
  peakNm: { label: 'Peak', unit: 'Nm', d: 1 },
  reps: { label: 'Reps', d: 0 },
  peakSpeedDps: { label: 'Peak speed', unit: '°/s', d: 0 },
  returnRmsDeg: { label: 'Return error', unit: '°', d: 1 },
  pulses: { label: 'Kicks', d: 0 },
  caught: { label: 'Caught', d: 0 },
  lastDevDeg: { label: 'Last kick', unit: '°', d: 1 },
  hits: { label: 'Apexes', d: 0 },
  misses: { label: 'Missed', d: 0 },
  avgReactionMs: { label: 'Reaction', unit: 'ms', d: 0 },
  lap: { label: 'Lap', d: 0 },
  lapS: { label: 'Lap time', unit: 's', d: 0 },
  lastLapAccPct: { label: 'Last lap', unit: '%', d: 0 },
  rampNm: { label: 'Load now', unit: 'Nm', d: 1 },
  bestHeldNm: { label: 'Best held', unit: 'Nm', d: 1 },
  enduranceS: { label: 'At cap for', unit: 's', d: 0 },
  sdDeg: { label: 'Steadiness (SD)', unit: '°', d: 1 },
  maxDevDeg: { label: 'Max deviation', unit: '°', d: 1 },
  workConcJ: { label: 'Concentric work', unit: 'J', d: 0 },
  workEccJ: { label: 'Eccentric work', unit: 'J', d: 0 },
  avgNm: { label: 'Avg torque', unit: 'Nm', d: 2 },
  breakpointNm: { label: 'Breakpoint', unit: 'Nm', d: 2 },
  avgDevDeg: { label: 'Avg kick', unit: '°', d: 1 },
  avgRecoveryMs: { label: 'Recovery', unit: 'ms', d: 0 },
  avgCaptureMs: { label: 'Capture time', unit: 'ms', d: 0 },
  repsPerMin: { label: 'Reps / min', d: 1 },
  speedErrPct: { label: 'Speed error', unit: '%', d: 0 },
  eccentricWorkJ: { label: 'Eccentric work', unit: 'J', d: 0 },
  laps: { label: 'Laps', d: 0 },
  bestLapAccPct: { label: 'Best lap', unit: '%', d: 0 },
  fadePct: { label: 'Fade', unit: '%', d: 0 },
};

const sessionOf = (t: Tick | null) => (t?.activity && (t.activity as any).kind === 'session' ? (t.activity as SessionSnap) : null);

export function Train({ go }: { go: (p: string) => void }) {
  const st = useAppState();
  const t = useTick(15);
  const s = sessionOf(t);
  const unit = st?.settings.forceUnit ?? 'kgf';
  const rim = st?.settings.wheel.rimDiameterMm ?? 300;
  const accent = s?.drill.accent ?? '#22d3ee';

  if (!st) return null;
  if (!s) return <NoSession go={go} />;

  const g = s.guide;
  const phase = s.phase;
  const tripped = t?.safety === 'Tripped';
  const phaseLeft = Math.max(0, s.phaseLenS - (phase === 'work' ? s.elapsedS : s.phaseElapsedS));
  const err = g ? (t?.angleDeg ?? 0) - g.targetDeg : 0;
  const inZone = !!g && g.showGhost && Math.abs(err) <= (g.zoneDeg || 0);
  const cueColor = phase === 'countdown' ? '#f8fafc' : phase !== 'work' ? '#94a3b8' : g?.showGhost ? (inZone ? '#34d399' : '#fbbf24') : accent;
  const handTxt = s.hand && s.hand !== 'both' ? `${s.hand.toUpperCase()} HAND` : 'BOTH HANDS';
  const strength = t?.strength;
  const f = force(strength?.rimForceKgf ?? 0, unit);
  const fh = force(strength?.perHandKgf ?? 0, unit);

  return (
    <div className="cockpit fade-in">
      {/* ---------- header ---------- */}
      <div className="cockpit-head">
        <div style={{ color: accent }}><DrillIcon name={s.drill.icon} color={accent} size={52} /></div>
        <div>
          <div className="title">{s.drill.name}</div>
          <div className="sub">{s.title !== s.drill.name ? `${s.title} · ` : ''}{s.drill.tagline}</div>
        </div>
        <div className="row wrap" style={{ marginLeft: 12, gap: 6 }}>
          <span className="chip" style={{ borderColor: accent, color: accent }}>{s.role}</span>
          <span className="chip">DRILL {s.itemIndex + 1}/{s.itemCount}</span>
          <span className="chip">{handTxt}</span>
          {s.dir && s.drill.id !== 'lock-to-lock' && s.drill.id !== 'apex-hunter' && s.drill.id !== 'stint' && <span className="chip">PUSH {s.dir.toUpperCase()}</span>}
          {g?.grip && <span className="chip" style={{ color: '#a78bfa' }}>{g.grip}</span>}
          <span className="chip mono">{s.loadNm.toFixed(1)} Nm · {s.loadPct.toFixed(0)}% {s.refIsBreakpoint ? 'of max' : 'of base'}</span>
        </div>
        <div className="spacer" />
        <SetDots s={s} accent={accent} />
        <Ring value={s.phaseLenS > 0 ? 1 - phaseLeft / s.phaseLenS : 0} size={66} stroke={6} color={phase === 'rest' ? '#a78bfa' : accent}>
          <div>
            <div className="mono" style={{ fontSize: 17, fontWeight: 700 }}>{phase === 'ready' || phase === 'paused' || phase === 'complete' ? '—' : mmss(phaseLeft)}</div>
            <div className="dim head" style={{ fontSize: 10 }}>{phase}</div>
          </div>
        </Ring>
      </div>

      {/* ---------- force column ---------- */}
      <div className="card tight col" style={{ minHeight: 0, gap: 6 }}>
        <div className="meter-label">Your force</div>
        <div className="big-num" style={{ fontSize: 38 }}>{f.v}<small>{f.u}</small></div>
        <div className="mono muted" style={{ fontSize: 13 }}>{(strength?.torqueNm ?? 0).toFixed(2)} Nm</div>
        <div className="mono dim" style={{ fontSize: 12 }}>{fh.v} {fh.u} / hand</div>
        <div style={{ flex: 1, minHeight: 200 }}><ForceMeter rimDiameterMm={rim} /></div>
        {s.refIsBreakpoint && (
          <div className="row" style={{ justifyContent: 'center' }}>
            <Ring value={(strength?.percentOfMax ?? 0) / 100} size={74} stroke={7} color="#a78bfa">
              <div><div className="mono" style={{ fontSize: 16, fontWeight: 700 }}>{(strength?.percentOfMax ?? 0).toFixed(0)}%</div><div className="dim" style={{ fontSize: 9 }}>OF MAX</div></div>
            </Ring>
          </div>
        )}
      </div>

      {/* ---------- wheel ---------- */}
      <div className="col" style={{ minHeight: 0, gap: 12 }}>
        <div className="wheel-stage card" style={{ flex: 1, padding: 0, overflow: 'hidden' }}>
          <WheelGauge accent={accent} onDrag={st.demo && demoMode.current === 'manual' ? throttledDrag : undefined} />
          {g && phase !== 'countdown' && <div className="cue" style={{ color: cueColor }}>{g.cue}</div>}
          {g?.subCue && phase === 'work' && <div className="subcue">{g.subCue}</div>}
          {phase === 'countdown' && <CountdownOverlay n={Math.ceil(3 - s.phaseElapsedS)} />}
          {(phase === 'ready' || phase === 'paused') && !tripped && <ReadyPanel s={s} />}
          {phase === 'setdone' && s.last && <SetDoneSplash s={s} />}
          {phase === 'rest' && <RestPanel s={s} left={phaseLeft} />}
          {phase === 'complete' && <CompletePanel s={s} go={go} />}
          {tripped && <TripPanel message={t?.tripMessage ?? 'Safety stop'} code={t?.tripCode} />}
          {st.demo && phase !== 'ready' && phase !== 'paused' && phase !== 'complete' && <DemoPanel />}
        </div>
        <div className="card tight" style={{ height: 128, padding: 8 }}><TelemetryTrace /></div>
      </div>

      {/* ---------- runway ---------- */}
      <div className="card tight" style={{ padding: 6, minHeight: 0 }}><Runway accent={accent} /></div>

      {/* ---------- right column ---------- */}
      <div className="side-col" style={{ overflow: 'auto' }}>
        <Card title="Body load · % of typical max" className="tight" right={<span className="tag">{st.settings.norms === 'women' ? 'women' : 'men'} norms</span>}>
          <BodyMap s={strength} hand={s.hand} technique={st.settings.technique} />
          <LinkBars s={strength} technique={st.settings.technique} />
          <EnduranceLine t={t} />
        </Card>
        <LiveMetrics s={s} t={t} rimMm={rim} />
      </div>

      {/* ---------- intensity ---------- */}
      <IntensityBar s={s} t={t} rim={rim} go={go} />
    </div>
  );
}

// ---------- demo driver (simulated wheel only) ----------
const demoMode = { current: 'follow' as 'follow' | 'manual' | 'off' };
let lastDrag = 0;
function throttledDrag(a: number | null) {
  const now = performance.now();
  if (a == null || now - lastDrag < 30) return;
  lastDrag = now;
  post('demoDriver', { mode: 'manual', target: a });
}

function DemoPanel() {
  const [mode, setMode] = useState(demoMode.current);
  const set = (m: typeof mode) => { demoMode.current = m; setMode(m); send('demoDriver', { mode: m }).catch(() => {}); };
  return (
    <div style={{ position: 'absolute', left: 10, bottom: 10, zIndex: 15 }} className="row">
      <span className="meter-label" style={{ fontSize: 10 }}>Demo driver</span>
      <Seg value={mode} onChange={set} options={[{ value: 'follow', label: 'Auto' }, { value: 'manual', label: 'Mouse' }, { value: 'off', label: 'Hands off' }]} />
    </div>
  );
}

function SetDots({ s, accent }: { s: SessionSnap; accent: string }) {
  const done = s.results.filter((r) => r.drillName === s.drill.name).length;
  return (
    <div className="row" style={{ gap: 5 }}>
      {Array.from({ length: s.setCount }).map((_, i) => (
        <div key={i} style={{
          width: 26, height: 8, borderRadius: 4,
          background: i < done ? accent : i === s.setIndex && s.phase === 'work' ? `${accent}88` : '#1c2636',
          boxShadow: i === s.setIndex && s.phase === 'work' ? `0 0 12px ${accent}` : undefined,
        }} />
      ))}
      <span className="mono dim" style={{ fontSize: 12, marginLeft: 4 }}>SET {Math.min(s.setIndex + 1, s.setCount)}/{s.setCount}</span>
    </div>
  );
}

function CountdownOverlay({ n }: { n: number }) {
  return <div className="overlay"><div key={n} className="count-num">{n > 0 ? n : 'GO'}</div></div>;
}

function ReadyPanel({ s }: { s: SessionSnap }) {
  const [err, setErr] = useState<string | null>(null);
  const start = () => send('go').then(() => setErr(null)).catch((e) => setErr(e.message));
  return (
    <div style={{ position: 'absolute', left: 0, right: 0, bottom: 18, display: 'flex', justifyContent: 'center', zIndex: 12 }}>
      <div className="card" style={{ maxWidth: 640, background: 'rgba(8,12,18,0.92)', textAlign: 'left' }}>
        <div className="row" style={{ alignItems: 'flex-start', gap: 18 }}>
          <div className="grow">
            <div className="head" style={{ fontSize: 13, color: s.phase === 'paused' ? '#fbbf24' : 'var(--text-2)' }}>
              {s.phase === 'paused' ? `Paused — ${s.pausedReason ?? ''}` : 'Get in position'}
            </div>
            <ol className="howto" style={{ marginTop: 8 }}>
              {s.drill.howTo.slice(0, 3).map((h) => <li key={h}>{h}</li>)}
            </ol>
            {err && <div style={{ color: '#fb7185', marginTop: 8 }}>{err}</div>}
          </div>
          <div className="col" style={{ alignItems: 'center' }}>
            <button className="btn go" onClick={start}>{s.phase === 'paused' ? 'Resume' : 'Arm & start'}</button>
            <span className="dim" style={{ fontSize: 11 }}>or press <span className="kbd">Enter</span></span>
          </div>
        </div>
      </div>
    </div>
  );
}

function SetDoneSplash({ s }: { s: SessionSnap }) {
  const l = s.last!;
  const isScore = l.scoreLabel === 'score';
  return (
    <div className="overlay">
      <div className="panel fade-in">
        <div className="head muted" style={{ fontSize: 14 }}>{l.completed ? 'Set complete' : 'Set stopped'}</div>
        <div className="row" style={{ justifyContent: 'center', margin: '14px 0' }}>
          <Ring value={isScore ? l.score / 100 : 1} size={150} stroke={10} color={isScore ? (l.score > 80 ? '#34d399' : l.score > 55 ? '#fbbf24' : '#fb923c') : s.drill.accent}>
            <div><div className="mono" style={{ fontSize: 42, fontWeight: 700 }}>{l.score.toFixed(l.scoreLabel === 'Nm' ? 1 : 0)}</div><div className="dim head">{l.scoreLabel}</div></div>
          </Ring>
        </div>
      </div>
    </div>
  );
}

function RestPanel({ s, left }: { s: SessionSnap; left: number }) {
  const l = s.last;
  const keys = l ? Object.keys(l.metrics).filter((k) => METRIC_META[k]).slice(0, 6) : [];
  return (
    <div className="overlay">
      <div className="panel fade-in" style={{ minWidth: 520 }}>
        <div className="row" style={{ gap: 22, alignItems: 'center' }}>
          <Ring value={s.phaseLenS > 0 ? left / s.phaseLenS : 0} size={130} stroke={9} color="#a78bfa">
            <div><div className="mono" style={{ fontSize: 34, fontWeight: 700 }}>{mmss(left)}</div><div className="dim head">rest</div></div>
          </Ring>
          <div className="grow" style={{ textAlign: 'left' }}>
            {l && (
              <>
                <div className="head muted" style={{ fontSize: 13 }}>Last set · {l.drillName}</div>
                <div className="mono" style={{ fontSize: 30, fontWeight: 700 }}>{l.score.toFixed(l.scoreLabel === 'Nm' ? 1 : 0)} <span className="dim" style={{ fontSize: 14 }}>{l.scoreLabel}</span></div>
                <div className="grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)', gap: 6, marginTop: 6 }}>
                  {keys.map((k) => <div key={k} style={{ fontSize: 12 }}><div className="dim">{METRIC_META[k].label}</div><div className="mono">{l.metrics[k].toFixed(METRIC_META[k].d ?? 1)}{METRIC_META[k].unit ? ` ${METRIC_META[k].unit}` : ''}</div></div>)}
                </div>
              </>
            )}
            {s.next && <div style={{ marginTop: 10 }} className="muted">Next: <b style={{ color: 'var(--text)' }}>{s.next.name}</b> · set {s.next.set}</div>}
          </div>
        </div>
        <div className="row" style={{ justifyContent: 'center', marginTop: 16 }}>
          <button className="btn" onClick={() => send('next')}>Skip rest</button>
          <span className="dim" style={{ fontSize: 12 }}>Shake out your hands. Breathe.</span>
        </div>
      </div>
    </div>
  );
}

function CompletePanel({ s, go }: { s: SessionSnap; go: (p: string) => void }) {
  const done = s.results.filter((r) => r.completed).length;
  const avg = s.results.filter((r) => r.scoreLabel === 'score');
  return (
    <div className="overlay">
      <div className="panel fade-in" style={{ minWidth: 560 }}>
        <div className="head" style={{ fontSize: 40, fontWeight: 800, color: '#34d399' }}>Session complete</div>
        <div className="muted">{s.title}</div>
        <div className="row" style={{ justifyContent: 'center', gap: 30, margin: '18px 0' }}>
          <div className="kpi"><span className="l">Sets</span><span className="v">{done}/{s.results.length}</span></div>
          <div className="kpi"><span className="l">Avg score</span><span className="v">{avg.length ? (avg.reduce((a, r) => a + r.score, 0) / avg.length).toFixed(0) : '—'}</span></div>
        </div>
        <div className="row" style={{ justifyContent: 'center' }}>
          <button className="btn primary" onClick={() => { send('endSession'); go('progress'); }}>See progress</button>
          <button className="btn" onClick={() => { send('endSession'); go('home'); }}>Home</button>
        </div>
      </div>
    </div>
  );
}

export function TripPanel({ message, code }: { message: string; code?: string | null }) {
  const focus = code === 'FocusLost';
  const keepRunning = () => send('setFocusMode', { mode: 'background' }).then(() => send('ackTrip'));
  return (
    <div className="overlay" style={{ zIndex: 30 }}>
      <div className="panel trip-panel fade-in">
        <div style={{ fontSize: 54 }}>⛔</div>
        <div className="head" style={{ fontSize: 34, fontWeight: 800, color: '#fb7185' }}>Safety stop</div>
        <div style={{ fontSize: 18, margin: '8px 0 18px' }}>{message}</div>
        <div className="muted" style={{ fontSize: 13, marginBottom: 16 }}>All force is off. Hands back on the wheel, centre it, then acknowledge.</div>
        <div className="row" style={{ justifyContent: 'center' }}>
          <button className="btn primary" onClick={() => send('ackTrip')}>Acknowledge</button>
          {focus && <button className="btn" onClick={keepRunning}>Keep running in background (like iRacing)</button>}
        </div>
        {focus && (
          <div className="dim" style={{ fontSize: 12, marginTop: 14, lineHeight: 1.5 }}>
            Background mode ignores other windows taking focus. Hands-off, runaway, lock and watchdog stops still apply,
            and <b>Pause/Break</b> or a wheel button bound to STOP (Settings) stops the wheel from anywhere.
          </div>
        )}
      </div>
    </div>
  );
}

function EnduranceLine({ t }: { t: Tick | null }) {
  const s = t?.strength;
  if (!s) return null;
  const et = s.enduranceS;
  return (
    <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px solid var(--line)' }}>
      <div className="row" style={{ justifyContent: 'space-between', fontSize: 12 }}>
        <span className="muted">Like holding <b className="mono" style={{ color: 'var(--text)' }}>{s.frontRaiseKg.toFixed(1)} kg</b> at arm's length</span>
        <span className="dim">front raise</span>
      </div>
      <div className="row" style={{ justifyContent: 'space-between', fontSize: 12, marginTop: 6 }}>
        <span className="muted">Weakest link <b style={{ color: 'var(--text)' }}>{s.limitingLink}</b> · holdable ~<b className="mono" style={{ color: 'var(--text)' }}>{s.torqueNm < 0.5 || et >= 3600 ? '—' : s.limitingPct < 15 ? '15+ min' : mmss(et)}</b></span>
        <span className="mono" style={{ fontSize: 12 }}>{s.budgetUsedPct.toFixed(0)}% used</span>
      </div>
      <div className="bar" style={{ marginTop: 5 }}><i style={{ width: `${Math.min(100, s.budgetUsedPct)}%`, background: s.budgetUsedPct > 85 ? '#f43f5e' : s.budgetUsedPct > 60 ? '#fbbf24' : '#a78bfa' }} /></div>
    </div>
  );
}

function LiveMetrics({ s, t, rimMm }: { s: SessionSnap; t: Tick | null; rimMm: number }) {
  const live = s.live ?? {};
  const keys = Object.keys(live).filter((k) => METRIC_META[k] && !(k === 'lapS'));
  const power = t?.strength?.powerW ?? 0;
  return (
    <Card title="Live" className="tight">
      <div className="grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
        {keys.map((k) => (
          <div key={k} className="kpi">
            <span className="l">{METRIC_META[k].label}</span>
            <span className="v" style={{ fontSize: 20 }}>{live[k].toFixed(METRIC_META[k].d ?? 1)}<span className="u">{METRIC_META[k].unit}</span></span>
          </div>
        ))}
        <div className="kpi"><span className="l">Power</span><span className="v" style={{ fontSize: 20, color: power >= 0 ? '#22d3ee' : '#a78bfa' }}>{Math.abs(power).toFixed(0)}<span className="u">W {power >= 0 ? 'conc' : 'ecc'}</span></span></div>
        <div className="kpi"><span className="l">Speed</span><span className="v" style={{ fontSize: 20 }}>{Math.abs(t?.velocityDps ?? 0).toFixed(0)}<span className="u">°/s</span></span></div>
        <div className="kpi"><span className="l">Rim travel</span><span className="v" style={{ fontSize: 20 }}>{(Math.abs(t?.angleDeg ?? 0) * Math.PI / 180 * rimMm / 20).toFixed(1)}<span className="u">cm</span></span></div>
      </div>
    </Card>
  );
}

function IntensityBar({ s, t, rim, go }: { s: SessionSnap; t: Tick | null; rim: number; go: (p: string) => void }) {
  const intensity = t?.intensity ?? 1;
  const [drag, setDrag] = useState<number | null>(null);
  const timer = useRef<number | undefined>(undefined);
  const value = drag ?? intensity;
  const set = (v: number) => {
    setDrag(v);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => { send('setIntensity', { value: v }).finally(() => setDrag(null)); }, 60);
  };
  const perHand = rimKgf(s.loadNm, rim) / ((s.hand && s.hand !== 'both') ? 1 : 2);
  const presets: [string, number][] = [['Recovery', 0.7], ['Base', 1], ['Push', 1.15], ['Max', 1.3]];
  const capHit = s.loadNm >= (t?.globalCapNm ?? 99) - 0.05;
  return (
    <div className="intensity card tight" style={{ padding: '12px 16px' }}>
      <div style={{ minWidth: 150 }}>
        <div className="meter-label">Resistance</div>
        <div className="big-num" style={{ fontSize: 34 }}>{Math.round(value * 100)}<small>%</small></div>
      </div>
      <button className="btn" style={{ width: 52, height: 52, fontSize: 26 }} onClick={() => set(Math.max(0.1, +(value - 0.05).toFixed(2)))}>−</button>
      <div style={{ width: 300 }}>
        <input type="range" min={0.1} max={1.5} step={0.05} value={value} onChange={(e) => set(Number(e.target.value))} />
        <div className="row" style={{ marginTop: 8, gap: 6 }}>
          {presets.map(([n, v]) => <button key={n} className={`btn small ${Math.abs(value - v) < 0.01 ? 'primary' : ''}`} onClick={() => set(v)}>{n}</button>)}
        </div>
      </div>
      <button className="btn" style={{ width: 52, height: 52, fontSize: 26 }} onClick={() => set(Math.min(1.5, +(value + 0.05).toFixed(2)))}>+</button>
      <div className="col" style={{ gap: 2, minWidth: 220 }}>
        <span className="mono" style={{ fontSize: 20 }}>{s.loadNm.toFixed(1)} Nm <span className="dim" style={{ fontSize: 13 }}>→ {perHand.toFixed(1)} kgf/hand</span></span>
        <span className="dim" style={{ fontSize: 12 }}>{capHit ? <span style={{ color: '#fbbf24' }}>At safety cap ({(t?.globalCapNm ?? 0).toFixed(1)} Nm) — raise it in Settings</span> : <>on top of SimPro · <span className="kbd">↑</span> <span className="kbd">↓</span> or wheel buttons</>}</span>
      </div>
      <div className="spacer" />
      {s.phase === 'rest' && <button className="btn" onClick={() => send('next')}>Skip rest <span className="kbd">Enter</span></button>}
      <button className="btn ghost" onClick={() => { send('endSession'); go('home'); }}>End session</button>
      <button className="stop-btn" style={{ height: 56, fontSize: 24, padding: '0 28px' }} onClick={() => post('stop', { reason: 'STOP button' })}>STOP <span style={{ fontSize: 12, opacity: 0.8 }}>SPACE</span></button>
    </div>
  );
}

function NoSession({ go }: { go: (p: string) => void }) {
  const st = useAppState();
  const next = st?.program?.sessions.find((x) => x.key === st.program?.next);
  return (
    <div className="page fade-in">
      <h2 className="page-title">Cockpit</h2>
      <p className="page-sub">Nothing loaded. Start today's program session or pick a drill.</p>
      <div className="row" style={{ gap: 14 }}>
        {next && <button className="btn primary" style={{ height: 52 }} onClick={() => send('startProgramSession')}>Start {next.focus} (W{next.week} S{next.number})</button>}
        <button className="btn" style={{ height: 52 }} onClick={() => go('drills')}>Browse drills</button>
      </div>
      <div className="card" style={{ marginTop: 20, height: 520 }}>
        <div className="wheel-stage" style={{ height: '100%' }}><WheelGauge guided={false} /></div>
      </div>
    </div>
  );
}
