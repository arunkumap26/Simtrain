import { useMemo, useState } from 'react';
import { useAppState } from '../bridge';
import { DrillIcon } from '../components/Icons';
import { Seg, Slider } from '../components/ui';
import { duration, rimKgf, TARGET_COLORS, TARGET_LABELS } from '../format';
import type { DrillConfig, DrillDef } from '../types';
import { defaultConfig, startDrill } from './Home';

const CATS = ['All', 'Isometric', 'Grip', 'Power', 'Eccentric', 'Reactive', 'Endurance', 'Test', 'Sandbox'];

export function Drills({ go }: { go: (p: any) => void }) {
  const st = useAppState()!;
  const [cat, setCat] = useState('All');
  const [open, setOpen] = useState<DrillDef | null>(null);
  const drills = st.drills.filter((d) => cat === 'All' || d.category === cat);
  return (
    <div className="page fade-in">
      <h2 className="page-title">Drills</h2>
      <p className="page-sub">13 resistance profiles built from how F1 drivers train — tap one to tune it and go.</p>
      <div style={{ marginBottom: 16 }}><Seg value={cat} onChange={setCat} options={CATS.map((c) => ({ value: c, label: c }))} /></div>
      <div className="drill-grid">
        {drills.map((d) => (
          <div key={d.id} className="card drill-card" onClick={() => setOpen(d)}>
            <div className="glow" style={{ background: d.accent }} />
            <div className="row" style={{ alignItems: 'flex-start' }}>
              <div className="grow">
                <div className="cat" style={{ color: d.accent }}>{d.category} · {d.resistanceProfile}</div>
                <h4>{d.name}</h4>
              </div>
              <span style={{ color: d.accent }}><DrillIcon name={d.icon} color={d.accent} size={54} /></span>
            </div>
            <p>{d.tagline}</p>
            <Targets t={d.targets} />
          </div>
        ))}
      </div>
      {open && <DrillDrawer d={open} onClose={() => setOpen(null)} onStart={(cfg) => { startDrill(cfg).then(() => go('train')); setOpen(null); }} />}
    </div>
  );
}

export function Targets({ t }: { t: Record<string, number> }) {
  return (
    <div className="targets">
      {Object.entries(t).filter(([, v]) => v > 0).map(([k, v]) => (
        <div key={k} className="target">
          {TARGET_LABELS[k] ?? k}
          <div className="bar"><i style={{ width: `${v * 100}%`, background: TARGET_COLORS[k] ?? '#94a3b8' }} /></div>
        </div>
      ))}
    </div>
  );
}

