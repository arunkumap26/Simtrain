import { useEffect, useMemo, useState } from 'react';
import { send, useAppState } from '../bridge';
import { Card, Empty, Toggle } from '../components/ui';
import { G, relTime } from '../format';
import type { History } from '../types';

type SetRow = History['sets'][number];

/** Typical shoulder MVC (Nm) used for "% of typical" — docs/research/03 §3.4. */
const NORMS = { men: { flex: 77, ext: 91.9 }, women: { flex: 40, ext: 43.5 } };
const REACH_M = 0.45;
const ARM_MASS = 0.049, ARM_COM_M = 0.2;

const CONF = ['', 'low', 'medium', 'high'];
const CONF_COLOR = ['', '#fb923c', '#fbbf24', '#34d399'];

interface ArmResult { key: string; title: string; motion: 'lift' | 'press'; set?: SetRow }

export function MaxTest({ go }: { go: (p: any) => void }) {
  const st = useAppState()!;
  const [hist, setHist] = useState<History | null>(null);
  const items = (st as any).maxTest?.items as { id: string; drill: string; hand: string; dir: string; label: string }[] | undefined;
  const [battery, setBattery] = useState<string[]>(['profile-right-lift', 'profile-left-lift', 'profile-right-press', 'profile-left-press']);
  const [fullPower, setFullPower] = useState(true);
  const [braced, setBraced] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  useEffect(() => { send<History>('history').then(setHist).catch(() => {}); }, [st.session]);

  const rimR = st.settings.wheel.rimDiameterMm / 2000;
  const bm = st.settings.bodyMassKg ?? (st.settings.norms === 'women' ? 65 : 80);
  const norms = NORMS[st.settings.norms === 'women' ? 'women' : 'men'];
  const armWeightNm = ARM_MASS * bm * G * ARM_COM_M;
  const deviceMax = st.settings.wheel.baseMaxTorqueNm * st.settings.wheel.simProStrengthPct / 100 * st.settings.wheel.torqueCalibration;

  // latest max-test session
  const latest = useMemo(() => {
    if (!hist) return null;
    const s = hist.sessions.find((x) => x.programRef === 'maxtest' && x.sets > 0);
    if (!s) return null;
    return { session: s, sets: hist.sets.filter((x) => x.sessionId === s.id) };
  }, [hist]);
  const pick = (hand: string, dir: string, drill = 'speed-profile') =>
    latest?.sets.filter((x) => x.drillId === drill && x.hand === hand && x.direction === dir).at(-1);

  const arms: ArmResult[] = [
    { key: 'rl', title: 'Right arm · lift', motion: 'lift', set: pick('Right', 'Left') },
    { key: 'll', title: 'Left arm · lift', motion: 'lift', set: pick('Left', 'Right') },
    { key: 'rp', title: 'Right arm · press', motion: 'press', set: pick('Right', 'Right') },
    { key: 'lp', title: 'Left arm · press', motion: 'press', set: pick('Left', 'Left') },
  ];
  const t0 = (s?: SetRow) => (s && s.metrics.valid > 0 ? s.metrics.t0Nm : undefined);
  const turnRight = t0(arms[2].set) && t0(arms[1].set) ? t0(arms[2].set)! + t0(arms[1].set)! : undefined;
  const turnLeft = t0(arms[3].set) && t0(arms[0].set) ? t0(arms[3].set)! + t0(arms[0].set)! : undefined;
  const twoHandR = pick('Both', 'Right'), twoHandL = pick('Both', 'Left');
  const endurance = latest?.sets.filter((x) => x.drillId === 'breakpoint').at(-1);
  const ladders = latest?.sets.filter((x) => x.drillId === 'max-push') ?? [];

  const start = () => {
    setErr(null);
    send('startMaxTest', { battery, fullPower, confirmed: braced }).then(() => go('train')).catch((e) => setErr(e.message));
  };

  return (
    <div className="page fade-in">
      <h2 className="page-title">Max strength</h2>
      <p className="page-sub">Find your real maximum turning strength — per shoulder, per direction — even beyond the {deviceMax.toFixed(0)} Nm your wheel can produce.</p>
      <div className="grid" style={{ gridTemplateColumns: '420px 1fr', gap: 16, alignItems: 'start' }}>
        <div className="col" style={{ gap: 16 }}>
          <Card title="How it works" accent="#f43f5e">
            <ol className="howto">
              <li><span>The wheel loads up and pulls back. You <b>brace</b>, then on <b>GO</b> turn to the marker as fast as you can.</span></li>
              <li><span>Five loads up to the motor's max, two reps each. Heavier load → slower turn.</span></li>
              <li><span>Speed vs. torque falls in a near-straight line; extending it to zero speed estimates your <b>maximum torque</b> — even past what the motor can make.</span></li>
              <li><span>One arm at a time keeps you inside the range the motor can measure well, and isolates each shoulder: <b>lift</b> = shoulder flexion, <b>press</b> = extension.</span></li>
            </ol>
          </Card>
          <Card title="Set up the test">
            <div className="col" style={{ gap: 8 }}>
              {(items ?? []).map((it) => {
                const on = battery.includes(it.id);
                return (
                  <div key={it.id} className={`check ${on ? 'on' : ''}`} style={{ padding: 9 }} onClick={() => setBattery(on ? battery.filter((b) => b !== it.id) : [...battery, it.id])}>
                    <div className="box">{on ? '✓' : ''}</div>
                    <div style={{ fontSize: 13 }}>{it.label}{it.id.startsWith('profile-') && (it.hand === 'Right' || it.hand === 'Left') ? <span className="tag" style={{ marginLeft: 6 }}>recommended</span> : null}</div>
                  </div>
                );
              })}
            </div>
            <div style={{ marginTop: 14 }}>
              <Toggle on={fullPower} onChange={setFullPower} label={`Full power for this test (${deviceMax.toFixed(1)} Nm)`}
                hint={`Your normal safety cap is ${(deviceMax * st.settings.safety.globalCapPct / 100).toFixed(1)} Nm. Full power lasts only until this test ends; every other safety stop stays on.`} />
            </div>
            {fullPower && (
              <div className={`check ${braced ? 'on' : ''}`} style={{ marginTop: 10 }} onClick={() => setBraced(!braced)}>
                <div className="box">{braced ? '✓' : ''}</div>
                <div style={{ fontSize: 13 }}>Rig bolted down, thumbs outside the spokes, SimPro RimForce profile at 100%. I'll stop with <b>Space</b> / <b>Pause</b> if anything feels wrong.</div>
              </div>
            )}
            {st.settings.wheel.simProStrengthPct < 100 && <div style={{ color: '#fbbf24', fontSize: 12, marginTop: 8 }}>SimPro strength is set to {st.settings.wheel.simProStrengthPct}% in Settings — the motor can only reach {deviceMax.toFixed(1)} Nm.</div>}
            {err && <div style={{ color: '#fb7185', marginTop: 10 }}>{err}</div>}
            <button className="btn go" style={{ width: '100%', marginTop: 14 }} disabled={battery.length === 0 || (fullPower && !braced)} onClick={start}>
              Start max test · ~{Math.round(battery.length * 3.5 + 1)} min
            </button>
            <div className="dim" style={{ fontSize: 12, marginTop: 8 }}>Warm up first (included). Every rep is all-out; rest is built in.</div>
          </Card>
        </div>

        <div className="col" style={{ gap: 16 }}>
          {!latest ? (
            <Card title="Your strength profile"><Empty>Run the test to see your max turning strength per shoulder.</Empty></Card>
          ) : (
            <>
              <div className="hero">
                <div className="stripe" />
                <div className="head" style={{ color: 'var(--you)', fontSize: 15 }}>Latest test · {relTime(latest.session.startedUtc)}</div>
                <div className="row wrap" style={{ gap: 36, marginTop: 8 }}>
                  <BigEstimate label="Turning right (R press + L lift)" nm={turnRight ?? t0(twoHandR)} deviceMax={deviceMax} rimR={rimR} />
                  <BigEstimate label="Turning left (L press + R lift)" nm={turnLeft ?? t0(twoHandL)} deviceMax={deviceMax} rimR={rimR} />
                </div>
                <div className="dim" style={{ fontSize: 12, marginTop: 10 }}>Two-handed max ≈ the sum of the two arms (real two-handed efforts are usually 5–10% lower — the "bilateral deficit").</div>
              </div>

              <div className="grid" style={{ gridTemplateColumns: '1fr 1fr', gap: 14 }}>
                {arms.map((a) => <ArmCard key={a.key} a={a} rimR={rimR} armWeightNm={armWeightNm} norm={a.motion === 'lift' ? norms.flex : norms.ext} deviceMax={deviceMax} />)}
              </div>

              {(twoHandR || twoHandL) && (
                <div className="grid" style={{ gridTemplateColumns: '1fr 1fr', gap: 14 }}>
                  {[['Two hands · right', twoHandR], ['Two hands · left', twoHandL]].map(([t, s]) => s && (
                    <Card key={t as string} title={t as string}><ProfileDetail s={s as SetRow} deviceMax={deviceMax} /></Card>
                  ))}
                </div>
              )}

              {(endurance || ladders.length > 0) && (
                <Card title="Other results">
                  <div className="col" style={{ gap: 8, fontSize: 13 }}>
                    {endurance && (
                      <div>Hold at full power: <b className="mono">{endurance.metrics.lowerBound > 0 ? '≥ ' : ''}{endurance.score.toFixed(1)} Nm</b>
                        <span className="dim"> {endurance.metrics.estimated > 0 ? `(estimated from how long you held ${endurance.metrics.capNm?.toFixed(1)} Nm — Rohmert endurance curve)` : '(measured)'}</span></div>
                    )}
                    {ladders.map((l) => (
                      <div key={l.id}>Push-through ladder · {l.direction}: <b className="mono">{l.metrics.capped > 0 ? '≥ ' : ''}{l.score.toFixed(1)} Nm</b>
                        <span className="dim"> {l.metrics.capped > 0 ? '(the motor ran out first)' : `(${l.metrics.rungs} rungs)`}</span></div>
                    ))}
                  </div>
                </Card>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function BigEstimate({ label, nm, deviceMax, rimR }: { label: string; nm?: number; deviceMax: number; rimR: number }) {
  return (
    <div>
      <div className="meter-label">{label}</div>
      <div className="big-num" style={{ fontSize: 56 }}>{nm ? nm.toFixed(1) : '—'}<small>Nm</small></div>
      {nm ? <div className="muted" style={{ fontSize: 13 }}>{(nm / rimR / G).toFixed(1)} kgf at the rim · <b style={{ color: 'var(--text)' }}>{(nm / deviceMax).toFixed(1)}×</b> your wheel's max</div> : <div className="dim" style={{ fontSize: 13 }}>needs both arms tested</div>}
    </div>
  );
}

function ArmCard({ a, rimR, armWeightNm, norm, deviceMax }: { a: ArmResult; rimR: number; armWeightNm: number; norm: number; deviceMax: number }) {
  const s = a.set;
  const valid = !!s && s.metrics.valid > 0;
  const t0 = valid ? s!.metrics.t0Nm : 0;
  const handN = t0 / rimR;
  const shoulderNm = handN * REACH_M + (a.motion === 'lift' ? armWeightNm : -armWeightNm);
  const conf = valid ? s!.metrics.confidence : 0;
  return (
    <Card title={a.title} right={valid ? <span className="tag" style={{ color: CONF_COLOR[conf] }}>{CONF[conf]} confidence</span> : null}>
      {!s ? <div className="dim">Not tested</div> : !valid ? (
        <div className="dim" style={{ fontSize: 13 }}>
          Not enough clean reps to fit a line{s.metrics.couldNotHoldNm ? ` — couldn't hold ${s.metrics.couldNotHoldNm.toFixed(1)} Nm, so this arm's max is below that` : ''}.
        </div>
      ) : (
        <>
          <div className="row" style={{ gap: 22, alignItems: 'flex-end' }}>
            <div className="kpi"><span className="l">Max torque</span><span className="v" style={{ fontSize: 30 }}>{t0.toFixed(1)}<span className="u">Nm</span></span></div>
            <div className="kpi"><span className="l">Hand force</span><span className="v" style={{ fontSize: 20 }}>{(handN / G).toFixed(1)}<span className="u">kgf</span></span></div>
            <div className="kpi"><span className="l">Shoulder</span><span className="v" style={{ fontSize: 20 }}>{shoulderNm.toFixed(0)}<span className="u">Nm · {(shoulderNm / norm * 100).toFixed(0)}%</span></span></div>
          </div>
          <FvChart s={s!} deviceMax={deviceMax} />
          <div className="row" style={{ gap: 16, fontSize: 12 }}>
            <span className="muted">Peak power <b className="mono" style={{ color: 'var(--text)' }}>{s!.metrics.pmaxW?.toFixed(0)} W</b></span>
            <span className="muted">Top speed <b className="mono" style={{ color: 'var(--text)' }}>{s!.metrics.v0Dps?.toFixed(0)}°/s</b></span>
            <span className="muted">Fit R² <b className="mono" style={{ color: 'var(--text)' }}>{s!.metrics.r2?.toFixed(2)}</b></span>
          </div>
          {conf < 2 && <div style={{ color: '#fb923c', fontSize: 12, marginTop: 6 }}>Long extrapolation ({s!.metrics.extrapolation?.toFixed(1)}× the hardest rep) — treat as a rough figure.</div>}
        </>
      )}
    </Card>
  );
}

function ProfileDetail({ s, deviceMax }: { s: SetRow; deviceMax: number }) {
  const valid = s.metrics.valid > 0;
  return valid ? (
    <>
      <div className="kpi"><span className="l">Estimated max</span><span className="v">{s.metrics.t0Nm.toFixed(1)}<span className="u">Nm · {CONF[s.metrics.confidence]} confidence</span></span></div>
      <FvChart s={s} deviceMax={deviceMax} />
    </>
  ) : <div className="dim">Not enough clean reps.</div>;
}

/** Torque–velocity points, the fitted line extended to zero speed, and the motor's limit. */
function FvChart({ s, deviceMax }: { s: SetRow; deviceMax: number }) {
  const W = 420, H = 150, L = 34, R = 8, T = 10, B = 22;
  const pts: { v: number; t: number }[] = [];
  for (let i = 1; i <= 8; i++) if (s.metrics[`p${i}Nm`] != null) pts.push({ v: s.metrics[`p${i}Dps`], t: s.metrics[`p${i}Nm`] });
  const t0 = s.metrics.t0Nm, v0 = s.metrics.v0Dps;
  const vMax = Math.max(v0 || 0, ...pts.map((p) => p.v)) * 1.05 || 1;
  const tMax = Math.max(t0 || 0, deviceMax, ...pts.map((p) => p.t)) * 1.1;
  const X = (v: number) => L + (v / vMax) * (W - L - R);
  const Y = (t: number) => T + (1 - t / tMax) * (H - T - B);
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height: H, marginTop: 8 }}>
      {[0, 0.5, 1].map((f) => <line key={f} x1={L} x2={W - R} y1={Y(tMax * f)} y2={Y(tMax * f)} stroke="rgba(148,163,184,0.1)" />)}
      <line x1={L} x2={W - R} y1={Y(deviceMax)} y2={Y(deviceMax)} stroke="#f43f5e" strokeDasharray="5 4" />
      <text x={W - R} y={Y(deviceMax) - 4} fill="#f43f5e" fontSize="10" textAnchor="end" fontFamily="Barlow Condensed">WHEEL MAX {deviceMax.toFixed(0)} Nm</text>
      {t0 && v0 && <line x1={X(0)} y1={Y(t0)} x2={X(v0)} y2={Y(0)} stroke="#a78bfa" strokeWidth="2" strokeDasharray="7 5" />}
      {t0 && <circle cx={X(0)} cy={Y(t0)} r="6" fill="#a78bfa" style={{ filter: 'drop-shadow(0 0 6px #a78bfa)' }} />}
      {t0 && <text x={X(0) + 10} y={Y(t0) + 4} fill="#e9d5ff" fontSize="12" fontFamily="JetBrains Mono">{t0.toFixed(1)} Nm</text>}
      {pts.map((p, i) => <circle key={i} cx={X(p.v)} cy={Y(p.t)} r="4.5" fill="#22d3ee" />)}
      <text x={L} y={H - 6} fill="#64748b" fontSize="10" fontFamily="JetBrains Mono">0°/s</text>
      <text x={W - R} y={H - 6} fill="#64748b" fontSize="10" textAnchor="end" fontFamily="JetBrains Mono">{vMax.toFixed(0)}°/s</text>
      <text x={4} y={T + 8} fill="#64748b" fontSize="10" fontFamily="Barlow Condensed">Nm</text>
    </svg>
  );
}
