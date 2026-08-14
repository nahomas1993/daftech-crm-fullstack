import { Component, Input, computed, signal } from '@angular/core';

export interface BarChartDatum {
  label: string;
  value: number;
  color?: string;
}

/**
 * Minimal dependency-free horizontal bar chart. No charting library needed —
 * keeps the project installable without extra npm packages. Values are
 * shown as percentages (0-100) by default.
 */
@Component({
  selector: 'app-bar-chart',
  standalone: true,
  template: `
    <div class="chart">
      @for (d of data(); track d.label) {
        <div class="row">
          <div class="label">{{ d.label }}</div>
          <div class="track">
            <div class="fill" [style.width.%]="d.value" [style.background]="d.color ?? defaultColor"></div>
          </div>
          <div class="value">{{ d.value }}%</div>
        </div>
      }
      @if (data().length === 0) {
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
export class BarChartComponent {
  private readonly _data = signal<BarChartDatum[]>([]);
  @Input({ required: true }) set chartData(value: BarChartDatum[]) {
    this._data.set(value);
  }
  data = computed(() => this._data());

  readonly defaultColor = 'var(--accent)';
}
