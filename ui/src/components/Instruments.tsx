import { useRef } from 'react';
import { getTick } from '../bridge';
import type { SessionSnap } from '../types';
import { effortColor, useCanvas } from './canvas';

const session = () => {
  const t = getTick();
  return t?.activity && (t.activity as any).kind === 'session' ? (t.activity as SessionSnap) : null;
};

/**
 * Runway: the road ahead. Upcoming target angles scroll towards you (top = future, line = now),
 * your recent angles trail below the line. Keep your dot on the road.
 */
export function Runway({ accent = '#22d3ee' }: { accent?: string }) {
  const hist = useRef<{ t: number; a: number; target: number }[]>([]);
  const ref = useCanvas((ctx, w, h, now) => {
    const t = getTick();
    const s = session();
    const g = s?.guide;
    const angle = t?.angleDeg ?? 0;
    const path = g?.showGhost && g.path?.length ? g.path : [angle];
    const step = g?.pathStepS ?? 0.1;
    const horizonS = path.length * step;

    hist.current.push({ t: now, a: angle, target: g?.targetDeg ?? angle });
    while (hist.current.length && now - hist.current[0].t > 2500) hist.current.shift();

    const maxA = Math.max(30, ...path.map(Math.abs), Math.abs(angle)) * 1.25;
    const span = Math.ceil(maxA / 15) * 15;
    const X = (a: number) => w / 2 + (a / span) * (w / 2 - 14);
    const nowY = h * 0.78;
    const pxPerS = nowY / horizonS;
    const pastPx = (h - nowY) / 2.5;

    // grid
    ctx.font = '600 10px "JetBrains Mono", monospace';
    ctx.textAlign = 'center';
    for (let a = -span; a <= span; a += span >= 90 ? 45 : 15) {
      ctx.strokeStyle = a === 0 ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.08)';
      ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(X(a), 0); ctx.lineTo(X(a), h); ctx.stroke();
      ctx.fillStyle = 'rgba(148,163,184,0.5)';
      ctx.fillText(`${a > 0 ? '+' : ''}${a}`, X(a), h - 6);
    }

    // road ribbon (future)
    if (g?.showGhost) {
      const zone = Math.max(2, g.zoneDeg || 3);
      const zonePx = Math.max(10, (zone / span) * (w / 2 - 14));
      const grad = ctx.createLinearGradient(0, 0, 0, nowY);
      grad.addColorStop(0, 'rgba(203,213,225,0.02)');
      grad.addColorStop(1, 'rgba(203,213,225,0.22)');
      ctx.fillStyle = grad;
      ctx.beginPath();
      for (let i = 0; i < path.length; i++) { const y = nowY - i * step * pxPerS; ctx.lineTo(X(path[i]) - zonePx, y); }
      for (let i = path.length - 1; i >= 0; i--) { const y = nowY - i * step * pxPerS; ctx.lineTo(X(path[i]) + zonePx, y); }
      ctx.closePath(); ctx.fill();
      // edges
      for (const sgn of [-1, 1]) {
        ctx.strokeStyle = 'rgba(226,232,240,0.35)'; ctx.lineWidth = 1.5;
        ctx.beginPath();
        for (let i = 0; i < path.length; i++) ctx.lineTo(X(path[i]) + sgn * zonePx, nowY - i * step * pxPerS);
        ctx.stroke();
      }
      // centre line dashes
      ctx.setLineDash([10, 12]); ctx.strokeStyle = 'rgba(248,250,252,0.45)'; ctx.lineWidth = 2;
      ctx.beginPath();
      for (let i = 0; i < path.length; i++) ctx.lineTo(X(path[i]), nowY - i * step * pxPerS);
      ctx.stroke(); ctx.setLineDash([]);
    }

    // past trail below the now line
    const hh = hist.current;
    ctx.lineWidth = 3; ctx.lineCap = 'round';
    for (let i = 1; i < hh.length; i++) {
      const y0 = nowY + ((now - hh[i - 1].t) / 1000) * pastPx, y1 = nowY + ((now - hh[i].t) / 1000) * pastPx;
      const off = g?.showGhost ? Math.abs(hh[i].a - hh[i].target) > (g.zoneDeg || 3) : false;
      ctx.strokeStyle = off ? 'rgba(251,191,36,0.7)' : 'rgba(34,211,238,0.7)';
      ctx.beginPath(); ctx.moveTo(X(hh[i - 1].a), y0); ctx.lineTo(X(hh[i].a), y1); ctx.stroke();
    }

    // now line + your marker
    ctx.strokeStyle = 'rgba(248,250,252,0.25)'; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(0, nowY); ctx.lineTo(w, nowY); ctx.stroke();
    const inZone = g?.showGhost ? Math.abs(angle - g.targetDeg) <= (g.zoneDeg || 3) : true;
    const col = inZone ? (g?.showGhost ? '#34d399' : accent) : '#fbbf24';
    ctx.shadowColor = col; ctx.shadowBlur = 18; ctx.fillStyle = col;
    ctx.beginPath(); ctx.moveTo(X(angle), nowY - 14); ctx.lineTo(X(angle) - 10, nowY + 6); ctx.lineTo(X(angle) + 10, nowY + 6); ctx.closePath(); ctx.fill();
    ctx.shadowBlur = 0;

    // label
    ctx.fillStyle = 'rgba(148,163,184,0.6)'; ctx.textAlign = 'left';
    ctx.font = '600 11px "Barlow Condensed", sans-serif';
    ctx.fillText(`TRACK AHEAD · ${horizonS.toFixed(0)} s`, 8, 14);
  });
  return <canvas ref={ref} style={{ width: '100%', height: '100%' }} />;
}

