import { useEffect, useState } from 'react';
import { send, useAppState } from '../bridge';
import { DrillIcon } from '../components/Icons';
import { Card, Ring, Seg } from '../components/ui';
import { duration, relTime } from '../format';
import type { DrillConfig, DrillDef, History } from '../types';

export function defaultConfig(d: DrillDef): DrillConfig {
  return {
    drillId: d.id, loadPct: d.defaultLoadPct, sets: d.defaultSets, workS: d.defaultWorkS, restS: d.defaultRestS,
    hand: d.id === 'one-hand-hold' ? 'Right' : 'Both', direction: (d.direction as DrillConfig['direction']) ?? 'Alternate',
    params: Object.fromEntries(d.params.map((p) => [p.key, p.default])),
  };
}

export function startDrill(cfg: DrillConfig) {
  return send('startDrill', { config: cfg });
}

export function Home({ go }: { go: (p: any) => void }) {
  const st = useAppState()!;
  const [hist, setHist] = useState<History | null>(null);
  const [perWeek, setPerWeek] = useState(st.settings.sessionsPerWeek);
  useEffect(() => { send<History>('history').then(setHist).catch(() => {}); }, []);

  const program = st.program;
  const next = program?.sessions.find((s) => s.key === program.next);
  const bp = st.breakpoints;
  const bpR = bp['Both:right'], bpL = bp['Both:left'];
  const thisWeek = hist?.weekly.at(-1);
  const est = next ? next.items.reduce((a, i) => a + i.sets * (i.workS + i.restS) + 20, 0) : 0;
  const quick = ['corner-hold', 'lock-to-lock', 'stint', 'free-ride'].map((id) => st.drills.find((d) => d.id === id)!).filter(Boolean);

  return (
    <div className="page fade-in">
      <div className="grid" style={{ gridTemplateColumns: '1.6fr 1fr', gap: 16 }}>
        <div className="hero">
          <div className="stripe" />
          {next ? (
            <>
              <div className="head" style={{ color: 'var(--you)', fontSize: 15 }}>Today · Week {next.week} · Session {next.number}{next.deload ? ' · Deload' : ''}</div>
              <h2>{next.baseline ? 'Baseline strength test' : next.focus}</h2>
              <div className="muted" style={{ marginBottom: 16 }}>{next.items.length} blocks · about {Math.round(est / 60)} min · loads auto-scale to your breakpoint</div>
              <div className="row wrap" style={{ gap: 8, marginBottom: 20 }}>
                {next.items.map((i, k) => {
                  const d = st.drills.find((x) => x.id === i.drillId);
                  return (
                    <div key={k} className="row" style={{ gap: 8, padding: '8px 12px', borderRadius: 12, border: '1px solid var(--line-2)', background: 'rgba(0,0,0,0.25)' }}>
                      <span style={{ color: d?.accent }}><DrillIcon name={d?.icon ?? 'hold'} color={d?.accent} size={28} /></span>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: 13 }}>{i.name}</div>
                        <div className="dim" style={{ fontSize: 11 }}>{i.role} · {i.sets}×{duration(i.workS)}{i.role === 'Working' ? ` · ${i.loadPct.toFixed(0)}%` : ''}</div>
                      </div>
                    </div>
                  );
                })}
              </div>
              <div className="row">
                <button className="btn go" onClick={() => send('startProgramSession')}>Start session</button>
                <button className="btn ghost" onClick={() => go('program')}>View plan</button>
              </div>
            </>
          ) : (
            <>
              <div className="head" style={{ color: 'var(--you)', fontSize: 15 }}>Get started</div>
              <h2>Build your 8-week plan</h2>
              <p className="muted" style={{ maxWidth: 560, lineHeight: 1.5 }}>
                Holds, sprints, eccentrics, reaction and race stints, auto-scaled to your breakpoint. +5% when you nail a session,
                deload weeks 4 and 8 with a strength retest.
              </p>
              <div className="row" style={{ marginTop: 14 }}>
                <Seg value={perWeek} onChange={setPerWeek} options={[{ value: 2, label: '2 / week' }, { value: 3, label: '3 / week' }]} />
                <button className="btn go" onClick={() => send('generateProgram', { sessionsPerWeek: perWeek })}>Build my plan</button>
              </div>
            </>
          )}
        </div>

        <div className="grid" style={{ gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          <Card title="Breakpoint">
            {bpR || bpL ? (
              <div className="row" style={{ gap: 16 }}>
                <div className="kpi"><span className="l">Push right</span><span className="v">{bpR ? bpR.toFixed(1) : '—'}<span className="u">Nm</span></span></div>
                <div className="kpi"><span className="l">Push left</span><span className="v">{bpL ? bpL.toFixed(1) : '—'}<span className="u">Nm</span></span></div>
              </div>
            ) : (
              <div className="col" style={{ gap: 8 }}>
                <span className="muted" style={{ fontSize: 13 }}>No test yet. Everything scales from this.</span>
                <button className="btn small" onClick={() => { const d = st.drills.find((x) => x.id === 'breakpoint')!; startDrill(defaultConfig(d)); }}>Run breakpoint test</button>
              </div>
            )}
          </Card>
          <Card title="Plan">
            <div className="row" style={{ gap: 14 }}>
              <Ring value={program?.progress ?? 0} size={70} stroke={7} color="#a78bfa">
                <span className="mono" style={{ fontSize: 15, fontWeight: 700 }}>{Math.round((program?.progress ?? 0) * 100)}%</span>
              </Ring>
              <div className="muted" style={{ fontSize: 13 }}>{program ? `${program.completed.length}/${program.sessions.length} sessions` : 'No plan yet'}</div>
            </div>
          </Card>
          <Card title="This week">
            <div className="kpi"><span className="l">Impulse (load × time)</span><span className="v">{thisWeek ? thisWeek.impulseNms.toFixed(0) : 0}<span className="u">N·m·s</span></span></div>
            <div className="dim" style={{ fontSize: 12, marginTop: 4 }}>{thisWeek?.sessions ?? 0} sessions · {thisWeek?.sets ?? 0} sets</div>
          </Card>
          <Card title="Under tension">
            <div className="kpi"><span className="l">This week</span><span className="v">{thisWeek ? Math.round(thisWeek.tutS / 60) : 0}<span className="u">min</span></span></div>
            <div className="dim" style={{ fontSize: 12, marginTop: 4 }}>{hist?.sessions[0] ? `Last: ${relTime(hist.sessions[0].startedUtc)}` : 'No sessions yet'}</div>
          </Card>
        </div>
      </div>

      <h3 className="head" style={{ margin: '26px 0 12px', color: 'var(--text-2)', fontSize: 16, letterSpacing: '0.14em' }}>Quick start</h3>
      <div className="grid" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
        {quick.map((d) => (
          <div key={d.id} className="card drill-card" onClick={() => startDrill(defaultConfig(d))}>
            <div className="glow" style={{ background: d.accent }} />
            <div className="row">
              <span style={{ color: d.accent }}><DrillIcon name={d.icon} color={d.accent} size={46} /></span>
              <div>
                <div className="cat" style={{ color: d.accent }}>{d.category}</div>
                <div className="head" style={{ fontSize: 22, fontWeight: 700 }}>{d.name}</div>
              </div>
            </div>
            <p style={{ marginTop: 8, fontSize: 13 }}>{d.tagline}</p>
          </div>
        ))}
      </div>

      <div className="grid" style={{ gridTemplateColumns: '1.2fr 1fr', marginTop: 20 }}>
        <Card title="Why 12 Nm feels so heavy" accent="#fb923c">
          <p className="muted" style={{ lineHeight: 1.55, marginTop: 0 }}>
            12 Nm on a 300 mm rim is only ~8 kgf of rim force — but you don't push a rim like a barbell. That force sits on a
            ~0.45 m lever in front of your shoulder, goes through your small wrist muscles, and has to be held by skin friction.
            Per link, a static 12 Nm hold is roughly:
          </p>
          <div className="col" style={{ gap: 8 }}>
            {[['Lifting shoulder', 33, '≈ 2 min to failure'], ['Wrist (radial deviation)', 29, ''], ['Grip squeeze', 16, ''], ['If you resist by twisting your forearms', 75, 'near max!']].map(([l, v, n]) => (
              <div key={l as string}>
                <div className="row" style={{ justifyContent: 'space-between', fontSize: 13 }}><span>{l}</span><span className="mono">{v}% <span className="dim">{n}</span></span></div>
                <div className="bar"><i style={{ width: `${v}%`, background: (v as number) > 60 ? '#f43f5e' : (v as number) > 30 ? '#fbbf24' : '#34d399' }} /></div>
              </div>
            ))}
          </div>
          <p className="dim" style={{ fontSize: 12, marginBottom: 0 }}>Typical adult male norms; women's values are roughly 1.5–2× these percentages. Damper/inertia effects add more on top. Source: docs/research/03.</p>
        </Card>
        <Card title="How resistance is layered" accent="#22d3ee">
          <div className="col" style={{ gap: 10 }}>
            {[
              ['1', 'SimPro overall strength', 'Hardware cap. Use a dedicated training profile at 100% so RimForce can use the full motor.'],
              ['2', 'RimForce safety cap', `Global ceiling (now ${st.capNm.toFixed(1)} Nm of ${st.maxTorqueNm.toFixed(1)} Nm). Raise it gradually in Settings.`],
              ['3', 'Resistance %', 'Live master slider in the cockpit — like iRacing\'s strength slider. ↑/↓ or wheel buttons.'],
              ['4', 'Drill load', '% of YOUR breakpoint, so every drill scales as you get stronger.'],
            ].map(([n, t, d]) => (
              <div key={n} className="row" style={{ alignItems: 'flex-start', gap: 12 }}>
                <div className="mono" style={{ width: 28, height: 28, borderRadius: 8, display: 'grid', placeItems: 'center', background: '#13212f', border: '1px solid #23415a', color: 'var(--you)', flex: 'none' }}>{n}</div>
                <div><div style={{ fontWeight: 600 }}>{t}</div><div className="dim" style={{ fontSize: 13 }}>{d}</div></div>
              </div>
            ))}
            <div className="dim" style={{ fontSize: 12 }}>No software can exceed SimPro's cap — every DirectInput gain stops at 100%. Past the motor's limit, progress comes from one-hand work, weaker grips, longer holds and less rest (the program does this automatically).</div>
          </div>
        </Card>
      </div>
    </div>
  );
}
