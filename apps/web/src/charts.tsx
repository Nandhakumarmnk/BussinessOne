// Lightweight, dependency-free SVG charts for the dashboard.
// Responsive via viewBox; styling driven by the design tokens in index.css.

import type { CategorySlice, TrendPoint } from "./types";

let gid = 0;
const nextId = (p: string) => `${p}-${++gid}`;

/** Compact INR for axes/labels: ₹1.9L, ₹84k, ₹560. */
export function compactINR(n: number): string {
  const a = Math.abs(n);
  if (a >= 1e7) return `₹${(n / 1e7).toFixed(a >= 1e8 ? 0 : 1)}Cr`;
  if (a >= 1e5) return `₹${(n / 1e5).toFixed(a >= 1e6 ? 0 : 1)}L`;
  if (a >= 1e3) return `₹${Math.round(n / 1e3)}k`;
  return `₹${Math.round(n)}`;
}

/** Catmull-Rom → cubic Bézier for a smooth line through the given points. */
function smooth(points: Array<[number, number]>): string {
  if (points.length < 2) return points.length ? `M ${points[0][0]},${points[0][1]}` : "";
  const d = [`M ${points[0][0]},${points[0][1]}`];
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[i - 1] ?? points[i];
    const p1 = points[i];
    const p2 = points[i + 1];
    const p3 = points[i + 2] ?? p2;
    const c1x = p1[0] + (p2[0] - p0[0]) / 6;
    const c1y = p1[1] + (p2[1] - p0[1]) / 6;
    const c2x = p2[0] - (p3[0] - p1[0]) / 6;
    const c2y = p2[1] - (p3[1] - p1[1]) / 6;
    d.push(`C ${c1x.toFixed(1)},${c1y.toFixed(1)} ${c2x.toFixed(1)},${c2y.toFixed(1)} ${p2[0]},${p2[1]}`);
  }
  return d.join(" ");
}

