import type { Strength } from '../types';
import { effortColor } from './canvas';

function heat(pct: number) { return effortColor(pct / 100); }

/** Front view of the driver: each link glows by % of a typical person's isometric max (MVC). */
export function BodyMap({ s, hand, technique }: { s: Strength | null | undefined; hand?: string | null; technique: string }) {
  const sh = s?.shoulderPct ?? 0, wr = s?.wristPct ?? 0, gr = s?.gripPct ?? 0, tw = s?.forearmTwistPct ?? 0;
  const left = !hand || hand === 'both' || hand === 'left';
  const right = !hand || hand === 'both' || hand === 'right';
  const wristMode = technique === 'wrists';
  const glow = (pct: number) => `drop-shadow(0 0 ${4 + Math.min(14, pct / 6)}px ${heat(pct)})`;
  const limb = (on: boolean, pct: number) => ({ stroke: on ? heat(pct) : '#273244', opacity: on ? 1 : 0.5, filter: on ? glow(pct) : 'none' });

  return (
    <svg viewBox="0 0 300 215" style={{ width: '100%', height: 'auto', display: 'block' }}>
      <defs>
        <linearGradient id="torso" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#1b2636" /><stop offset="1" stopColor="#0c121b" />
        </linearGradient>
      </defs>
      {/* torso & head */}
      <path d="M 100 76 Q 150 62 200 76 L 188 175 Q 150 186 112 175 Z" fill="url(#torso)" stroke="#2a3749" />
      <circle cx="150" cy="36" r="21" fill="#141d2a" stroke="#2a3749" />
      <rect x="140" y="55" width="20" height="14" rx="5" fill="#141d2a" />
      {/* wheel */}
      <circle cx="150" cy="168" r="54" fill="none" stroke="#334155" strokeWidth="7" opacity="0.9" />
      <circle cx="150" cy="168" r="54" fill="none" stroke="#475569" strokeWidth="1" />
      {/* upper arms (shoulder load) */}
      <line x1="98" y1="82" x2="70" y2="136" strokeWidth="13" strokeLinecap="round" {...limb(left, sh)} />
      <line x1="202" y1="82" x2="230" y2="136" strokeWidth="13" strokeLinecap="round" {...limb(right, sh)} />
      {/* forearms (twist / wrist) */}
      <line x1="70" y1="136" x2="96" y2="166" strokeWidth="11" strokeLinecap="round" strokeDasharray={wristMode ? undefined : '0'} {...limb(left, wristMode ? tw : wr)} />
      <line x1="230" y1="136" x2="204" y2="166" strokeWidth="11" strokeLinecap="round" {...limb(right, wristMode ? tw : wr)} />
      {/* shoulders */}
      {[98, 202].map((x, i) => {
        const on = i === 0 ? left : right;
        return <circle key={x} cx={x} cy={82} r="14" fill={on ? heat(sh) : '#1e293b'} opacity={on ? 0.95 : 0.4} style={{ filter: on ? glow(sh) : 'none' }} />;
      })}
      {/* hands (grip) */}
      {[96, 204].map((x, i) => {
        const on = i === 0 ? left : right;
        return <rect key={x} x={x - 11} y={157} width="22" height="22" rx="7" fill={on ? heat(gr) : '#1e293b'} opacity={on ? 1 : 0.35} style={{ filter: on ? glow(gr) : 'none' }} />;
      })}
      {/* labels */}
      <g fontFamily="Barlow Condensed" fontWeight="700" fontSize="13" fill="#cbd5e1">
        <text x="6" y="70">SHOULDER</text>
        <text x="6" y="86" fontFamily="JetBrains Mono" fontSize="14" fill={heat(sh)}>{sh.toFixed(0)}%</text>
        <text x="6" y="122">{wristMode ? 'FOREARM' : 'WRIST'}</text>
        <text x="6" y="138" fontFamily="JetBrains Mono" fontSize="14" fill={heat(wristMode ? tw : wr)}>{(wristMode ? tw : wr).toFixed(0)}%</text>
        <text x="242" y="174">GRIP</text>
        <text x="242" y="190" fontFamily="JetBrains Mono" fontSize="14" fill={heat(gr)}>{gr.toFixed(0)}%</text>
      </g>
    </svg>
  );
}

export function LinkBars({ s, technique }: { s: Strength | null | undefined; technique: string }) {
  const rows: [string, number, string][] = [
    ['Shoulder (lifting arm)', s?.shoulderPct ?? 0, `${(s?.shoulderNm ?? 0).toFixed(1)} Nm`],
    ['Wrist (radial dev.)', s?.wristPct ?? 0, `${(s?.wristNm ?? 0).toFixed(1)} Nm`],
    ['Grip squeeze', s?.gripPct ?? 0, `${(s?.gripKgf ?? 0).toFixed(1)} kgf`],
    [technique === 'wrists' ? 'Forearm twist' : 'If you only twisted', s?.forearmTwistPct ?? 0, ''],
  ];
  return (
    <div className="col" style={{ gap: 7 }}>
      {rows.map(([label, v, sub], i) => {
        const limiting = s && ((i === 0 && s.limitingLink === 'shoulder') || (i === 1 && s.limitingLink === 'wrist') || (i === 2 && s.limitingLink === 'grip') || (i === 3 && s.limitingLink === 'forearm twist'));
        return (
          <div key={label} style={{ opacity: i === 3 && technique !== 'wrists' ? 0.55 : 1 }}>
            <div className="row" style={{ justifyContent: 'space-between', fontSize: 12 }}>
              <span className={limiting ? '' : 'muted'}>{limiting ? '★ ' : ''}{label}</span>
              <span className="mono" style={{ color: effortColor(v / 100) }}>{v.toFixed(0)}% <span className="dim">{sub}</span></span>
            </div>
            <div className="bar"><i style={{ width: `${Math.min(100, v)}%`, background: effortColor(v / 100) }} /></div>
          </div>
        );
      })}
    </div>
  );
}
