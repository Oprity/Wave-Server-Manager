// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { sendWindowCommand } from '../utils/desktop';

function WaveMark() {
  return (
    <svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true">
      <rect width="14" height="14" rx="3" fill="#0b0b10" />
      <polyline
        points="2.5,4.5 5,9.5 7,6.5 9,9.5 11.5,4.5"
        fill="none"
        stroke="#10b981"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

const btn =
  'flex h-9 w-11 items-center justify-center text-zinc-300 hover:bg-zinc-700 hover:text-white';

/** Custom title bar for the chromeless desktop window (hidden in browsers). */
export default function TitleBar() {
  function onBarMouseDown(e: React.MouseEvent) {
    if (e.button !== 0) return;
    if ((e.target as HTMLElement).closest('button')) return;
    sendWindowCommand('drag');
  }

  function onBarDoubleClick(e: React.MouseEvent) {
    if ((e.target as HTMLElement).closest('button')) return;
    sendWindowCommand('maximize');
  }

  return (
    <div
      className="flex h-9 select-none items-stretch justify-between bg-[#101014]"
      onMouseDown={onBarMouseDown}
      onDoubleClick={onBarDoubleClick}
    >
      <div className="flex items-center gap-2 pl-3 text-xs text-zinc-300">
        <WaveMark />
        <span>Wave Server Manager</span>
      </div>
      <div className="flex">
        <button className={btn} aria-label="Minimize" onClick={() => sendWindowCommand('minimize')}>
          &#95;
        </button>
        <button className={btn} aria-label="Maximize" onClick={() => sendWindowCommand('maximize')}>
          &#9744;
        </button>
        <button
          className={`${btn} hover:!bg-red-600`}
          aria-label="Close"
          onClick={() => sendWindowCommand('close')}
        >
          &#10005;
        </button>
      </div>
    </div>
  );
}
