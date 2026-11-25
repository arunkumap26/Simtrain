import { useRef } from 'react';
import { getTick } from '../bridge';
import type { Guide, SessionSnap, Tick } from '../types';
import { effortColor, polar, rad, useCanvas } from './canvas';

interface Props {
  /** Show the guide ghost and zone (false on non-session pages). */
  guided?: boolean;
  accent?: string;
  /** Demo: drag the wheel with the mouse (sets the simulated driver's target). */
  onDrag?: (angleDeg: number | null) => void;
}

/**
 * The live wheel: real rim rotated to your angle, a grey ghost rim at the target, the target zone on the
 * bezel, upcoming targets as fading dots, the pull/push arrows and hand markers.
 */
export function WheelGauge({ guided = true, accent = '#22d3ee', onDrag }: Props) {
  const disp = useRef({ angle: 0, ghost: 0, init: false, inZoneGlow: 0, trail: [] as { a: number; t: number }[], lastTickT: 0, tickAt: 0 });
  const geom = useRef({ cx: 0, cy: 0 });

  const ref = useCanvas((ctx, w, h, now) => {
    const t = getTick();
    const d = disp.current;
    const act = t?.activity && (t.activity as any).kind === 'session' ? (t.activity as SessionSnap) : null;
    const guide: Guide | null | undefined = guided ? act?.guide : null;

    // --- smoothing / extrapolation between 60 Hz ticks ---
    const target = t ? t.angleDeg : 0;
    if (t && t.t !== d.lastTickT) { d.lastTickT = t.t; d.tickAt = now; }
    const extrap = t ? Math.max(-6, Math.min(6, t.velocityDps * Math.min(0.03, (now - d.tickAt) / 1000))) : 0;
    const want = target + extrap;
    if (!d.init) { d.angle = want; d.init = true; }
    d.angle += (want - d.angle) * 0.55;
    const ghostTarget = guide?.targetDeg ?? d.angle;
    d.ghost += (ghostTarget - d.ghost) * 0.35;

    d.trail.push({ a: d.angle, t: now });
    while (d.trail.length && now - d.trail[0].t > 450) d.trail.shift();

    const size = Math.min(w, h * 0.92);
    const cx = w / 2, cy = h / 2 + h * 0.07;
    geom.current = { cx, cy };
    const R = size * 0.3;           // rim radius
    const bez = size * 0.395;       // bezel radius
    const rimW = Math.max(10, size * 0.045);

    const zone = guide?.zoneDeg ?? 0;
    const err = guide ? d.angle - (guide.targetDeg ?? 0) : 0;
    const inZone = !!guide && guide.showGhost && zone > 0 && Math.abs(err) <= zone;
    d.inZoneGlow += ((inZone ? 1 : 0) - d.inZoneGlow) * 0.12;
    const working = act?.phase === 'work';

    // ---------- bezel ----------
    ctx.save();
    const bg = ctx.createRadialGradient(cx, cy, R * 0.2, cx, cy, bez * 1.08);
    bg.addColorStop(0, 'rgba(20,30,44,0.55)');
    bg.addColorStop(0.75, 'rgba(10,15,23,0.35)');
    bg.addColorStop(1, 'rgba(10,15,23,0)');
    ctx.fillStyle = bg;
    ctx.beginPath(); ctx.arc(cx, cy, bez * 1.08, 0, Math.PI * 2); ctx.fill();

    ctx.strokeStyle = 'rgba(148,163,184,0.12)';
    ctx.lineWidth = 1;
    ctx.beginPath(); ctx.arc(cx, cy, bez, 0, Math.PI * 2); ctx.stroke();
    ctx.beginPath(); ctx.arc(cx, cy, bez - 26, 0, Math.PI * 2); ctx.stroke();

    // ticks every 5°, labels every 45°
    ctx.font = `600 ${Math.max(10, size * 0.022)}px "JetBrains Mono", monospace`;
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    for (let a = -180; a < 180; a += 5) {
      const major = a % 45 === 0, mid = a % 15 === 0;
      const len = major ? 16 : mid ? 10 : 5;
      const [x1, y1] = polar(cx, cy, bez - 2, a);
      const [x2, y2] = polar(cx, cy, bez - 2 - len, a);
      ctx.strokeStyle = major ? 'rgba(203,213,225,0.55)' : 'rgba(148,163,184,0.25)';
      ctx.lineWidth = major ? 2 : 1;
      ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
      if (major) {
        const [lx, ly] = polar(cx, cy, bez + 16, a);
        ctx.fillStyle = 'rgba(148,163,184,0.7)';
        ctx.fillText(a === 0 ? '0' : a === -180 ? '180' : `${a > 0 ? '+' : ''}${a}`, lx, ly);
      }
    }

    // lock-to-lock markers
    if (guide?.markerLeft != null && guide?.markerRight != null) {
      for (const m of [guide.markerLeft, guide.markerRight]) {
        const active = Math.sign(m) === Math.sign(guide.targetDeg);
        const [x1, y1] = polar(cx, cy, bez + 4, m);
        const [x2, y2] = polar(cx, cy, R - rimW, m);
        ctx.strokeStyle = active ? accent : 'rgba(148,163,184,0.25)';
        ctx.lineWidth = active ? 4 : 2;
        ctx.setLineDash([6, 6]);
        ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
        ctx.setLineDash([]);
        if (active) {
          ctx.shadowColor = accent; ctx.shadowBlur = 20;
          const [px, py] = polar(cx, cy, bez - 12, m);
          ctx.fillStyle = accent; ctx.beginPath(); ctx.arc(px, py, 9, 0, Math.PI * 2); ctx.fill();
          ctx.shadowBlur = 0;
        }
      }
    }

    // upcoming path (fading dots on the bezel): where you will need to be
    if (guide && guide.showGhost && guide.path?.length) {
      const n = guide.path.length;
      for (let i = n - 1; i >= 1; i--) {
        const f = 1 - i / n;
        const [px, py] = polar(cx, cy, bez - 34 - (1 - f) * 0, guide.path[i]);
        ctx.fillStyle = `rgba(226,232,240,${0.05 + 0.35 * f * f})`;
        ctx.beginPath(); ctx.arc(px, py, 2 + 3 * f, 0, Math.PI * 2); ctx.fill();
      }
    }

    // target zone arc
    if (guide && guide.showGhost && zone > 0) {
      const z0 = rad(guide.targetDeg - zone - 90), z1 = rad(guide.targetDeg + zone - 90);
      const col = inZone ? '#34d399' : '#fbbf24';
      ctx.strokeStyle = col; ctx.lineWidth = 22; ctx.globalAlpha = 0.18 + 0.25 * d.inZoneGlow;
      ctx.beginPath(); ctx.arc(cx, cy, bez - 13, z0 - rad(1.5), z1 + rad(1.5)); ctx.stroke();
      ctx.globalAlpha = 1; ctx.lineWidth = 4; ctx.shadowColor = col; ctx.shadowBlur = 16;
      ctx.beginPath(); ctx.arc(cx, cy, bez - 2, z0, z1); ctx.stroke();
      ctx.beginPath(); ctx.arc(cx, cy, bez - 24, z0, z1); ctx.stroke();
      ctx.shadowBlur = 0;
      // target needle on the bezel
      const [tx, ty] = polar(cx, cy, bez + 2, guide.targetDeg);
      const [tx2, ty2] = polar(cx, cy, bez - 30, guide.targetDeg);
      ctx.strokeStyle = '#f8fafc'; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(tx, ty); ctx.lineTo(tx2, ty2); ctx.stroke();
    }
    ctx.restore();

    // ---------- error wedge: from you to the target ----------
    if (guide && guide.showGhost && working && Math.abs(err) > zone && zone > 0) {
      const a0 = rad(Math.min(d.angle, guide.targetDeg) - 90), a1 = rad(Math.max(d.angle, guide.targetDeg) - 90);
      ctx.save();
      ctx.fillStyle = 'rgba(251,191,36,0.10)';
      ctx.beginPath(); ctx.moveTo(cx, cy); ctx.arc(cx, cy, R + rimW, a0, a1); ctx.closePath(); ctx.fill();
      // arrow along the rim pointing to the target
      const dir = Math.sign(guide.targetDeg - d.angle);
      drawArcArrow(ctx, cx, cy, R + rimW + 14, d.angle, d.angle + dir * Math.min(40, Math.abs(err)), '#fbbf24', 5);
      ctx.restore();
    }

    // ---------- motion trail ----------
    if (d.trail.length > 2) {
      ctx.save();
      for (let i = 0; i < d.trail.length; i++) {
        const p = d.trail[i];
        const f = 1 - (now - p.t) / 450;
        const [px, py] = polar(cx, cy, R, p.a);
        ctx.fillStyle = `rgba(34,211,238,${0.10 * f})`;
        ctx.beginPath(); ctx.arc(px, py, rimW * 0.55, 0, Math.PI * 2); ctx.fill();
      }
      ctx.restore();
    }

    // ---------- real wheel ----------
    const tq = t?.motorTorqueNm ?? 0;
    const capNm = Math.max(0.5, t?.globalCapNm ?? 4.8);
    const effort = Math.abs(tq) / capNm;
    const glowCol = !guide || !guide.showGhost || !working ? accent : inZone ? '#34d399' : '#fbbf24';
    drawWheel(ctx, cx, cy, R, rimW, d.angle, {
      rim: glowCol, spoke: 'rgba(148,163,184,0.75)', hub: '#0f1723', marker: '#f43f5e', outline: null, glow: 18 + 14 * d.inZoneGlow,
      grips: true,
    });

    // hands
    drawHands(ctx, cx, cy, R, rimW, d.angle, act, guide);

    // ---------- ghost wheel: a translucent grey shadow hovering where the wheel should be ----------
    if (guide && guide.showGhost && act?.phase !== 'complete') {
      ctx.save();
      const sep = Math.min(1, Math.abs(d.ghost - d.angle) / 6); // fade in as you drift off
      ctx.globalAlpha = 0.4 + 0.55 * sep;
      drawWheel(ctx, cx, cy, R, rimW * 0.92, d.ghost, {
        rim: 'rgba(226,232,240,0.40)', spoke: 'rgba(226,232,240,0.28)', hub: 'rgba(0,0,0,0)', marker: 'rgba(248,250,252,0.95)',
        outline: 'rgba(241,245,249,0.9)', dashed: true,
      });
      // ghost hand spots
      const hand = act?.hand ?? 'both';
      for (const [at, on] of [[-90, hand !== 'right'], [90, hand !== 'left']] as const) {
        if (!on) continue;
        const [x, y] = polar(cx, cy, R, d.ghost + at);
        ctx.setLineDash([4, 4]);
        ctx.strokeStyle = 'rgba(241,245,249,0.85)'; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.arc(x, y, rimW * 1.25, 0, Math.PI * 2); ctx.stroke();
        ctx.setLineDash([]);
      }
      // label at the target
      const [lx, ly] = polar(cx, cy, bez + 34, guide.targetDeg);
      ctx.globalAlpha = 1;
      ctx.font = `700 ${Math.max(11, size * 0.024)}px "Barlow Condensed", sans-serif`;
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillStyle = 'rgba(241,245,249,0.9)';
      ctx.fillText(`GHOST ${guide.targetDeg >= 0 ? '+' : '−'}${Math.abs(guide.targetDeg).toFixed(0)}°`, lx, ly);
      ctx.restore();
    }

    // ---------- force arrows: pull (motor) and push (you) ----------
    if (Math.abs(tq) > 0.15) {
      const pullDir = Math.sign(tq); // + = clockwise pull
      const len = 18 + 40 * Math.min(1, effort);
      const thick = 4 + 8 * Math.min(1, effort);
      const col = effort > 0.85 ? '#f43f5e' : '#fb7185'; // pull = always red; effort shows as length/thickness
      // pull arrow (outer, at top-ish of the rim, moving with the wheel)
      drawArcArrow(ctx, cx, cy, R + rimW + 30, d.angle - pullDir * len / 2 + 0, d.angle + pullDir * len / 2, col, thick, 0.9);
      // push arrow (where you push) — dashed cyan opposite direction near the bottom of the rim
      ctx.save(); ctx.setLineDash([7, 6]);
      drawArcArrow(ctx, cx, cy, R + rimW + 30, d.angle + 180 + pullDir * len / 2, d.angle + 180 - pullDir * len / 2, '#22d3ee', 4, 0.85);
      ctx.restore();
    }

    // ---------- hub readout ----------
    ctx.save();
    const hubR = R * 0.36;
    // progress ring
    const prog = guide?.repProgress ?? 0;
    ctx.strokeStyle = 'rgba(148,163,184,0.12)'; ctx.lineWidth = 5;
    ctx.beginPath(); ctx.arc(cx, cy, hubR + 9, 0, Math.PI * 2); ctx.stroke();
    if (prog > 0) {
      ctx.strokeStyle = accent; ctx.shadowColor = accent; ctx.shadowBlur = 10;
      ctx.beginPath(); ctx.arc(cx, cy, hubR + 9, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * Math.min(1, prog)); ctx.stroke();
      ctx.shadowBlur = 0;
    }
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillStyle = '#f8fafc';
    ctx.font = `700 ${Math.round(size * 0.075)}px "JetBrains Mono", monospace`;
    const a = t?.angleDeg ?? 0;
    ctx.fillText(`${a >= 0 ? '+' : '−'}${Math.abs(a).toFixed(1)}°`, cx, cy - size * 0.012);
    ctx.font = `600 ${Math.round(size * 0.026)}px "Inter", sans-serif`;
    if (guide && guide.showGhost) {
      ctx.fillStyle = inZone ? '#34d399' : '#fbbf24';
      ctx.fillText(`${inZone ? 'IN ZONE' : 'OFF'} · Δ ${err >= 0 ? '+' : '−'}${Math.abs(err).toFixed(1)}°`, cx, cy + size * 0.05);
    } else {
      ctx.fillStyle = 'rgba(148,163,184,0.8)';
      ctx.fillText(`${Math.round(t?.velocityDps ?? 0)}°/s`, cx, cy + size * 0.05);
    }
    ctx.restore();
  });

  const toAngle = (e: React.PointerEvent) => {
    const r = (e.target as HTMLCanvasElement).getBoundingClientRect();
    const x = e.clientX - r.left - geom.current.cx, y = e.clientY - r.top - geom.current.cy;
    return (Math.atan2(x, -y) * 180) / Math.PI;
  };
  const dragging = useRef(false);

  return (
    <canvas
      ref={ref}
      style={{ width: '100%', height: '100%', display: 'block', cursor: onDrag ? 'grab' : 'default', touchAction: 'none' }}
      onPointerDown={onDrag ? (e) => { dragging.current = true; (e.target as Element).setPointerCapture(e.pointerId); onDrag(toAngle(e)); } : undefined}
      onPointerMove={onDrag ? (e) => { if (dragging.current) onDrag(toAngle(e)); } : undefined}
      onPointerUp={onDrag ? () => { dragging.current = false; } : undefined}
    />
  );
}