/**
 * Vertical force meter: your torque now, the prescribed load (TARGET), your tested max (MAX) and the safety cap (CAP).
 */
export function ForceMeter({ rimDiameterMm }: { rimDiameterMm: number }) {
  const smooth = useRef(0);
  const peak = useRef({ v: 0, t: 0 });
  const ref = useCanvas((ctx, w, h, now) => {
    const t = getTick();
    const s = session();
    const tq = Math.abs(t?.strength?.torqueNm ?? 0);
    smooth.current += (tq - smooth.current) * 0.3;
    if (tq > peak.current.v || now - peak.current.t > 2500) peak.current = { v: tq, t: now };
    const cap = t?.globalCapNm ?? 4.8;
    const maxDev = t?.maxTorqueNm ?? 12;
    const isTest = s?.drill.category === 'Test';
    const load = isTest ? (s?.live?.fvLoadNm ?? s?.live?.rungNm ?? s?.live?.rampNm ?? 0) : (s?.loadNm ?? 0);
    const ref = s?.refIsBreakpoint ? s.refMaxNm ?? null : null;
    const scale = Math.max(cap * 1.08, (ref ?? 0) * 1.08, load * 1.25, 2);

    const top = 10, bottom = h - 10, bx = w * 0.18, bw = w * 0.3;
    const Y = (v: number) => bottom - (Math.min(v, scale) / scale) * (bottom - top);

    // track
    ctx.fillStyle = '#0c121b';
    roundRect(ctx, bx, top, bw, bottom - top, 10); ctx.fill();
    ctx.strokeStyle = '#1e2938'; ctx.lineWidth = 1; ctx.stroke();
    // ticks
    ctx.font = '500 10px "JetBrains Mono", monospace'; ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    const stepNm = scale > 10 ? 2 : scale > 5 ? 1 : 0.5;
    for (let v = 0; v <= scale + 1e-6; v += stepNm) {
      ctx.fillStyle = 'rgba(148,163,184,0.45)';
      ctx.fillText(v.toFixed(stepNm < 1 ? 1 : 0), bx - 6, Y(v));
      ctx.strokeStyle = 'rgba(148,163,184,0.12)';
      ctx.beginPath(); ctx.moveTo(bx, Y(v)); ctx.lineTo(bx + bw, Y(v)); ctx.stroke();
    }
    // fill
    const f = smooth.current / Math.max(0.5, ref ?? cap);
    const col = effortColor(f);
    const grad = ctx.createLinearGradient(0, bottom, 0, Y(smooth.current));
    grad.addColorStop(0, 'rgba(34,211,238,0.6)');
    grad.addColorStop(1, col);
    ctx.fillStyle = grad;
    ctx.shadowColor = col; ctx.shadowBlur = 16;
    roundRect(ctx, bx + 3, Y(smooth.current), bw - 6, bottom - Y(smooth.current) - 3, 7); ctx.fill();
    ctx.shadowBlur = 0;
    // peak hold
    ctx.strokeStyle = '#f8fafc'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(bx + 2, Y(peak.current.v)); ctx.lineTo(bx + bw - 2, Y(peak.current.v)); ctx.stroke();

    const mark = (v: number, label: string, color: string, dash: number[] = []) => {
      const y = Y(v);
      ctx.setLineDash(dash); ctx.strokeStyle = color; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(bx - 2, y); ctx.lineTo(bx + bw + 6, y); ctx.stroke(); ctx.setLineDash([]);
      ctx.fillStyle = color; ctx.textAlign = 'left'; ctx.font = '700 11px "Barlow Condensed", sans-serif';
      ctx.fillText(label, bx + bw + 9, y - 6);
      ctx.font = '500 10px "JetBrains Mono", monospace';
      ctx.fillText(v.toFixed(1), bx + bw + 9, y + 7);
    };
    mark(cap, 'CAP', '#f43f5e', [4, 4]);
    if (ref) mark(ref, 'YOUR MAX', '#a78bfa');
    if (load > 0 && !(ref && Math.abs(load - ref) < scale * 0.06)) mark(load, isTest ? 'LOAD' : 'TARGET', '#f8fafc', [6, 4]);
    void maxDev; void rimDiameterMm;
  });
  return <canvas ref={ref} style={{ width: '100%', height: '100%' }} />;
}

