/** Lightweight SVG charts styled for the dark telemetry look. */

export interface Series { name: string; color: string; points: { x: number; y: number; label?: string; hollow?: boolean }[] }

export function LineChart({ series, height = 220, yLabel, xFmt }: { series: Series[]; height?: number; yLabel?: string; xFmt?: (x: number) => string }) {
  const W = 640, H = height, L = 44, R = 12, T = 14, B = 28;
  const all = series.flatMap((s) => s.points);
  if (all.length === 0) return <div className="dim" style={{ height, display: 'grid', placeItems: 'center' }}>No data yet</div>;
  const xs = all.map((p) => p.x), ys = all.map((p) => p.y);
  let x0 = Math.min(...xs), x1 = Math.max(...xs);
  if (x1 === x0) { x0 -= 1; x1 += 1; }
  const yMax = Math.max(...ys) * 1.15 || 1, yMin = 0;
  const X = (x: number) => L + ((x - x0) / (x1 - x0)) * (W - L - R);
  const Y = (y: number) => T + (1 - (y - yMin) / (yMax - yMin)) * (H - T - B);
  const ticks = 4;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height }}>
      {Array.from({ length: ticks + 1 }).map((_, i) => {
        const v = yMin + ((yMax - yMin) * i) / ticks;
        return (
          <g key={i}>
            <line x1={L} x2={W - R} y1={Y(v)} y2={Y(v)} stroke="rgba(148,163,184,0.1)" />
            <text x={L - 6} y={Y(v) + 4} fill="#64748b" fontSize="10" textAnchor="end" fontFamily="JetBrains Mono">{v.toFixed(v < 10 ? 1 : 0)}</text>
          </g>
        );
      })}
      {yLabel && <text x={6} y={T + 4} fill="#64748b" fontSize="10" fontFamily="Barlow Condensed">{yLabel}</text>}
      {xFmt && [x0, (x0 + x1) / 2, x1].map((x, i) => <text key={i} x={X(x)} y={H - 8} fill="#64748b" fontSize="10" textAnchor={i === 0 ? 'start' : i === 2 ? 'end' : 'middle'} fontFamily="JetBrains Mono">{xFmt(x)}</text>)}
      {series.map((s) => {
        const pts = [...s.points].sort((a, b) => a.x - b.x);
        const d = pts.map((p, i) => `${i ? 'L' : 'M'}${X(p.x).toFixed(1)},${Y(p.y).toFixed(1)}`).join(' ');
        return (
          <g key={s.name}>
            <path d={d} fill="none" stroke={s.color} strokeWidth="2.5" style={{ filter: `drop-shadow(0 0 5px ${s.color})` }} />
            {pts.map((p, i) => <circle key={i} cx={X(p.x)} cy={Y(p.y)} r="4" fill={p.hollow ? '#0b1018' : s.color} stroke={s.color} strokeWidth="2"><title>{p.label ?? p.y.toFixed(2)}</title></circle>)}
          </g>
        );
      })}
    </svg>
  );
}

export function BarChart({ data, height = 200, color = '#22d3ee', unit }: { data: { label: string; value: number }[]; height?: number; color?: string; unit?: string }) {
  const W = 640, H = height, L = 40, R = 10, T = 12, B = 26;
  if (!data.length) return <div className="dim" style={{ height, display: 'grid', placeItems: 'center' }}>No data yet</div>;
  const max = Math.max(...data.map((d) => d.value)) * 1.15 || 1;
  const bw = (W - L - R) / data.length;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height }}>
      {[0, 0.5, 1].map((f) => (
        <g key={f}>
          <line x1={L} x2={W - R} y1={T + (1 - f) * (H - T - B)} y2={T + (1 - f) * (H - T - B)} stroke="rgba(148,163,184,0.1)" />
          <text x={L - 6} y={T + (1 - f) * (H - T - B) + 4} fill="#64748b" fontSize="10" textAnchor="end" fontFamily="JetBrains Mono">{(max * f).toFixed(0)}</text>
        </g>
      ))}
      {data.map((d, i) => {
        const h = (d.value / max) * (H - T - B);
        return (
          <g key={i}>
            <rect x={L + i * bw + bw * 0.18} y={H - B - h} width={bw * 0.64} height={h} rx="4" fill={color} opacity={i === data.length - 1 ? 1 : 0.55} style={{ filter: i === data.length - 1 ? `drop-shadow(0 0 8px ${color})` : undefined }}><title>{`${d.value.toFixed(0)} ${unit ?? ''}`}</title></rect>
            <text x={L + i * bw + bw / 2} y={H - 8} fill="#64748b" fontSize="10" textAnchor="middle" fontFamily="JetBrains Mono">{d.label}</text>
          </g>
        );
      })}
    </svg>
  );
}

/** Set trace: [t, angle, target, torque]*. */
export function TraceChart({ trace, height = 200 }: { trace: number[]; height?: number }) {
  const W = 640, H = height;
  const n = Math.floor(trace.length / 4);
  if (n < 2) return <div className="dim" style={{ height, display: 'grid', placeItems: 'center' }}>No trace</div>;
  const pts = Array.from({ length: n }, (_, i) => ({ t: trace[i * 4], a: trace[i * 4 + 1], g: trace[i * 4 + 2], q: Math.abs(trace[i * 4 + 3]) }));
  const t1 = pts[n - 1].t || 1;
  const am = Math.max(10, ...pts.map((p) => Math.max(Math.abs(p.a), Math.abs(p.g)))) * 1.15;
  const qm = Math.max(1, ...pts.map((p) => p.q)) * 1.15;
  const X = (t: number) => (t / t1) * W;
  const Ya = (a: number) => H / 2 - (a / am) * (H / 2 - 6);
  const Yq = (q: number) => H - (q / qm) * (H - 6);
  const line = (f: (p: (typeof pts)[0]) => number) => pts.map((p, i) => `${i ? 'L' : 'M'}${X(p.t).toFixed(1)},${f(p).toFixed(1)}`).join(' ');
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height }}>
      <path d={`${line((p) => Yq(p.q))} L${W},${H} L0,${H} Z`} fill="rgba(251,146,60,0.15)" stroke="rgba(251,146,60,0.7)" />
      <path d={line((p) => Ya(p.g))} fill="none" stroke="rgba(248,250,252,0.7)" strokeDasharray="6 5" strokeWidth="1.5" />
      <path d={line((p) => Ya(p.a))} fill="none" stroke="#22d3ee" strokeWidth="2" />
    </svg>
  );
}