interface WheelStyle { rim: string; spoke: string; hub: string; marker: string; outline: string | null; dashed?: boolean; glow?: number; grips?: boolean }

function drawWheel(ctx: CanvasRenderingContext2D, cx: number, cy: number, R: number, rimW: number, angle: number, s: WheelStyle) {
  ctx.save();
  ctx.translate(cx, cy);
  ctx.rotate(rad(angle));
  // spokes (3 o'clock, 9 o'clock, 6 o'clock) — formula-style flat-bottom feel
  ctx.strokeStyle = s.spoke; ctx.lineCap = 'round';
  ctx.lineWidth = rimW * 0.9;
  ctx.beginPath(); ctx.moveTo(-R + rimW * 0.3, 0); ctx.lineTo(R - rimW * 0.3, 0); ctx.stroke();
  ctx.lineWidth = rimW * 0.8;
  ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(0, R - rimW * 0.3); ctx.stroke();
  // hub
  ctx.fillStyle = s.hub;
  ctx.beginPath(); ctx.arc(0, 0, R * 0.36, 0, Math.PI * 2); ctx.fill();
  if (!s.dashed) {
    ctx.strokeStyle = 'rgba(148,163,184,0.35)'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(0, 0, R * 0.36, 0, Math.PI * 2); ctx.stroke();
  }
  // rim
  if (s.glow) { ctx.shadowColor = s.rim; ctx.shadowBlur = s.glow; }
  ctx.strokeStyle = s.rim; ctx.lineWidth = rimW;
  if (s.dashed) ctx.setLineDash([]);
  ctx.beginPath(); ctx.arc(0, 0, R, 0, Math.PI * 2); ctx.stroke();
  ctx.shadowBlur = 0;
  if (!s.dashed) {
    // inner highlight
    const g = ctx.createLinearGradient(0, -R, 0, R);
    g.addColorStop(0, 'rgba(255,255,255,0.35)'); g.addColorStop(0.5, 'rgba(255,255,255,0)'); g.addColorStop(1, 'rgba(0,0,0,0.25)');
    ctx.strokeStyle = g; ctx.lineWidth = rimW * 0.45;
    ctx.beginPath(); ctx.arc(0, 0, R, 0, Math.PI * 2); ctx.stroke();
  }
  if (s.grips) {
    // grip sections at 9 and 3
    ctx.strokeStyle = 'rgba(15,23,35,0.55)'; ctx.lineWidth = rimW * 1.12;
    for (const base of [0, Math.PI]) {
      ctx.beginPath(); ctx.arc(0, 0, R, base - 0.42, base + 0.42); ctx.stroke();
    }
    ctx.strokeStyle = 'rgba(255,255,255,0.05)'; ctx.lineWidth = 1;
    for (let i = -6; i <= 6; i++) for (const base of [0, Math.PI]) {
      const a = base + i * 0.065;
      ctx.beginPath(); ctx.moveTo(Math.cos(a) * (R - rimW / 2), Math.sin(a) * (R - rimW / 2)); ctx.lineTo(Math.cos(a) * (R + rimW / 2), Math.sin(a) * (R + rimW / 2)); ctx.stroke();
    }
  }
  if (s.outline) {
    ctx.strokeStyle = s.outline; ctx.lineWidth = 1.5; ctx.setLineDash(s.dashed ? [8, 7] : []);
    ctx.beginPath(); ctx.arc(0, 0, R + rimW / 2, 0, Math.PI * 2); ctx.stroke();
    ctx.beginPath(); ctx.arc(0, 0, R - rimW / 2, 0, Math.PI * 2); ctx.stroke();
    ctx.setLineDash([]);
  }
  // top-centre marker stripe
  ctx.strokeStyle = s.marker; ctx.lineWidth = rimW * 1.05; ctx.lineCap = 'butt';
  ctx.beginPath(); ctx.arc(0, 0, R, -Math.PI / 2 - 0.05, -Math.PI / 2 + 0.05); ctx.stroke();
  ctx.restore();
}

