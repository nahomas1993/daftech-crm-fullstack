import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { EmployeeService } from '../../core/services/employee.service';
import { ReportService } from '../../core/services/report.service';
import { EmployeePerformanceReport } from '../../core/models';

@Component({
  selector: 'app-employee-performance',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <h1>Employee Performance</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Attendance, ticket outcomes, and satisfaction — combined per employee.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="picker-row">
        <select [ngModel]="selectedId()" (ngModelChange)="selectedId.set($event)">
          @for (e of employees.employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
        </select>
        <button class="btn btn-primary btn-sm" [disabled]="loading()" (click)="load()">
          {{ loading() ? 'Loading…' : 'View Metrics' }}
        </button>
      </div>
    </div>

    @if (report(); as r) {
      <div class="metrics-grid" style="margin-top:1.25rem;">
        <div class="panel panel-pad metric"><div class="metric-label">Tickets Assigned</div><div class="metric-value">{{ r.ticketsAssigned }}</div></div>
        <div class="panel panel-pad metric"><div class="metric-label">Tickets Resolved</div><div class="metric-value">{{ r.ticketsResolved }}</div></div>
        <div class="panel panel-pad metric"><div class="metric-label">On-Time Rate</div><div class="metric-value">{{ r.onTimeRate }}%</div></div>
        <div class="panel panel-pad metric"><div class="metric-label">Avg. Resolution Time</div><div class="metric-value">{{ r.averageResolutionHours != null ? (r.averageResolutionHours | number:'1.0-1') + ' h' : '—' }}</div></div>
        <div class="panel panel-pad metric"><div class="metric-label">Avg. Satisfaction</div><div class="metric-value">{{ r.averageSatisfactionScore != null ? (r.averageSatisfactionScore | number:'1.0-0') + '/100' : '—' }}</div></div>
        <div class="panel panel-pad metric"><div class="metric-label">Total Hours Worked</div><div class="metric-value">{{ r.totalHoursWorked | number:'1.0-1' }}</div></div>
      </div>
    }
  `,
  styles: [`
    .picker-row { display: flex; gap: 0.6rem; align-items: center; flex-wrap: wrap; }
    .picker-row select { flex: 1; min-width: 200px; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 1rem; }
    .metric-label { font-size: 0.75rem; color: var(--slate-500); font-weight: 600; margin-bottom: 0.4rem; }
    .metric-value { font-size: 1.5rem; font-weight: 700; color: var(--navy-900); }
  `],
})
export class EmployeePerformanceComponent {
  selectedId = signal('');
  report = signal<EmployeePerformanceReport | null>(null);
  loading = signal(false);

  constructor(public employees: EmployeeService, private reports: ReportService) {}

  async load() {
    if (!this.selectedId()) return;
    this.loading.set(true);
    try {
      this.report.set(await this.reports.getEmployeePerformanceReport(this.selectedId()));
    } finally {
      this.loading.set(false);
    }
  }
}
