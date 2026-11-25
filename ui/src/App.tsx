import { useEffect, useRef, useState } from 'react';
import { configureAudio, say, sfx } from './audio';
import { getTick, post, send, useAppState, useConnected, useEvent, useTick } from './bridge';
import { NavIcon } from './components/Icons';
import { Device } from './pages/Device';
import { Drills } from './pages/Drills';
import { Home } from './pages/Home';
import { Onboarding } from './pages/Onboarding';
import { ProgramPage } from './pages/Program';
import { Progress } from './pages/Progress';
import { SettingsPage } from './pages/Settings';
import { Train } from './pages/Train';

const PAGES = [
  ['home', 'Home'], ['train', 'Train'], ['drills', 'Drills'], ['program', 'Program'], ['progress', 'Progress'], ['device', 'Device'], ['settings', 'Settings'],
] as const;
export type PageId = (typeof PAGES)[number][0];

interface Toast { id: number; text: string; kind?: 'good' | 'bad' }

export function App() {
  const st = useAppState();
  const connected = useConnected();
  const initial = (new URLSearchParams(location.search).get('page') as PageId) || 'home';
  const [page, setPage] = useState<PageId>(initial);
  const [toasts, setToasts] = useState<Toast[]>([]);
  const prevSession = useRef<string | null>(null);

  const toast = (text: string, kind?: Toast['kind']) => {
    const id = Math.random();
    setToasts((t) => [...t.slice(-3), { id, text, kind }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 4200);
  };

  useEffect(() => {
    if (st) configureAudio({ enabled: st.settings.audioEnabled, volume: st.settings.audioVolume, voice: st.settings.voiceCues });
    const key = st?.session ? `${st.session.title}` : null;
    if (key && key !== prevSession.current) setPage('train');
    prevSession.current = key;
  }, [st]);

  useEvent((kind, data) => {
    switch (kind) {
      case 'countdown': sfx.countdown(data.n); say(String(data.n)); break;
      case 'setStart': sfx.go(); say('Go'); break;
      case 'setEnd':
        sfx.setEnd();
        say(data.completed ? 'Set complete' : 'Set stopped');
        toast(`${data.drillName} · set ${data.setIndex + 1}: ${Number(data.score).toFixed(data.scoreLabel === 'Nm' ? 1 : 0)} ${data.scoreLabel}`, data.completed ? 'good' : undefined);
        break;
      case 'phase': if (data.phase === 'rest') say('Rest'); break;
      case 'rep': sfx.rep(); break;
      case 'pulse': sfx.pulse(); break;
      case 'catch': data.ok ? sfx.good() : sfx.miss(); break;
      case 'apex': sfx.good(); break;
      case 'miss': sfx.miss(); break;
      case 'target': sfx.click(); break;
      case 'lap': sfx.lap(); say(`Lap ${data.lap}. ${Math.round(data.accuracy)} percent`); break;
      case 'breakpoint': say(`Breakpoint ${Number(data.nm).toFixed(1)} newton metres`); toast(`Breakpoint: ${Number(data.nm).toFixed(2)} Nm${data.estimated ? ' (estimated from endurance)' : ''}`, 'good'); break;
      case 'breakpointCapped': toast(`Reached the safety cap at ${data.nm} Nm — keep holding! Your max will be estimated from how long you last.`); say('At the cap. Hold on.'); break;
      case 'safety':
        if (data.kind === 'tripped') { sfx.alarm(); say(data.message); toast(`Safety stop: ${data.message}`, 'bad'); }
        break;
      case 'sessionComplete': say('Session complete. Great work.'); break;
      case 'directionCheck': toast(`Direction check: ${data.result}${data.displacementDeg != null ? ` (${data.displacementDeg}°)` : ''}`, data.result === 'ok' || data.result === 'inverted' ? 'good' : 'bad'); break;
      case 'buttonBound': toast(`Wheel button ${data.button + 1} → ${data.action}`, 'good'); break;
      case 'intensity': break;
      case 'error': toast(String(data), 'bad'); break;
    }
  }, []);

  // keyboard: Space / Esc = STOP (always), Enter = start/next, arrows = resistance
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const typing = (e.target as HTMLElement)?.tagName === 'INPUT' && (e.target as HTMLInputElement).type !== 'range';
      if (e.key === 'Escape' || (e.key === ' ' && !typing)) { e.preventDefault(); post('stop', { reason: 'Key stop' }); return; }
      if (typing) return;
      const act: any = getTick()?.activity;
      if (e.key === 'Enter' && act?.kind === 'session') {
        e.preventDefault();
        if (act.phase === 'ready' || act.phase === 'paused') send('go').catch((err) => toast(err.message, 'bad'));
        else if (act.phase === 'rest') send('next');
      }
      if (e.key === 'ArrowUp') { e.preventDefault(); send('nudgeIntensity', { delta: 0.05 }); }
      if (e.key === 'ArrowDown') { e.preventDefault(); send('nudgeIntensity', { delta: -0.05 }); }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  if (!st) {
    return (
      <div style={{ display: 'grid', placeItems: 'center', height: '100vh' }}>
        <div className="col" style={{ alignItems: 'center' }}>
          <img src="./icon.png" width={84} style={{ filter: 'drop-shadow(0 0 30px rgba(34,211,238,0.4))' }} />
          <div className="head" style={{ fontSize: 28, letterSpacing: '0.2em' }}>RimForce</div>
          <div className="dim">{connected ? 'Loading…' : 'Connecting to the RimForce engine…'}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="shell">
      <nav className="rail">
        <img className="brand" src="./icon.png" alt="RimForce" />
        {PAGES.map(([id, label]) => (
          <button key={id} className={`nav-btn ${page === id ? 'active' : ''}`} onClick={() => setPage(id)} title={label}>
            <NavIcon name={id} />
            {label}
          </button>
        ))}
        <div className="spacer" />
        <div className="dim mono" style={{ fontSize: 10 }}>v{st.version}</div>
      </nav>
      <TopBar connected={connected} />
      <main className="main">
        {page === 'home' && <Home go={setPage} />}
        {page === 'train' && <Train go={(p) => setPage(p as PageId)} />}
        {page === 'drills' && <Drills go={setPage} />}
        {page === 'program' && <ProgramPage go={setPage} />}
        {page === 'progress' && <Progress />}
        {page === 'device' && <Device />}
        {page === 'settings' && <SettingsPage />}
      </main>
      {!st.settings.onboardingDone && <Onboarding />}
      <div className="toast-stack">
        {toasts.map((t) => <div key={t.id} className={`toast ${t.kind ?? ''}`}>{t.text}</div>)}
      </div>
    </div>
  );
}

function TopBar({ connected }: { connected: boolean }) {
  const st = useAppState();
  const t = useTick(8);
  const safety = t?.safety ?? 'Disarmed';
  const sessionTitle = st?.session?.title;
  return (
    <header className="topbar">
      <h1>RIM<span>FORCE</span></h1>
      {sessionTitle && <span className="dim" style={{ marginLeft: 6 }}>· {sessionTitle}</span>}
      <div className="spacer" />
      <span className={`pill ${connected && t?.connected ? 'ok' : 'warn'}`}>
        <span className="dot" />{!connected ? 'Engine offline' : t?.simulated ? 'Simulated wheel (demo)' : t?.deviceName ?? 'No wheel'}
      </span>
      <span className="pill mono" title="Force loop rate">{(t?.loopHz ?? 0).toFixed(0)} Hz</span>
      <span className="pill mono" title="Safety cap">CAP {(t?.globalCapNm ?? 0).toFixed(1)} Nm</span>
      <span className={`pill ${safety === 'Armed' ? 'armed' : safety === 'Tripped' ? 'bad' : ''}`}>
        <span className="dot" />{safety === 'Armed' ? 'ARMED' : safety === 'Tripped' ? 'SAFETY STOP' : 'Disarmed · no force'}
      </span>
      {safety === 'Tripped' && <button className="btn small" onClick={() => send('ackTrip')}>Acknowledge</button>}
      <button className="stop-btn" onClick={() => post('stop', { reason: 'STOP button' })}>STOP</button>
    </header>
  );
}
