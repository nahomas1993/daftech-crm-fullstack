import { Component, Input, computed, signal } from '@angular/core';

export interface CountBarDatum {
  label: string;
  value: number;
  color?: string;
}

/**
 * Minimal dependency-free horizontal bar chart for raw counts (tickets by
 * region, failure type, employee) — a sibling to BarChartComponent, which
 * is hardcoded to 0-100 percentage semantics and would misrepresent a
 * count like "47 tickets" as a near-full bar. Bar widths here are
 * normalized against the largest value in the current dataset, computed
 * client-side, so no charting library is needed.
 */
@Component({
  selector: 'app-count-bar-chart',
  standalone: true,
  template: `
    <div class="chart">
      @for (d of bars(); track d.label) {
        <div class="row">
          <div class="label">{{ d.label }}</div>
          <div class="track">
            <div class="fill" [style.width.%]="d.widthPercent" [style.background]="d.color ?? defaultColor"></div>
          </div>
          <div class="value">{{ d.value }}</div>
        </div>
      }
      @if (bars().length === 0) {
        <div class="empty">No data yet.</div>
      }
    </div>
  `,
  styles: [`
    .chart { display: flex; flex-direction: column; gap: 0.7rem; }
    .row { display: grid; grid-template-columns: 130px 1fr 44px; align-items: center; gap: 0.7rem; }
    .label { font-size: 0.82rem; color: var(--navy-800); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .track { background: var(--slate-100); border-radius: 999px; height: 10px; overflow: hidden; }
    .fill { height: 100%; border-radius: 999px; transition: width 0.3s ease; }
    .value { font-size: 0.78rem; color: var(--slate-500); text-align: right; font-weight: 600; }
    .empty { color: var(--slate-500); font-size: 0.85rem; text-align: center; padding: 1rem; }
  `],
})
export class CountBarChartComponent {
  private readonly _data = signal<CountBarDatum[]>([]);
  @Input({ required: true }) set chartData(value: CountBarDatum[]) {
    this._data.set(value);
  }

  /** Cap the number of bars shown — defaults to 8 so a long tail (e.g. many failure types) doesn't overwhelm a dashboard card. Pass 0 for no limit. */
  @Input() maxBars = 8;

  readonly defaultColor = 'var(--accent)';

  private readonly maxValue = computed(() => Math.max(1, ...this._data().map(d => d.value)));

  bars = computed(() => {
    const data = this.maxBars > 0 ? this._data().slice(0, this.maxBars) : this._data();
    const max = this.maxValue();
    return data.map(d => ({ ...d, widthPercent: Math.round((d.value / max) * 100) }));
  });
}
