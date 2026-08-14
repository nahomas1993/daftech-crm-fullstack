import { Component, Input, computed, signal } from '@angular/core';

export interface DonutSlice {
  label: string;
  value: number;
  color: string;
}

interface RenderedSlice extends DonutSlice {
  dashArray: string;
  dashOffset: number;
  percent: number;
}

const RADIUS = 60;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

/**
 * Minimal dependency-free donut chart built from stacked SVG circle strokes.
 * No charting library required.
 */
@Component({
  selector: 'app-donut-chart',
  standalone: true,
  template: `
    <div class="wrap">
      <svg viewBox="0 0 140 140" width="140" height="140">
        <circle cx="70" cy="70" [attr.r]="radius" fill="none" stroke="var(--slate-100)" stroke-width="16" />
        @for (s of slices(); track s.label) {
          <circle
            cx="70" cy="70" [attr.r]="radius" fill="none"
            [attr.stroke]="s.color" stroke-width="16"
            [attr.stroke-dasharray]="s.dashArray"
            [attr.stroke-dashoffset]="s.dashOffset"
            transform="rotate(-90 70 70)"
            stroke-linecap="butt"
          />
        }
        <text x="70" y="66" text-anchor="middle" class="center-value">{{ centerValue() }}%</text>
        <text x="70" y="84" text-anchor="middle" class="center-label">{{ centerLabel }}</text>
      </svg>
      <div class="legend">
        @for (s of slices(); track s.label) {
          <div class="legend-row">
            <span class="dot" [style.background]="s.color"></span>
            <span class="legend-label">{{ s.label }}</span>
            <span class="legend-value">{{ s.value }} ({{ s.percent }}%)</span>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .wrap { display: flex; align-items: center; gap: 1.5rem; flex-wrap: wrap; }
    .center-value { font-size: 1.15rem; font-weight: 700; fill: var(--navy-900); }
    .center-label { font-size: 0.55rem; fill: var(--slate-500); text-transform: uppercase; letter-spacing: 0.04em; }
    .legend { display: flex; flex-direction: column; gap: 0.5rem; }
    .legend-row { display: flex; align-items: center; gap: 0.5rem; font-size: 0.82rem; }
    .dot { width: 10px; height: 10px; border-radius: 3px; flex-shrink: 0; }
    .legend-label { color: var(--navy-800); }
    .legend-value { color: var(--slate-500); margin-left: auto; padding-left: 0.75rem; }
  `],
})
export class DonutChartComponent {
  readonly radius = RADIUS;

  private readonly _slicesInput = signal<DonutSlice[]>([]);
  @Input({ required: true }) set data(value: DonutSlice[]) {
    this._slicesInput.set(value);
  }

  /** Value shown in the center of the ring. Defaults to the first slice's share of the total. */
  @Input() centerOverride: number | null = null;
  @Input() centerLabel = 'On Time';

  private readonly total = computed(() => this._slicesInput().reduce((sum, s) => sum + s.value, 0));

  slices = computed((): RenderedSlice[] => {
    const total = this.total();
    if (total === 0) return [];

    let offsetAccum = 0;
    return this._slicesInput().map(s => {
      const fraction = s.value / total;
      const arcLength = fraction * CIRCUMFERENCE;
      const rendered: RenderedSlice = {
        ...s,
        dashArray: `${arcLength} ${CIRCUMFERENCE - arcLength}`,
        dashOffset: -offsetAccum,
        percent: Math.round(fraction * 100),
      };
      offsetAccum += arcLength;
      return rendered;
    });
  });

  centerValue = computed(() => {
    if (this.centerOverride != null) return this.centerOverride;
    const first = this._slicesInput()[0];
    const total = this.total();
    return first && total > 0 ? Math.round((first.value / total) * 100) : 0;
  });
}