function drawHands(ctx: CanvasRenderingContext2D, cx: number, cy: number, R: number, rimW: number, angle: number, act: SessionSnap | null, guide: Guide | null | undefined) {
  const hand = act?.hand ?? 'both';
  const sides: { at: number; name: string; active: boolean }[] = [
    { at: -90, name: 'L', active: hand === 'both' || hand === 'left' },
    { at: 90, name: 'R', active: hand === 'both' || hand === 'right' },
  ];
  ctx.save();
  for (const s of sides) {
    const [x, y] = polar(cx, cy, R, angle + s.at);
    ctx.globalAlpha = s.active ? 1 : 0.25;
    ctx.fillStyle = s.active ? 'rgba(34,211,238,0.18)' : 'rgba(148,163,184,0.1)';
    ctx.strokeStyle = s.active ? '#22d3ee' : 'rgba(148,163,184,0.5)';
    ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(x, y, rimW * 1.15, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    ctx.fillStyle = s.active ? '#e0f7fb' : '#64748b';
    ctx.font = `700 ${Math.round(rimW * 0.95)}px "Barlow Condensed", sans-serif`;
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText(s.name, x, y + 1);
    if (s.active && guide?.action && hand !== 'both') {
      // lift / press arrow
      const up = guide.action === 'lift';
      ctx.fillStyle = '#22d3ee';
      const dy = up ? -1 : 1;
      ctx.beginPath();
      ctx.moveTo(x, y + dy * (rimW * 2.6)); ctx.lineTo(x - 9, y + dy * (rimW * 1.7)); ctx.lineTo(x + 9, y + dy * (rimW * 1.7)); ctx.closePath(); ctx.fill();
      ctx.font = `700 13px "Barlow Condensed", sans-serif`;
      ctx.fillText(up ? 'LIFT' : 'PRESS', x, y + dy * (rimW * 3.4));
    }
  }
  ctx.restore();
}

function drawArcArrow(ctx: CanvasRenderingContext2D, cx: number, cy: number, r: number, fromDeg: number, toDeg: number, color: string, width: number, alpha = 1) {
  if (Math.abs(toDeg - fromDeg) < 1) return;
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.strokeStyle = color; ctx.fillStyle = color; ctx.lineWidth = width; ctx.lineCap = 'round';
  ctx.shadowColor = color; ctx.shadowBlur = 12;
  const a0 = rad(fromDeg - 90), a1 = rad(toDeg - 90);
  ctx.beginPath(); ctx.arc(cx, cy, r, Math.min(a0, a1), Math.max(a0, a1)); ctx.stroke();
  // head
  const dir = Math.sign(toDeg - fromDeg);
  const [hx, hy] = polar(cx, cy, r, toDeg);
  const tang = a1 + (dir * Math.PI) / 2;
  const hl = width * 2.6 + 6;
  ctx.setLineDash([]);
  ctx.beginPath();
  ctx.moveTo(hx + Math.cos(tang) * hl * 0.6, hy + Math.sin(tang) * hl * 0.6);
  ctx.lineTo(hx - Math.cos(tang) * hl * 0.4 + Math.cos(a1) * hl * 0.55, hy - Math.sin(tang) * hl * 0.4 + Math.sin(a1) * hl * 0.55);
  ctx.lineTo(hx - Math.cos(tang) * hl * 0.4 - Math.cos(a1) * hl * 0.55, hy - Math.sin(tang) * hl * 0.4 - Math.sin(a1) * hl * 0.55);
  ctx.closePath(); ctx.fill();
  ctx.restore();
}

export type { Tick };
