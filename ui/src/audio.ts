/** Synthesised audio cues (no assets) + optional voice, so eyes can stay on the gauge. */

let ctx: AudioContext | null = null;
let enabled = true;
let volume = 0.7;
let voice = true;

export function configureAudio(opts: { enabled: boolean; volume: number; voice: boolean }) {
  enabled = opts.enabled; volume = opts.volume; voice = opts.voice;
}

function ac() {
  if (!ctx) ctx = new AudioContext();
  if (ctx.state === 'suspended') ctx.resume().catch(() => {});
  return ctx;
}

function tone(freq: number, dur: number, type: OscillatorType = 'sine', gain = 0.25, delay = 0, slideTo?: number) {
  if (!enabled) return;
  try {
    const c = ac();
    const t0 = c.currentTime + delay;
    const o = c.createOscillator();
    const g = c.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t0);
    if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t0 + dur);
    g.gain.setValueAtTime(0, t0);
    g.gain.linearRampToValueAtTime(gain * volume, t0 + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    o.connect(g).connect(c.destination);
    o.start(t0);
    o.stop(t0 + dur + 0.05);
  } catch { /* audio not available */ }
}

export function say(text: string) {
  if (!enabled || !voice || !('speechSynthesis' in window)) return;
  try {
    const u = new SpeechSynthesisUtterance(text);
    u.rate = 1.08; u.pitch = 1; u.volume = volume;
    speechSynthesis.cancel();
    speechSynthesis.speak(u);
  } catch { }
}

export const sfx = {
  countdown: (n: number) => tone(n === 1 ? 740 : 620, 0.12, 'square', 0.18),
  go: () => { tone(988, 0.35, 'square', 0.22); tone(1318, 0.35, 'sine', 0.12, 0.02); },
  setEnd: () => { tone(880, 0.16, 'triangle', 0.25); tone(660, 0.16, 'triangle', 0.25, 0.17); tone(523, 0.3, 'triangle', 0.25, 0.34); },
  rep: () => tone(1500, 0.05, 'square', 0.1),
  good: () => { tone(1046, 0.09, 'sine', 0.2); tone(1568, 0.14, 'sine', 0.18, 0.08); },
  miss: () => tone(220, 0.18, 'sawtooth', 0.12),
  pulse: () => tone(180, 0.08, 'sawtooth', 0.15),
  alarm: () => { for (let i = 0; i < 3; i++) tone(880, 0.14, 'sawtooth', 0.28, i * 0.18, 440); },
  click: () => tone(2000, 0.02, 'square', 0.06),
  lap: () => { tone(784, 0.1, 'sine', 0.2); tone(1175, 0.18, 'sine', 0.2, 0.1); },
};