function DrillDrawer({ d, onClose, onStart }: { d: DrillDef; onClose: () => void; onStart: (c: DrillConfig) => void }) {
  const st = useAppState()!;
  const [cfg, setCfg] = useState<DrillConfig>(() => defaultConfig(d));
  const set = (p: Partial<DrillConfig>) => setCfg((c) => ({ ...c, ...p }));
  const setParam = (k: string, v: number) => setCfg((c) => ({ ...c, params: { ...c.params, [k]: v } }));

  const ref = useMemo(() => {
    const dirKey = cfg.direction === 'Left' ? 'left' : 'right';
    const handKey = cfg.hand === 'Both' ? 'Both' : cfg.hand;
    return st.breakpoints[`${handKey}:${dirKey}`] ?? st.breakpoints[`${handKey}:${dirKey === 'left' ? 'right' : 'left'}`] ?? null;
  }, [cfg.direction, cfg.hand, st.breakpoints]);
  const base = ref ?? st.maxTorqueNm;
  const loadNm = Math.min(st.maxTorqueNm * d.drillCapPct / 100, cfg.loadPct / 100 * base);
  const capped = loadNm > st.capNm;
  const hands = cfg.hand === 'Both' ? 2 : 1;
  const endless = d.endless;
  const minutes = cfg.params['minutes'];

  return (
    <>
      <div className="drawer-bg" onClick={onClose} />
      <div className="drawer">
        <div className="row" style={{ alignItems: 'flex-start' }}>
          <span style={{ color: d.accent }}><DrillIcon name={d.icon} color={d.accent} size={72} /></span>
          <div className="grow">
            <div className="cat head" style={{ color: d.accent, fontSize: 13, letterSpacing: '0.16em' }}>{d.category} · {d.resistanceProfile}</div>
            <div className="head" style={{ fontSize: 38, fontWeight: 800, lineHeight: 1 }}>{d.name}</div>
            <div className="muted" style={{ marginTop: 6 }}>{d.tagline}</div>
          </div>
          <button className="btn ghost small" onClick={onClose}>✕</button>
        </div>
        <p style={{ lineHeight: 1.55 }}>{d.description}</p>
        {d.why && <div className="card tight" style={{ borderColor: '#23415a', background: 'rgba(34,211,238,0.05)', fontSize: 13 }}><b style={{ color: 'var(--you)' }}>Why: </b>{d.why}</div>}
        <h3 className="head muted" style={{ fontSize: 14, margin: '18px 0 8px' }}>How to</h3>
        <ol className="howto">{d.howTo.map((h) => <li key={h}>{h}</li>)}</ol>
        <h3 className="head muted" style={{ fontSize: 14, margin: '18px 0 8px' }}>Trains</h3>
        <Targets t={d.targets} />

        <h3 className="head muted" style={{ fontSize: 14, margin: '22px 0 10px' }}>Setup</h3>
        <div className="col" style={{ gap: 14 }}>
          {!d.isTest && (
            <Slider label={ref ? 'Load (% of your breakpoint)' : 'Load (% of base max — run a breakpoint test!)'} value={cfg.loadPct} min={10} max={110} step={5} unit="%" onChange={(v) => set({ loadPct: v })} />
          )}
          <div className="card tight row" style={{ justifyContent: 'space-between' }}>
            <div className="kpi"><span className="l">Load</span><span className="v" style={{ color: capped ? '#fbbf24' : undefined }}>{Math.min(loadNm, st.capNm).toFixed(1)}<span className="u">Nm</span></span></div>
            <div className="kpi"><span className="l">Rim force</span><span className="v">{rimKgf(Math.min(loadNm, st.capNm), st.settings.wheel.rimDiameterMm).toFixed(1)}<span className="u">kgf</span></span></div>
            <div className="kpi"><span className="l">Per hand</span><span className="v">{(rimKgf(Math.min(loadNm, st.capNm), st.settings.wheel.rimDiameterMm) / hands).toFixed(1)}<span className="u">kgf</span></span></div>
          </div>
          {capped && <div style={{ color: '#fbbf24', fontSize: 13 }}>Limited by your safety cap ({st.capNm.toFixed(1)} Nm). Raise it in Settings when you're ready.</div>}
          {!endless && (
            <div className="grid" style={{ gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
              <Slider label="Sets" value={cfg.sets} min={1} max={10} step={1} onChange={(v) => set({ sets: v })} />
              <Slider label="Work" value={cfg.workS} min={10} max={d.isTest ? 240 : 120} step={5} fmt={duration} onChange={(v) => set({ workS: v })} />
              <Slider label="Rest" value={cfg.restS} min={0} max={180} step={15} fmt={duration} onChange={(v) => set({ restS: v })} />
            </div>
          )}
          {(d.usesHandChoice || d.id === 'one-hand-hold') && (
            <div className="field"><label>Hand</label>
              <Seg value={cfg.hand} onChange={(v) => set({ hand: v })} options={[...(d.id === 'one-hand-hold' ? [{ value: 'Both' as const, label: 'Alternate L/R' }] : [{ value: 'Both' as const, label: 'Both hands' }]), { value: 'Left' as const, label: 'Left @ 9' }, { value: 'Right' as const, label: 'Right @ 3' }]} />
            </div>
          )}
          {d.usesDirection && (
            <div className="field"><label>You push</label>
              <Seg value={cfg.direction} onChange={(v) => set({ direction: v })} options={[{ value: 'Alternate' as const, label: 'Alternate' }, { value: 'Right' as const, label: 'Right (clockwise)' }, { value: 'Left' as const, label: 'Left' }]} />
            </div>
          )}
          {d.params.map((p) => p.options?.length ? (
            <div key={p.key} className="field"><label>{p.label}</label>
              <Seg value={String(cfg.params[p.key] ?? p.default)} onChange={(v) => setParam(p.key, Number(v))} options={p.options.map((o) => ({ value: o.value, label: o.label }))} />
            </div>
          ) : (
            <Slider key={p.key} label={p.label} value={cfg.params[p.key] ?? p.default} min={p.min} max={p.max} step={p.step} unit={p.unit} hint={p.help} onChange={(v) => setParam(p.key, v)} />
          ))}
          <div className="row" style={{ marginTop: 8 }}>
            <button className="btn go" onClick={() => onStart(endless ? { ...cfg, sets: 1, workS: (minutes ?? 5) * 60 } : cfg)}>Load drill</button>
            <span className="dim" style={{ fontSize: 12 }}>Nothing moves until you press <b>Arm &amp; start</b> in the cockpit.</span>
          </div>
        </div>
      </div>
    </>
  );
}
