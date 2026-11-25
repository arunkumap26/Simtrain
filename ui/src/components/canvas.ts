import { useEffect, useRef } from 'react';

/** Canvas sized to its CSS box at device pixel ratio. draw() is called every frame with a ctx scaled to CSS px. */
export function useCanvas(draw: (ctx: CanvasRenderingContext2D, w: number, h: number, now: number) => void) {
  const ref = useRef<HTMLCanvasElement | null>(null);
  const drawRef = useRef(draw);
  drawRef.current = draw;
  useEffect(() => {
    const c = ref.current;
    if (!c) return;
    let raf = 0;
    let w = 0, h = 0, dpr = 1;
    const resize = () => {
      const r = c.getBoundingClientRect();
      dpr = Math.min(2, window.devicePixelRatio || 1);
      w = Math.max(1, r.width); h = Math.max(1, r.height);
      c.width = Math.round(w * dpr); c.height = Math.round(h * dpr);
    };
    const ro = new ResizeObserver(resize);
    ro.observe(c);
    resize();
    const loop = (now: number) => {
      const ctx = c.getContext('2d');
      if (ctx) {
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, w, h);
        try { drawRef.current(ctx, w, h, now); } catch (e) { /* keep animating */ }
      }
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => { cancelAnimationFrame(raf); ro.disconnect(); };
  }, []);
  return ref;
}

export const rad = (d: number) => (d * Math.PI) / 180;

/** Angle on the dial: 0° at 12 o'clock, positive clockwise. */
export function polar(cx: number, cy: number, r: number, degFromTop: number) {
  const a = rad(degFromTop - 90);
  return [cx + r * Math.cos(a), cy + r * Math.sin(a)] as const;
}

export function lerpColor(a: string, b: string, t: number) {
  const pa = parseInt(a.slice(1), 16), pb = parseInt(b.slice(1), 16);
  const ar = pa >> 16, ag = (pa >> 8) & 255, ab = pa & 255;
  const br = pb >> 16, bg = (pb >> 8) & 255, bb = pb & 255;
  const r = Math.round(ar + (br - ar) * t), g = Math.round(ag + (bg - ag) * t), bl = Math.round(ab + (bb - ab) * t);
  return `rgb(${r},${g},${bl})`;
}

/** cyan → green → amber → red as effort rises. */
export function effortColor(f: number) {
  f = Math.max(0, Math.min(1.2, f));
  if (f < 0.35) return lerpColor('#22d3ee', '#34d399', f / 0.35);
  if (f < 0.65) return lerpColor('#34d399', '#fbbf24', (f - 0.35) / 0.3);
  if (f < 0.9) return lerpColor('#fbbf24', '#fb923c', (f - 0.65) / 0.25);
  return lerpColor('#fb923c', '#f43f5e', Math.min(1, (f - 0.9) / 0.2));
}
