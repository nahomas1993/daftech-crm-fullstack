import { Component, Input, computed, signal } from '@angular/core';

export interface LineSeries {
  label: string;
  color: string;
  /** One value per point in LineChartComponent.xLabels — same length and order. Null for a point this series has no data for (e.g. on-time rate before any ticket resolved that month) — rendered as a gap, not zero. */
  values: (number | null)[];
}

const WIDTH = 600;
const HEIGHT = 200;
const PADDING = 28;

/**
 * Minimal dependency-free multi-series SVG line chart, for the Dashboard's
 * monthly trend (tickets / resolved / on-time rate). No charting library
 * needed — a sibling to BarChartComponent/DonutChartComponent, which took
 * the same no-dependency approach for the same reason (installability).
 */
@Component({
  selector: 'app-line-chart',
  standalone: true,
  template: `
    <div class="wrap">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" [style.width.%]="100" [style.height.px]="height">
        <!-- Horizontal gridlines -->
        @for (gy of gridLines(); track gy) {
          <line [attr.x1]="padding" [attr.x2]="width - padding" [attr.y1]="gy" [attr.y2]="gy" class="grid-line" />
        }

        @for (s of renderedSeries(); track s.label) {
          <polyline [attr.points]="s.points" fill="none" [attr.stroke]="s.color" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" />
          @for (p of s.dots; track p.x) {
            <circle [attr.cx]="p.x" [attr.cy]="p.y" r="3" [attr.fill]="s.color" />
          }
        }

        @for (lbl of xLabelPositions(); track lbl.x) {
          <text [attr.x]="lbl.x" [attr.y]="height - 6" text-anchor="middle" class="x-label">{{ lbl.text }}</text>
        }
      </svg>
      <div class="legend">
        @for (s of series(); track s.label) {
          <div class="legend-row">
            <span class="dot" [style.background]="s.color"></span>
            <span class="legend-label">{{ s.label }}</span>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .wrap { display: flex; flex-direction: column; gap: 0.75rem; }
    .grid-line { stroke: var(--slate-100); stroke-width: 1; }
    .x-label { font-size: 9px; fill: var(--slate-500); }
    .legend { display: flex; gap: 1rem; flex-wrap: wrap; }
    .legend-row { display: flex; align-items: center; gap: 0.4rem; font-size: 0.78rem; color: var(--navy-800); }
    .dot { width: 9px; height: 9px; border-radius: 3px; flex-shrink: 0; }
  `],
})
export class LineChartComponent {
  readonly width = WIDTH;
  readonly height = HEIGHT;
  readonly padding = PADDING;

  private readonly _series = signal<LineSeries[]>([]);
  @Input({ required: true }) set data(value: LineSeries[]) {
    this._series.set(value);
  }
  series = computed(() => this._series());

  @Input({ required: true }) xLabels: string[] = [];

  private readonly maxValue = computed(() => {
    const all = this._series().flatMap(s => s.values).filter((v): v is number => v != null);
    return all.length > 0 ? Math.max(...all, 1) : 1;
  });

  private xFor(index: number): number {
    const count = this.xLabels.length;
    if (count <= 1) return this.width / 2;
    const usableWidth = this.width - this.padding * 2;
    return this.padding + (usableWidth * index) / (count - 1);
  }

  private yFor(value: number): number {
    const usableHeight = this.height - this.padding * 2;
    const max = this.maxValue();
    return this.padding + usableHeight * (1 - value / max);
  }

  gridLines = computed(() => {
    const usableHeight = this.height - this.padding * 2;
    return [0, 0.25, 0.5, 0.75, 1].map(f => this.padding + usableHeight * f);
  });

  xLabelPositions = computed(() =>
    this.xLabels.map((text, i) => ({ text, x: this.xFor(i) }))
  );

  renderedSeries = computed(() =>
    this._series().map(s => {
      const dots: { x: number; y: number }[] = [];
      const segments: string[] = [];

      s.values.forEach((v, i) => {
        if (v == null) return;
        const x = this.xFor(i);
        const y = this.yFor(v);
        dots.push({ x, y });
        segments.push(`${x},${y}`);
      });

      return { label: s.label, color: s.color, points: segments.join(' '), dots };
    })
  );
}
