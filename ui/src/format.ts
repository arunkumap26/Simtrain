import type { AppSettings } from './types';

export const G = 9.80665;

export function force(kgf: number, unit: AppSettings['forceUnit'] = 'kgf'): { v: string; u: string } {
  if (unit === 'N') return { v: (kgf * G).toFixed(0), u: 'N' };
  if (unit === 'lbf') return { v: (kgf * 2.20462).toFixed(1), u: 'lbf' };
  return { v: kgf.toFixed(1), u: 'kgf' };
}

export const nm = (v: number, d = 1) => `${v.toFixed(d)} Nm`;
export const deg = (v: number, d = 1) => `${v >= 0 ? '+' : '−'}${Math.abs(v).toFixed(d)}°`;
export const pct = (v: number, d = 0) => `${v.toFixed(d)}%`;
export const clamp = (v: number, a: number, b: number) => Math.min(b, Math.max(a, v));

export function mmss(s: number) {
  s = Math.max(0, Math.round(s));
  const m = Math.floor(s / 60);
  return `${m}:${String(s % 60).padStart(2, '0')}`;
}

export function duration(s: number) {
  if (s >= 3600) return '60+ min';
  if (s >= 120) return `${Math.round(s / 60)} min`;
  return `${Math.round(s)} s`;
}

export function rimKgf(nmVal: number, rimDiameterMm: number) {
  return nmVal / (rimDiameterMm / 2000) / G;
}

export const TARGET_LABELS: Record<string, string> = {
  forearm: 'Forearm', grip: 'Grip', shoulder: 'Shoulders', biceps: 'Biceps', reaction: 'Reaction', endurance: 'Endurance',
};

export const TARGET_COLORS: Record<string, string> = {
  forearm: '#22d3ee', grip: '#a78bfa', shoulder: '#fb923c', biceps: '#34d399', reaction: '#f472b6', endurance: '#fbbf24',
};

export function relTime(iso: string) {
  const d = new Date(iso).getTime();
  const diff = (Date.now() - d) / 1000;
  if (diff < 90) return 'just now';
  if (diff < 3600) return `${Math.round(diff / 60)} min ago`;
  if (diff < 86400) return `${Math.round(diff / 3600)} h ago`;
  return `${Math.round(diff / 86400)} d ago`;
}
