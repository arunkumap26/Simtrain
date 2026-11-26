import { useEffect, useRef, useSyncExternalStore } from 'react';
import type { AppState, Tick } from './types';

/** WebSocket bridge to the .NET host: commands with replies, 60 Hz ticks, events and full-state pushes. */

type Listener = () => void;
type EventHandler = (kind: string, data: any) => void;

const params = new URLSearchParams(location.search);
const token = params.get('token') ?? '';

let ws: WebSocket | null = null;
let nextId = 1;
const pending = new Map<number, { resolve: (v: any) => void; reject: (e: Error) => void }>();
const eventHandlers = new Set<EventHandler>();

let state: AppState | null = null;
let tick: Tick | null = null;
let connected = false;
const stateListeners = new Set<Listener>();
const tickListeners = new Set<Listener>();
const connListeners = new Set<Listener>();

// Inside the RimForce window the native host reports focus (and ignores its own UI popups); only a plain
// browser tab reports blur itself.
export const isWebView = !!(window as any).chrome?.webview || params.get('shell') === 'webview2';

function emit(set: Set<Listener>) { set.forEach((l) => l()); }

function connect() {
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  const host = params.get('host') ?? location.host;
  ws = new WebSocket(`${proto}://${host}/ws?token=${encodeURIComponent(token)}`);
  ws.onopen = () => { connected = true; emit(connListeners); send('hello').catch(() => {}); };
  ws.onclose = () => {
    connected = false; emit(connListeners);
    pending.forEach((p) => p.reject(new Error('Disconnected')));
    pending.clear();
    setTimeout(connect, 800);
  };
  ws.onerror = () => {};
  ws.onmessage = (m) => {
    let msg: any;
    try { msg = JSON.parse(m.data); } catch { return; }
    switch (msg.type) {
      case 'tick': tick = msg.data; emit(tickListeners); break;
      case 'state': state = msg.data; emit(stateListeners); break;
      case 'event': eventHandlers.forEach((h) => h(msg.kind, msg.data)); break;
      case 'reply': {
        const p = pending.get(msg.id);
        if (p) { pending.delete(msg.id); msg.ok ? p.resolve(msg.data) : p.reject(new Error(msg.error ?? 'Failed')); }
        if (msg.ok && msg.data && msg.data.version && msg.data.drills) { state = msg.data; emit(stateListeners); }
        break;
      }
    }
  };
}

export function send<T = any>(cmd: string, args?: Record<string, unknown>): Promise<T> {
  return new Promise((resolve, reject) => {
    if (!ws || ws.readyState !== WebSocket.OPEN) { reject(new Error('Not connected')); return; }
    const id = nextId++;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, cmd, args: args ?? {} }));
    setTimeout(() => { if (pending.has(id)) { pending.delete(id); reject(new Error('Timed out')); } }, 5000);
  });
}

/** Fire-and-forget (heartbeat, stop) without reply tracking. */
export function post(cmd: string, args?: Record<string, unknown>) {
  if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify({ id: 0, cmd, args: args ?? {} }));
}

export function onEvent(h: EventHandler) { eventHandlers.add(h); return () => { eventHandlers.delete(h); }; }

export function getTick() { return tick; }
export function getState() { return state; }

export function useAppState() {
  return useSyncExternalStore((l) => { stateListeners.add(l); return () => stateListeners.delete(l); }, () => state);
}
export function useConnected() {
  return useSyncExternalStore((l) => { connListeners.add(l); return () => connListeners.delete(l); }, () => connected);
}

/** Re-render at most `hz` times per second with the latest tick (for numbers/text; canvases read getTick() in rAF). */
export function useTick(hz = 20): Tick | null {
  const ref = useRef<Tick | null>(tick);
  const sub = (l: Listener) => {
    let last = 0;
    const fn = () => { const now = performance.now(); if (now - last >= 1000 / hz) { last = now; ref.current = tick; l(); } };
    tickListeners.add(fn);
    return () => { tickListeners.delete(fn); };
  };
  return useSyncExternalStore(sub, () => ref.current);
}

export function useEvent(h: EventHandler, deps: unknown[] = []) {
  useEffect(() => onEvent(h), deps); // eslint-disable-line react-hooks/exhaustive-deps
}

/** Runs fn every animation frame with the latest tick. */
export function useFrame(fn: (t: Tick | null, now: number) => void) {
  const fnRef = useRef(fn);
  fnRef.current = fn;
  useEffect(() => {
    let raf = 0;
    const loop = (now: number) => { fnRef.current(tick, now); raf = requestAnimationFrame(loop); };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, []);
}

// heartbeat: the safety governor stops the wheel if the UI freezes for > 1.5 s
setInterval(() => post('heartbeat'), 250);

// In a plain browser, report focus changes ourselves (in WebView2 the native window does it).
if (!isWebView) {
  window.addEventListener('blur', () => post('focus', { focused: false }));
  window.addEventListener('focus', () => post('focus', { focused: true }));
  document.addEventListener('visibilitychange', () => { if (document.hidden) post('focus', { focused: false }); });
}

connect();