/** MoTeC-style trace: angle (cyan) vs target (white dashed) and torque (orange area), last 12 s. */
export function TelemetryTrace() {
  const buf = useRef<{ t: number; a: number; g: number | null; q: number }[]>([]);
  const lastT = useRef(-1);
  const ref = useCanvas((ctx, w, h) => {
    const t = getTick();
    const s = session();
    if (t && t.t !== lastT.current) {
      lastT.current = t.t;
      const g = s?.guide?.showGhost && s.phase === 'work' ? s.guide.targetDeg : null;
      buf.current.push({ t: t.t, a: t.angleDeg, g, q: Math.abs(t.strength?.torqueNm ?? 0) });
    }
    const tNow = t?.t ?? 0;
    while (buf.current.length && tNow - buf.current[0].t > 12) buf.current.shift();
    const b = buf.current;
    const amax = Math.max(20, ...b.map((p) => Math.max(Math.abs(p.a), Math.abs(p.g ?? 0)))) * 1.15;
    const qmax = Math.max(2, (t?.globalCapNm ?? 4) * 1.05);
    const X = (tt: number) => w - ((tNow - tt) / 12) * w;
    const Ya = (a: number) => h / 2 - (a / amax) * (h / 2 - 8);
    const Yq = (q: number) => h - (q / qmax) * (h - 8);

    // grid
    ctx.strokeStyle = 'rgba(148,163,184,0.08)'; ctx.lineWidth = 1;
    for (let i = 0; i <= 12; i += 2) { ctx.beginPath(); ctx.moveTo(X(tNow - i), 0); ctx.lineTo(X(tNow - i), h); ctx.stroke(); }
    ctx.beginPath(); ctx.moveTo(0, h / 2); ctx.lineTo(w, h / 2); ctx.stroke();

    // torque area
    if (b.length > 1) {
      ctx.beginPath(); ctx.moveTo(X(b[0].t), h);
      for (const p of b) ctx.lineTo(X(p.t), Yq(p.q));
      ctx.lineTo(X(b[b.length - 1].t), h); ctx.closePath();
      const gr = ctx.createLinearGradient(0, 0, 0, h);
      gr.addColorStop(0, 'rgba(251,146,60,0.35)'); gr.addColorStop(1, 'rgba(251,146,60,0.02)');
      ctx.fillStyle = gr; ctx.fill();
      ctx.strokeStyle = 'rgba(251,146,60,0.8)'; ctx.lineWidth = 1.5;
      ctx.beginPath(); b.forEach((p, i) => (i ? ctx.lineTo(X(p.t), Yq(p.q)) : ctx.moveTo(X(p.t), Yq(p.q)))); ctx.stroke();
      // target
      ctx.setLineDash([6, 5]); ctx.strokeStyle = 'rgba(248,250,252,0.7)'; ctx.lineWidth = 1.5;
      ctx.beginPath();
      let pen = false;
      for (const p of b) { if (p.g == null) { pen = false; continue; } if (!pen) { ctx.moveTo(X(p.t), Ya(p.g)); pen = true; } else ctx.lineTo(X(p.t), Ya(p.g)); }
      ctx.stroke(); ctx.setLineDash([]);
      // angle
      ctx.strokeStyle = '#22d3ee'; ctx.lineWidth = 2.2; ctx.shadowColor = '#22d3ee'; ctx.shadowBlur = 8;
      ctx.beginPath(); b.forEach((p, i) => (i ? ctx.lineTo(X(p.t), Ya(p.a)) : ctx.moveTo(X(p.t), Ya(p.a)))); ctx.stroke();
      ctx.shadowBlur = 0;
    }
    ctx.font = '600 11px "Barlow Condensed", sans-serif'; ctx.textAlign = 'left';
    ctx.fillStyle = '#22d3ee'; ctx.fillText('ANGLE', 8, 14);
    ctx.fillStyle = 'rgba(248,250,252,0.8)'; ctx.fillText('TARGET', 52, 14);
    ctx.fillStyle = '#fb923c'; ctx.fillText('TORQUE', 100, 14);
    ctx.fillStyle = 'rgba(148,163,184,0.5)'; ctx.textAlign = 'right'; ctx.fillText(`±${amax.toFixed(0)}° · ${qmax.toFixed(1)} Nm · 12 s`, w - 8, 14);
  });
  return <canvas ref={ref} style={{ width: '100%', height: '100%' }} />;
}

export function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  if (h < 0) { y += h; h = -h; }
  r = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}
