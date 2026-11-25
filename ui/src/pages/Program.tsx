import { Fragment, useState } from 'react';
import { send, useAppState } from '../bridge';
import { DrillIcon } from '../components/Icons';
import { Card, Seg } from '../components/ui';
import { duration } from '../format';
import type { ProgramSessionView } from '../types';

const TPL_COLOR: Record<string, string> = { A: '#22d3ee', B: '#fb923c', C: '#fbbf24' };

export function ProgramPage({ go }: { go: (p: any) => void }) {
  const st = useAppState()!;
  const p = st.program;
  const [perWeek, setPerWeek] = useState(st.settings.sessionsPerWeek);
  const [sel, setSel] = useState<ProgramSessionView | null>(null);
  const current = sel ?? p?.sessions.find((s) => s.key === p.next) ?? null;

  if (!p) {
    return (
      <div className="page fade-in">
        <h2 className="page-title">Program</h2>
        <p className="page-sub">An 8-week plan: warm-up at 25% of your breakpoint, 3–5 drills, cool-down. +5% when you nail it; deloads with retests in weeks 4 and 8.</p>
        <div className="row"><Seg value={perWeek} onChange={setPerWeek} options={[{ value: 2, label: '2 sessions / week' }, { value: 3, label: '3 sessions / week' }]} />
          <button className="btn go" onClick={() => send('generateProgram', { sessionsPerWeek: perWeek })}>Build my plan</button></div>
      </div>
    );
  }

  const weeks = Array.from({ length: p.weeks }, (_, i) => i + 1);
  return (
    <div className="page fade-in">
      <div className="row" style={{ alignItems: 'flex-end' }}>
        <div className="grow">
          <h2 className="page-title">8-week program</h2>
          <p className="page-sub">A · Hold strength &nbsp; B · Power &amp; reaction &nbsp; C · Grip &amp; endurance — {p.completed.length}/{p.sessions.length} done</p>
        </div>
        <Seg value={perWeek} onChange={setPerWeek} options={[{ value: 2, label: '2 / wk' }, { value: 3, label: '3 / wk' }]} />
        <button className="btn" onClick={() => { if (confirm('Regenerate the plan? Progress on the current plan is kept in history but the plan restarts.')) send('generateProgram', { sessionsPerWeek: perWeek }); }}>Regenerate</button>
      </div>

      <div className="grid" style={{ gridTemplateColumns: '1.5fr 1fr', gap: 16, alignItems: 'start' }}>
        <Card>
          <div className="week-grid" style={{ gridTemplateColumns: `90px repeat(${p.sessionsPerWeek}, 1fr)` }}>
            {weeks.map((w) => (
              <Fragment key={w}>
                <div className="col" style={{ justifyContent: 'center', gap: 2 }}>
                  <div className="head" style={{ fontSize: 20, fontWeight: 700 }}>Week {w}</div>
                  {w % 4 === 0 && <span className="tag" style={{ color: '#fbbf24', alignSelf: 'flex-start' }}>deload</span>}
                </div>
                {p.sessions.filter((s) => s.week === w).map((s) => (
                  <div key={s.key} className={`sess ${s.key === p.next ? 'next' : ''} ${s.done ? 'done' : ''} ${s.deload ? 'deload' : ''}`} onClick={() => setSel(s)}
                    style={{ outline: current?.key === s.key ? '2px solid rgba(255,255,255,0.25)' : undefined }}>
                    <div className="row" style={{ justifyContent: 'space-between' }}>
                      <span className="head" style={{ color: TPL_COLOR[s.template], fontSize: 13, letterSpacing: '0.12em' }}>{s.baseline ? 'TEST' : s.template} · S{s.number}</span>
                      {s.done ? <span style={{ color: '#34d399' }}>✓</span> : s.key === p.next ? <span className="tag" style={{ color: 'var(--you)' }}>next</span> : null}
                    </div>
                    <div style={{ fontWeight: 600, marginTop: 6 }}>{s.baseline ? 'Baseline test' : s.focus.replace(' · deload', '')}</div>
                    <div className="row" style={{ gap: 3, marginTop: 8 }}>
                      {s.items.filter((i) => i.role !== 'Warm-up' && i.role !== 'Cool-down').map((i, k) => {
                        const d = st.drills.find((x) => x.id === i.drillId);
                        return <span key={k} title={i.name} style={{ color: d?.accent }}><DrillIcon name={d?.icon ?? 'hold'} color={d?.accent} size={20} /></span>;
                      })}
                      {s.retest && <span className="tag" style={{ marginLeft: 'auto', color: '#f43f5e' }}>retest</span>}
                    </div>
                  </div>
                ))}
              </Fragment>
            ))}
          </div>
        </Card>

        <div className="col" style={{ gap: 14 }}>
          {current && (
            <Card title={`Week ${current.week} · Session ${current.number}`} accent={TPL_COLOR[current.template]}>
              <div className="head" style={{ fontSize: 26, fontWeight: 700 }}>{current.baseline ? 'Baseline strength test' : current.focus}</div>
              <table className="tbl" style={{ marginTop: 10 }}>
                <thead><tr><th>Block</th><th>Role</th><th>Sets</th><th>Load</th></tr></thead>
                <tbody>
                  {current.items.map((i, k) => (
                    <tr key={k}><td>{i.name}</td><td className="dim">{i.role}</td><td className="mono">{i.sets}×{duration(i.workS)}</td><td className="mono">{i.role === 'Test' ? 'ramp' : `${i.loadPct.toFixed(0)}%`}</td></tr>
                  ))}
                </tbody>
              </table>
              {!current.done && (
                <button className="btn go" style={{ marginTop: 14, width: '100%' }} onClick={() => send('startProgramSession', { key: current.key }).then(() => go('train'))}>Start this session</button>
              )}
            </Card>
          )}
          <Card title="Working loads (% of breakpoint)">
            <div className="col" style={{ gap: 7 }}>
              {Object.entries(p.workingLoadPct).filter(([id]) => id !== 'free-ride').map(([id, v]) => {
                const d = st.drills.find((x) => x.id === id);
                const m = p.workMultiplier[id];
                return (
                  <div key={id}>
                    <div className="row" style={{ justifyContent: 'space-between', fontSize: 13 }}><span>{d?.name ?? id}</span><span className="mono">{v.toFixed(0)}%{m && m > 1 ? ` · time ×${m.toFixed(2)}` : ''}</span></div>
                    <div className="bar"><i style={{ width: `${v}%`, background: d?.accent }} /></div>
                  </div>
                );
              })}
            </div>
          </Card>
          {p.log.length > 0 && (
            <Card title="Progression log">
              <div className="col mono" style={{ gap: 4, fontSize: 12 }}>{p.log.slice().reverse().map((l, i) => <div key={i} className="muted">{l}</div>)}</div>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