/* -------------------------------------------------------------------------- */
/* Trend area chart — income vs expense over time                             */
/* -------------------------------------------------------------------------- */
export function TrendChart({ data }: { data: TrendPoint[] }) {
  const W = 620, H = 260;
  const padL = 52, padR = 16, padT = 18, padB = 34;
  const iw = W - padL - padR, ih = H - padT - padB;

  const max = Math.max(1, ...data.map((d) => Math.max(d.income, d.expense)));
  const niceMax = niceCeil(max);
  const x = (i: number) => padL + (data.length <= 1 ? iw / 2 : (i / (data.length - 1)) * iw);
  const y = (v: number) => padT + ih - (v / niceMax) * ih;

  const incPts = data.map((d, i) => [x(i), y(d.income)] as [number, number]);
  const expPts = data.map((d, i) => [x(i), y(d.expense)] as [number, number]);
  const incLine = smooth(incPts);
  const expLine = smooth(expPts);
  const baseY = padT + ih;
  const incArea = `${incLine} L ${x(data.length - 1)},${baseY} L ${x(0)},${baseY} Z`;

  const incGrad = nextId("inc");
  const gridLevels = 4;

  return (
    <div className="chart">
      <svg viewBox={`0 0 ${W} ${H}`} width="100%" role="img" aria-label="Income versus expense trend">
        <defs>
          <linearGradient id={incGrad} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--brand-500)" stopOpacity="0.28" />
            <stop offset="100%" stopColor="var(--brand-500)" stopOpacity="0" />
          </linearGradient>
        </defs>

        {/* gridlines + y labels */}
        {Array.from({ length: gridLevels + 1 }).map((_, i) => {
          const gy = padT + (i / gridLevels) * ih;
          const val = niceMax * (1 - i / gridLevels);
          return (
            <g key={i}>
              <line x1={padL} y1={gy} x2={W - padR} y2={gy} stroke="var(--line)" strokeWidth="1" />
              <text x={padL - 10} y={gy + 4} textAnchor="end" className="chart__axis">{compactINR(val)}</text>
            </g>
          );
        })}

        {/* x labels */}
        {data.map((d, i) => (
          <text key={i} x={x(i)} y={H - 12} textAnchor="middle" className="chart__axis">{d.label}</text>
        ))}

        {/* income area + lines */}
        <path d={incArea} fill={`url(#${incGrad})`} />
        <path d={incLine} fill="none" stroke="var(--brand-600)" strokeWidth="2.5"
              strokeLinecap="round" strokeLinejoin="round" />
        <path d={expLine} fill="none" stroke="var(--neg-500)" strokeWidth="2.5"
              strokeDasharray="2 6" strokeLinecap="round" strokeLinejoin="round" />

        {/* income dots */}
        {incPts.map((p, i) => (
          <circle key={i} cx={p[0]} cy={p[1]} r="3" fill="#fff" stroke="var(--brand-600)" strokeWidth="2" />
        ))}
      </svg>

      <div className="chart__legend">
        <span className="chart__key"><i className="dotmark" style={{ background: "var(--brand-600)" }} />Income</span>
        <span className="chart__key"><i className="dotmark" style={{ background: "var(--neg-500)" }} />Expense</span>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Donut chart — expense breakdown                                            */
/* -------------------------------------------------------------------------- */
const PALETTE = ["#2563eb", "#10b981", "#f59e0b", "#8b5cf6", "#f43f5e", "#06b6d4"];

export function DonutChart({ data, centerLabel }: { data: CategorySlice[]; centerLabel?: string }) {
  const total = data.reduce((s, d) => s + d.value, 0) || 1;
  const size = 190, stroke = 26, r = (size - stroke) / 2, c = 2 * Math.PI * r, cx = size / 2;
  let offset = 0;

  return (
    <div className="donut">
      <svg viewBox={`0 0 ${size} ${size}`} className="donut__svg" role="img" aria-label="Expense breakdown">
        <circle cx={cx} cy={cx} r={r} fill="none" stroke="var(--line-soft)" strokeWidth={stroke} />
        {data.map((d, i) => {
          const frac = d.value / total;
          const seg = frac * c;
          const el = (
            <circle
              key={i} cx={cx} cy={cx} r={r} fill="none"
              stroke={PALETTE[i % PALETTE.length]} strokeWidth={stroke}
              strokeDasharray={`${seg} ${c - seg}`} strokeDashoffset={-offset}
              transform={`rotate(-90 ${cx} ${cx})`} strokeLinecap="butt"
            />
          );
          offset += seg;
          return el;
        })}
        <text x={cx} y={cx - 4} textAnchor="middle" className="donut__total">{compactINR(total)}</text>
        <text x={cx} y={cx + 15} textAnchor="middle" className="donut__caption">{centerLabel ?? "Total"}</text>
      </svg>

      <div className="donut__legend">
        {data.map((d, i) => (
          <div className="donut__item" key={i}>
            <i className="dotmark" style={{ background: PALETTE[i % PALETTE.length] }} />
            <span className="donut__name">{d.label}</span>
            <span className="donut__pct">{Math.round((d.value / total) * 100)}%</span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Sparkline — tiny trend for KPI cards                                       */
/* -------------------------------------------------------------------------- */
export function Sparkline({ data, tone = "var(--brand-600)" }: { data: number[]; tone?: string }) {
  const W = 108, H = 34, pad = 3;
  if (data.length < 2) return <svg viewBox={`0 0 ${W} ${H}`} className="spark" />;
  const min = Math.min(...data), max = Math.max(...data);
  const span = max - min || 1;
  const x = (i: number) => pad + (i / (data.length - 1)) * (W - pad * 2);
  const y = (v: number) => pad + (1 - (v - min) / span) * (H - pad * 2);
  const pts = data.map((v, i) => [x(i), y(v)] as [number, number]);
  const line = smooth(pts);
  const grad = nextId("spk");
  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="spark" preserveAspectRatio="none">
      <defs>
        <linearGradient id={grad} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={tone} stopOpacity="0.24" />
          <stop offset="100%" stopColor={tone} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={`${line} L ${W - pad},${H} L ${pad},${H} Z`} fill={`url(#${grad})`} />
      <path d={line} fill="none" stroke={tone} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* round a max value up to a clean axis bound */
function niceCeil(v: number): number {
  const pow = Math.pow(10, Math.floor(Math.log10(v)));
  const n = v / pow;
  const step = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10;
  return step * pow;
}
