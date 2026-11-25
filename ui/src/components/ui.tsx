import type { ReactNode } from 'react';

export function Card({ title, children, className = '', style, accent, right }: { title?: ReactNode; children: ReactNode; className?: string; style?: React.CSSProperties; accent?: string; right?: ReactNode }) {
  return (
    <div className={`card ${className}`} style={style}>
      {accent && <div className="accent-bar" style={{ background: accent }} />}
      {(title || right) && (
        <div className="row" style={{ justifyContent: 'space-between', marginBottom: 10 }}>
          {title && <h3 style={{ margin: 0 }}>{title}</h3>}
          {right}
        </div>
      )}
      {children}
    </div>
  );
}

export function Seg<T extends string | number>({ value, options, onChange }: { value: T; options: { value: T; label: ReactNode }[]; onChange: (v: T) => void }) {
  return (
    <div className="seg">
      {options.map((o) => (
        <button key={String(o.value)} className={o.value === value ? 'on' : ''} onClick={() => onChange(o.value)}>{o.label}</button>
      ))}
    </div>
  );
}

export function Slider({ label, value, min, max, step, unit, onChange, fmt, hint }: { label: ReactNode; value: number; min: number; max: number; step: number; unit?: string | null; onChange: (v: number) => void; fmt?: (v: number) => string; hint?: ReactNode }) {
  return (
    <div className="field">
      <label><span>{label}</span><b>{fmt ? fmt(value) : `${value}${unit ? ` ${unit}` : ''}`}</b></label>
      <input type="range" min={min} max={max} step={step} value={value} onChange={(e) => onChange(Number(e.target.value))} />
      {hint && <div className="dim" style={{ fontSize: 12 }}>{hint}</div>}
    </div>
  );
}

export function Toggle({ on, onChange, label, hint }: { on: boolean; onChange: (v: boolean) => void; label: ReactNode; hint?: ReactNode }) {
  return (
    <div className="row" style={{ justifyContent: 'space-between', alignItems: 'flex-start', gap: 16 }}>
      <div>
        <div>{label}</div>
        {hint && <div className="dim" style={{ fontSize: 12, marginTop: 2 }}>{hint}</div>}
      </div>
      <div className={`toggle ${on ? 'on' : ''}`} onClick={() => onChange(!on)} role="switch" aria-checked={on} />
    </div>
  );
}

export function Ring({ value, size = 64, stroke = 6, color = '#22d3ee', track = 'rgba(148,163,184,0.12)', children }: { value: number; size?: number; stroke?: number; color?: string; track?: string; children?: ReactNode }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const v = Math.max(0, Math.min(1, value));
  return (
    <div style={{ position: 'relative', width: size, height: size, flex: 'none' }}>
      <svg width={size} height={size} style={{ transform: 'rotate(-90deg)' }}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={track} strokeWidth={stroke} />
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={color} strokeWidth={stroke} strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={c * (1 - v)} style={{ transition: 'stroke-dashoffset .25s linear', filter: `drop-shadow(0 0 6px ${color})` }} />
      </svg>
      <div style={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', textAlign: 'center' }}>{children}</div>
    </div>
  );
}

export function Stat({ label, value, unit, color, small }: { label: ReactNode; value: ReactNode; unit?: string; color?: string; small?: boolean }) {
  return (
    <div className="kpi">
      <span className="l">{label}</span>
      <span className="v" style={{ color, fontSize: small ? 20 : undefined }}>{value}{unit && <span className="u">{unit}</span>}</span>
    </div>
  );
}

export function Bar({ value, color = '#22d3ee' }: { value: number; color?: string }) {
  return <div className="bar"><i style={{ width: `${Math.max(0, Math.min(100, value * 100))}%`, background: color }} /></div>;
}

export function Empty({ children }: { children: ReactNode }) {
  return <div className="dim" style={{ padding: 30, textAlign: 'center' }}>{children}</div>;
}
