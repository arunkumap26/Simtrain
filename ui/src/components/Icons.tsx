const S = { fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const };

export const NavIcon = ({ name }: { name: string }) => {
  switch (name) {
    case 'home': return <svg viewBox="0 0 24 24" {...S}><path d="M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6h-6v6H4a1 1 0 0 1-1-1z" /></svg>;
    case 'train': return <svg viewBox="0 0 24 24" {...S}><circle cx="12" cy="12" r="9" /><circle cx="12" cy="12" r="2.5" /><path d="M3 12h6.5M14.5 12H21M12 14.5V21" /></svg>;
    case 'drills': return <svg viewBox="0 0 24 24" {...S}><rect x="3" y="3" width="7" height="7" rx="2" /><rect x="14" y="3" width="7" height="7" rx="2" /><rect x="3" y="14" width="7" height="7" rx="2" /><rect x="14" y="14" width="7" height="7" rx="2" /></svg>;
    case 'program': return <svg viewBox="0 0 24 24" {...S}><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M3 9h18M8 2v4M16 2v4M7 13h3M7 17h3M14 13h3" /></svg>;
    case 'max': return <svg viewBox="0 0 24 24" {...S}><path d="M4 16a8 8 0 1 1 16 0" /><path d="M12 16l4.5-5" /><circle cx="12" cy="16" r="1.5" /><path d="M7 20h10" /></svg>;
    case 'progress': return <svg viewBox="0 0 24 24" {...S}><path d="M3 20h18M5 16l4-5 4 3 6-8" /><circle cx="19" cy="6" r="1.5" /></svg>;
    case 'device': return <svg viewBox="0 0 24 24" {...S}><rect x="4" y="6" width="16" height="12" rx="3" /><circle cx="12" cy="12" r="3" /><path d="M8 3v3M16 3v3M8 18v3M16 18v3" /></svg>;
    case 'settings': return <svg viewBox="0 0 24 24" {...S}><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z" /></svg>;
    default: return null;
  }
};

/** Drill glyphs: small animated wheels that hint at the resistance profile. */
export const DrillIcon = ({ name, color = 'currentColor', size = 44 }: { name: string; color?: string; size?: number }) => {
  const wheel = (anim?: string) => (
    <g style={{ transformOrigin: '24px 24px', animation: anim }}>
      <circle cx="24" cy="24" r="15" stroke={color} strokeWidth="3.5" fill="none" />
      <path d="M9.5 24h29M24 24v15" stroke={color} strokeWidth="3" strokeLinecap="round" />
      <path d="M24 9v4" stroke="#f43f5e" strokeWidth="3.5" />
    </g>
  );
  const ghost = (deg: number) => <circle cx="24" cy="24" r="15" stroke="#cbd5e1" strokeOpacity="0.3" strokeDasharray="4 4" strokeWidth="3" fill="none" transform={`rotate(${deg} 24 24)`} />;
  const arrow = (d: string) => <path d={d} stroke={color} strokeWidth="2.5" fill="none" strokeLinecap="round" />;
  let extra: JSX.Element | null = null;
  let anim: string | undefined;
  switch (name) {
    case 'hold': anim = 'rf-hold 3s ease-in-out infinite'; extra = ghost(35); break;
    case 'hand': extra = <><circle cx="39" cy="24" r="5" fill={color} /><circle cx="9" cy="24" r="4" fill="#334155" /></>; break;
    case 'grip': extra = <path d="M35 17c4 2 6 6 5 10" stroke={color} strokeWidth="3" fill="none" strokeLinecap="round" />; break;
    case 'sprint': anim = 'rf-sprint 0.9s ease-in-out infinite alternate'; extra = <>{arrow('M4 8l-2 4 4 1')}{arrow('M44 8l2 4-4 1')}</>; break;
    case 'return': anim = 'rf-return 3.2s ease-in-out infinite'; extra = arrow('M38 6a20 20 0 0 1 6 10'); break;
    case 'snap': anim = 'rf-snap 1.6s steps(1) infinite'; extra = <path d="M40 4l-4 8h5l-5 9" stroke="#f472b6" strokeWidth="2.5" fill="none" />; break;
    case 'kerb': extra = <path d="M2 44h6l2-3 2 3 2-3 2 3 2-3 2 3 2-3 2 3 2-3 2 3h8" stroke="#f43f5e" strokeWidth="2" fill="none" />; anim = 'rf-kerb 0.12s linear infinite alternate'; break;
    case 'stint': extra = <path d="M2 40c8-8 6-2 14-6s4-14 14-10 4 10 16 6" stroke="#fbbf24" strokeWidth="2" fill="none" strokeDasharray="3 3" />; anim = 'rf-hold 5s ease-in-out infinite'; break;
    case 'test': extra = <path d="M6 42h36M10 42V30M18 42V24M26 42V18M34 42V10" stroke="#f43f5e" strokeWidth="2" opacity="0.5" />; break;
    case 'iso': anim = 'rf-iso 4s linear infinite'; extra = ghost(-30); break;
    case 'apex': extra = <><circle cx="38" cy="10" r="5" stroke="#f472b6" strokeWidth="2.5" fill="none" /><circle cx="38" cy="10" r="1.5" fill="#f472b6" /></>; anim = 'rf-apex 2.4s ease-in-out infinite'; break;
    case 'wave': extra = <path d="M2 44c4-6 8-6 12 0s8 6 12 0 8-6 12 0 6 4 8 2" stroke={color} strokeWidth="2" fill="none" />; break;
    case 'free': anim = 'rf-free 6s ease-in-out infinite'; break;
  }
  return (
    <svg viewBox="0 0 48 48" width={size} height={size} style={{ overflow: 'visible' }}>
      <style>{`
        @keyframes rf-hold { 0%,100% { transform: rotate(30deg) } 50% { transform: rotate(38deg) } }
        @keyframes rf-sprint { from { transform: rotate(-80deg) } to { transform: rotate(80deg) } }
        @keyframes rf-return { 0% { transform: rotate(0) } 25%,40% { transform: rotate(80deg) } 90%,100% { transform: rotate(0) } }
        @keyframes rf-snap { 0% { transform: rotate(0) } 20% { transform: rotate(14deg) } 30% { transform: rotate(3deg) } 60% { transform: rotate(-12deg) } 70% { transform: rotate(0) } }
        @keyframes rf-kerb { from { transform: rotate(-2deg) } to { transform: rotate(2deg) } }
        @keyframes rf-iso { 0% { transform: rotate(-60deg) } 50% { transform: rotate(60deg) } 100% { transform: rotate(-60deg) } }
        @keyframes rf-apex { 0%,20% { transform: rotate(0) } 45%,70% { transform: rotate(48deg) } 100% { transform: rotate(0) } }
        @keyframes rf-free { 0% { transform: rotate(0) } 30% { transform: rotate(120deg) } 60% { transform: rotate(-70deg) } 100% { transform: rotate(0) } }
      `}</style>
      {extra}
      {wheel(anim)}
    </svg>
  );
};
