import { useEffect, useState } from 'react';
import { send, useAppState } from '../bridge';
import { BarChart, LineChart, TraceChart } from '../components/Charts';
import { Card, Empty } from '../components/ui';
import { relTime } from '../format';
import type { History } from '../types';
import { METRIC_META } from './Train';

const SERIES: { hand: string; dir: number; name: string; color: string }[] = [
  { hand: 'Both', dir: 1, name: 'Two hands · push right', color: '#22d3ee' },
  { hand: 'Both', dir: -1, name: 'Two hands · push left', color: '#a78bfa' },
  { hand: 'Right', dir: 1, name: 'Right hand · right', color: '#34d399' },
  { hand: 'Right', dir: -1, name: 'Right hand · left', color: '#86efac' },
  { hand: 'Left', dir: 1, name: 'Left hand · right', color: '#fb923c' },
  { hand: 'Left', dir: -1, name: 'Left hand · left', color: '#fbbf24' },
];

export function Progress() {
  const st = useAppState()!;
  const [h, setH] = useState<History | null>(null);
  const [setId, setSetId] = useState<number | null>(null);
  const [trace, setTrace] = useState<number[]>([]);
  const [exported, setExported] = useState<string | null>(null);

  useEffect(() => { send<History>('history').then(setH).catch(() => {}); }, []);
  useEffect(() => { if (setId != null) send<{ trace: number[] }>('trace', { setId }).then((r) => setTrace(r.trace)).catch(() => {}); }, [setId]);

  if (!h) return <div className="page"><Empty>Loading…</Empty></div>;
  const t0 = h.breakpoints.length ? new Date(h.breakpoints[0].measuredUtc).getTime() : Date.now();
  const series = SERIES.map((s) => ({
    name: s.name, color: s.color,
    points: h.breakpoints.filter((b) => b.hand === s.hand && b.dir === s.dir).map((b) => ({ x: (new Date(b.measuredUtc).getTime() - t0) / 86400000, y: b.torqueNm, hollow: b.estimated, label: `${b.torqueNm.toFixed(2)} Nm${b.estimated ? ' (estimated)' : ''}` })),
  })).filter((s) => s.points.length);
  const sel = h.sets.find((x) => x.id === setId);
  const sumImpulse = h.weekly.reduce((a, w) => a + w.impulseNms, 0);

  return (
    <div className="page fade-in">
      <div className="row" style={{ alignItems: 'flex-end' }}>
        <div className="grow">
          <h2 className="page-title">Progress</h2>
          <p className="page-sub">Breakpoint strength per hand and direction, weekly volume and every set you've done.</p>
        </div>
        <button className="btn" onClick={() => send<{ folder: string }>('exportCsv').then((r) => setExported(r.folder))}>Export CSV</button>
      </div>
      {exported && <div className="card tight" style={{ marginBottom: 14, fontSize: 13 }}>Exported to <span className="mono">{exported}</span></div>}

      <div className="grid" style={{ gridTemplateColumns: '1.3fr 1fr', gap: 16 }}>
        <Card title="Breakpoint (Nm)" right={<span className="dim" style={{ fontSize: 12 }}>hollow = estimated from hold-to-failure</span>}>
          {series.length ? <LineChart series={series} yLabel="Nm" xFmt={(x) => `day ${x.toFixed(0)}`} /> : <Empty>Run a Breakpoint Test to start your strength curve.</Empty>}
          <div className="row wrap" style={{ gap: 12, marginTop: 6 }}>
            {series.map((s) => <span key={s.name} className="row" style={{ gap: 6, fontSize: 12 }}><i style={{ width: 10, height: 10, borderRadius: 3, background: s.color }} />{s.name}</span>)}
          </div>
        </Card>
        <Card title="Weekly volume · impulse (N·m·s)" right={<span className="mono dim">{sumImpulse.toFixed(0)} total</span>}>
          <BarChart data={h.weekly.map((w) => ({ label: w.week.slice(5), value: w.impulseNms }))} unit="N·m·s" />
          <div className="grid" style={{ gridTemplateColumns: 'repeat(3,1fr)', marginTop: 6 }}>
            <div className="kpi"><span className="l">Sessions</span><span className="v" style={{ fontSize: 20 }}>{h.sessions.length}</span></div>
            <div className="kpi"><span className="l">Sets</span><span className="v" style={{ fontSize: 20 }}>{h.sets.length}</span></div>
            <div className="kpi"><span className="l">Under tension</span><span className="v" style={{ fontSize: 20 }}>{Math.round(h.weekly.reduce((a, w) => a + w.tutS, 0) / 60)}<span className="u">min</span></span></div>
          </div>
        </Card>
      </div>

      <div className="grid" style={{ gridTemplateColumns: '1.3fr 1fr', gap: 16, marginTop: 16, alignItems: 'start' }}>
        <Card title="Recent sets">
          {h.sets.length === 0 ? <Empty>No sets yet — go train!</Empty> : (
            <div style={{ maxHeight: 420, overflow: 'auto' }}>
              <table className="tbl">
                <thead><tr><th>When</th><th>Drill</th><th>Hand / dir</th><th>Load</th><th>Score</th><th></th></tr></thead>
                <tbody>
                  {h.sets.slice().reverse().slice(0, 80).map((x) => (
                    <tr key={x.id} className="clickable" onClick={() => setSetId(x.id)} style={{ background: x.id === setId ? 'rgba(34,211,238,0.06)' : undefined }}>
                      <td className="dim">{relTime(x.startedUtc)}</td>
                      <td>{x.drillName} <span className="dim">{x.role !== 'Working' ? `· ${x.role}` : ''}</span></td>
                      <td className="dim">{x.hand} / {x.direction}</td>
                      <td className="mono">{x.loadNm.toFixed(1)} Nm</td>
                      <td className="mono">{x.score.toFixed(x.scoreLabel === 'Nm' ? 2 : 0)} <span className="dim">{x.scoreLabel}</span></td>
                      <td>{x.completed ? <span style={{ color: '#34d399' }}>✓</span> : <span style={{ color: '#fb7185' }}>✕</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
        <Card title={sel ? `${sel.drillName} · trace` : 'Set trace'}>
          {sel ? (
            <>
              <TraceChart trace={trace} />
              <div className="grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)', gap: 8, marginTop: 8 }}>
                {Object.entries(sel.metrics).filter(([k]) => METRIC_META[k] || ['workConcJ', 'workEccJ', 'avgNm', 'sdDeg', 'maxDevDeg'].includes(k)).slice(0, 9).map(([k, v]) => (
                  <div key={k} style={{ fontSize: 12 }}><div className="dim">{METRIC_META[k]?.label ?? k}</div><div className="mono">{v.toFixed(METRIC_META[k]?.d ?? 1)} {METRIC_META[k]?.unit ?? ''}</div></div>
                ))}
              </div>
            </>
          ) : <Empty>Pick a set to see angle (cyan), target (dashed) and torque (orange).</Empty>}
        </Card>
      </div>
      {!st.session && null}
    </div>
  );
}
