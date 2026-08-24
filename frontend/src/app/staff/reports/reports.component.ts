import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe, DecimalPipe } from '@angular/common';
import { TicketReportService } from '../../core/services/ticket-report.service';
import { EmployeeService } from '../../core/services/employee.service';
import { FailureTypeService } from '../../core/services/failure-type.service';
import { LocationService } from '../../core/services/location.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import {
  ReportType, REPORT_TYPE_LABELS, TicketReportFilter, TableReportResult,
  CustomerSupportReportRow, EmployeePerformanceReportRow, RegionalReportRow,
  FailureTypeReportRow, ResolutionTimeReportRow, CustomerRatingReportRow,
  TicketStatus, SupportPhase,
} from '../../core/models';

const REPORT_TYPES: ReportType[] = ['customer-support', 'employee-performance', 'regional', 'failure-type', 'resolution-time', 'customer-rating'];
const STATUSES: TicketStatus[] = ['Submitted', 'Forwarded', 'Assigned', 'InProgress', 'Resolved', 'AwaitingClientConfirmation', 'Escalated', 'Closed'];
const PHASES: SupportPhase[] = ['Intake', 'Diagnosis', 'Repair', 'Verification', 'Closed'];
const MONTHS = [
  { value: 1, label: 'January' }, { value: 2, label: 'February' }, { value: 3, label: 'March' },
  { value: 4, label: 'April' }, { value: 5, label: 'May' }, { value: 6, label: 'June' },
  { value: 7, label: 'July' }, { value: 8, label: 'August' }, { value: 9, label: 'September' },
  { value: 10, label: 'October' }, { value: 11, label: 'November' }, { value: 12, label: 'December' },
];

/**
 * The Reports module — six table-only reports (Customer/Support, Employee
 * Performance, Regional, Failure-Type, Resolution-Time, Customer-Rating),
 * each filterable/searchable/paginated/printable/exportable. Deliberately
 * tables only, no charts — see the Dashboard page for charts/KPIs (the
 * product's Reports-vs-Dashboard split). One shared filter bar drives
 * whichever report is currently selected; switching report type re-fetches
 * with the same filter state, since a support manager typically wants to
 * compare the same slice of tickets across report types.
 */
@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, SlicePipe, DecimalPipe, BadgeComponent, PaginationComponent],
  template: `
    <h1>Reports</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Filterable, exportable tables — for charts and live KPIs, see the Dashboard.</p>

    <div class="tabs">
      @for (t of reportTypes; track t) {
        <button type="button" class="tab" [class.active]="activeType() === t" (click)="selectType(t)">
          {{ labelFor(t) }}
        </button>
      }
    </div>

    <div class="panel panel-pad filter-bar">
      <div class="filter-grid">
        <div class="field">
          <label>From</label>
          <input type="date" [ngModel]="filter.fromDate" (ngModelChange)="setFilter('fromDate', $event)" />
        </div>
        <div class="field">
          <label>To</label>
          <input type="date" [ngModel]="filter.toDate" (ngModelChange)="setFilter('toDate', $event)" />
        </div>
        <div class="field">
          <label>Month</label>
          <select [ngModel]="filter.month" (ngModelChange)="setFilter('month', $event)">
            <option [ngValue]="undefined">Any month</option>
            @for (m of months; track m.value) { <option [ngValue]="m.value">{{ m.label }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Region</label>
          <select [ngModel]="filter.region" (ngModelChange)="setFilter('region', $event)">
            <option [ngValue]="undefined">Any region</option>
            @for (r of locations.options().regions; track r.id) { <option [value]="r.name">{{ r.name }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Zone</label>
          <select [ngModel]="filter.zone" (ngModelChange)="setFilter('zone', $event)">
            <option [ngValue]="undefined">Any zone</option>
            @for (z of locations.options().zones; track z.id) { <option [value]="z.name">{{ z.name }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Woreda</label>
          <select [ngModel]="filter.woreda" (ngModelChange)="setFilter('woreda', $event)">
            <option [ngValue]="undefined">Any woreda</option>
            @for (w of locations.options().woredas; track w.id) { <option [value]="w.name">{{ w.name }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Employee / Technician</label>
          <select [ngModel]="filter.employeeId" (ngModelChange)="setFilter('employeeId', $event)">
            <option [ngValue]="undefined">Any employee</option>
            @for (e of employeesSvc.employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Failure Type</label>
          <select [ngModel]="filter.failureTypeId" (ngModelChange)="setFilter('failureTypeId', $event)">
            <option [ngValue]="undefined">Any failure type</option>
            @for (f of failureTypesSvc.types(); track f.id) { <option [value]="f.id">{{ f.name }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Ticket Status</label>
          <select [ngModel]="filter.status" (ngModelChange)="setFilter('status', $event)">
            <option [ngValue]="undefined">Any status</option>
            @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
          </select>
        </div>
        <div class="field">
          <label>Support Phase</label>
          <select [ngModel]="filter.supportPhase" (ngModelChange)="setFilter('supportPhase', $event)">
            <option [ngValue]="undefined">Any phase</option>
            @for (p of phases; track p) { <option [value]="p">{{ p }}</option> }
          </select>
        </div>
        <div class="field" style="grid-column: span 2;">
          <label>Search</label>
          <input type="text" [ngModel]="filter.search" (ngModelChange)="setFilter('search', $event)" placeholder="Client name, description, document #…" />
        </div>
      </div>

      <div class="filter-actions">
        <button class="btn btn-outline btn-sm" (click)="clearFilters()">Clear Filters</button>
        <div style="flex:1;"></div>
        <button class="btn btn-outline btn-sm" (click)="print()">Print</button>
        <button class="btn btn-outline btn-sm" [disabled]="exporting()" (click)="export('csv')">Export CSV</button>
        <button class="btn btn-primary btn-sm" [disabled]="exporting()" (click)="export('pdf')">Export PDF</button>
      </div>
    </div>

    <div class="panel panel-pad" id="report-table-panel">
      @if (loading()) {
        <p class="text-muted">Loading report…</p>
      } @else if (loadError()) {
        <p class="upload-error">{{ loadError() }}</p>
        <button class="btn btn-outline btn-sm" style="margin-top:0.75rem;" (click)="load()">Retry</button>
      } @else {
        <div class="table-scroll">
          @switch (activeType()) {
            @case ('customer-support') {
              <table>
                <thead><tr><th>Client</th><th>Region</th><th>Zone</th><th>Woreda</th><th>System/Product</th><th>Failure Type</th><th>Submitted</th><th>Assigned To</th><th>Status</th><th>Phase</th><th>Chargeable</th><th>Resolved</th><th>Satisfaction</th></tr></thead>
                <tbody>
                  @for (r of customerSupportRows(); track r.ticketId) {
                    <tr>
                      <td>{{ r.clientName }}</td><td>{{ r.region || '—' }}</td><td>{{ r.zone || '—' }}</td><td>{{ r.woreda || '—' }}</td>
                      <td>{{ r.systemProductName || '—' }}</td><td>{{ r.failureTypeName || '—' }}</td>
                      <td>{{ r.dateSubmitted | slice:0:10 }}</td><td>{{ r.assignedEmployeeName || 'Unassigned' }}</td>
                      <td><app-badge [status]="r.status"></app-badge></td><td>{{ r.supportPhase }}</td>
                      <td><app-badge [status]="r.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
                      <td>{{ r.resolvedAt ? (r.resolvedAt | slice:0:10) : '—' }}</td>
                      <td>{{ r.satisfactionScore ?? '—' }}</td>
                    </tr>
                  }
                  @empty { <tr><td colspan="13" class="text-muted">No tickets match these filters.</td></tr> }
                </tbody>
              </table>
            }
            @case ('employee-performance') {
              <table>
                <thead><tr><th>Employee</th><th>Total Assigned</th><th>Resolved</th><th>Open</th><th>Overdue</th><th>Avg Resolution (hrs)</th><th>On-Time %</th><th>Avg Satisfaction</th></tr></thead>
                <tbody>
                  @for (r of employeePerformanceRows(); track r.employeeId) {
                    <tr>
                      <td>{{ r.employeeName }}</td><td>{{ r.totalAssigned }}</td><td>{{ r.resolved }}</td><td>{{ r.open }}</td>
                      <td [class.warn-text]="r.overdue > 0">{{ r.overdue }}</td>
                      <td>{{ r.averageResolutionHours != null ? (r.averageResolutionHours | number:'1.1-1') : '—' }}</td>
                      <td>{{ r.onTimeRatePercent != null ? (r.onTimeRatePercent | number:'1.1-1') + '%' : '—' }}</td>
                      <td>{{ r.averageSatisfactionScore != null ? (r.averageSatisfactionScore | number:'1.0-1') : '—' }}</td>
                    </tr>
                  }
                  @empty { <tr><td colspan="8" class="text-muted">No data for these filters.</td></tr> }
                </tbody>
              </table>
            }
            @case ('regional') {
              <table>
                <thead><tr><th>Region</th><th>Zone</th><th>Woreda</th><th>Tickets</th><th>Open</th><th>Resolved</th><th>Avg Resolution (hrs)</th><th>Avg Satisfaction</th></tr></thead>
                <tbody>
                  @for (r of regionalRows(); track r.region + '|' + r.zone + '|' + r.woreda) {
                    <tr>
                      <td>{{ r.region || 'Unspecified' }}</td><td>{{ r.zone || '—' }}</td><td>{{ r.woreda || '—' }}</td>
                      <td>{{ r.ticketCount }}</td><td>{{ r.openCount }}</td><td>{{ r.resolvedCount }}</td>
                      <td>{{ r.averageResolutionHours != null ? (r.averageResolutionHours | number:'1.1-1') : '—' }}</td>
                      <td>{{ r.averageSatisfactionScore != null ? (r.averageSatisfactionScore | number:'1.0-1') : '—' }}</td>
                    </tr>
                  }
                  @empty { <tr><td colspan="8" class="text-muted">No data for these filters.</td></tr> }
                </tbody>
              </table>
            }
            @case ('failure-type') {
              <table>
                <thead><tr><th>Failure Type</th><th>Tickets</th><th>On-Time</th><th>Late</th><th>On-Time %</th><th>Avg Resolution (hrs)</th></tr></thead>
                <tbody>
                  @for (r of failureTypeRows(); track (r.failureTypeId || 'none')) {
                    <tr>
                      <td>{{ r.failureTypeName }}</td><td>{{ r.ticketCount }}</td><td>{{ r.onTimeCount }}</td><td>{{ r.lateCount }}</td>
                      <td>{{ r.onTimeRatePercent != null ? (r.onTimeRatePercent | number:'1.1-1') + '%' : '—' }}</td>
                      <td>{{ r.averageResolutionHours != null ? (r.averageResolutionHours | number:'1.1-1') : '—' }}</td>
                    </tr>
                  }
                  @empty { <tr><td colspan="6" class="text-muted">No data for these filters.</td></tr> }
                </tbody>
              </table>
            }
            @case ('resolution-time') {
              <table>
                <thead><tr><th>Client</th><th>Failure Type</th><th>Assigned To</th><th>Assigned At</th><th>Resolved At</th><th>Resolution (hrs)</th><th>Expected (hrs)</th><th>On Time</th></tr></thead>
                <tbody>
                  @for (r of resolutionTimeRows(); track r.ticketId) {
                    <tr>
                      <td>{{ r.clientName }}</td><td>{{ r.failureTypeName || '—' }}</td><td>{{ r.assignedEmployeeName || '—' }}</td>
                      <td>{{ r.assignedAt ? (r.assignedAt | slice:0:16) : '—' }}</td><td>{{ r.resolvedAt ? (r.resolvedAt | slice:0:16) : '—' }}</td>
                      <td>{{ r.resolutionHours != null ? (r.resolutionHours | number:'1.1-1') : '—' }}</td>
                      <td>{{ r.expectedResolutionHours != null ? (r.expectedResolutionHours | number:'1.1-1') : '—' }}</td>
                      <td>
                        @if (r.wasOnTime === true) { <app-badge status="Approved"></app-badge> }
                        @else if (r.wasOnTime === false) { <app-badge status="Rejected"></app-badge> }
                        @else { <span class="text-muted">—</span> }
                      </td>
                    </tr>
                  }
                  @empty { <tr><td colspan="8" class="text-muted">No resolved tickets match these filters.</td></tr> }
                </tbody>
              </table>
            }
            @case ('customer-rating') {
              <table>
                <thead><tr><th>Client</th><th>Assigned To</th><th>Resolved At</th><th>Stars</th><th>Score</th><th>Closure Reason</th></tr></thead>
                <tbody>
                  @for (r of customerRatingRows(); track r.ticketId) {
                    <tr>
                      <td>{{ r.clientName }}</td><td>{{ r.assignedEmployeeName || '—' }}</td>
                      <td>{{ r.resolvedAt ? (r.resolvedAt | slice:0:10) : '—' }}</td>
                      <td>{{ r.satisfactionStars }} / 5</td><td>{{ r.satisfactionScore }}</td>
                      <td>{{ r.closureReason || '—' }}</td>
                    </tr>
                  }
                  @empty { <tr><td colspan="6" class="text-muted">No rated tickets match these filters.</td></tr> }
                </tbody>
              </table>
            }
          }
        </div>

        <app-pagination [page]="page()" [totalPages]="totalPages()" [totalCount]="totalCount()" [pageSize]="pageSize()" (pageChange)="goToPage($event)"></app-pagination>
      }
    </div>
  `,
  styles: [`
    .tabs { display: flex; gap: 0.4rem; flex-wrap: wrap; margin: 1.1rem 0; }
    .tab { padding: 0.45rem 0.9rem; border-radius: 999px; border: 1px solid var(--slate-200); background: white; font-size: 0.82rem; cursor: pointer; color: var(--slate-500); }
    .tab.active { background: var(--brand-700, #1d4ed8); color: white; border-color: transparent; }
    .filter-bar { margin-bottom: 1.1rem; }
    .filter-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 0.75rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.74rem; font-weight: 600; color: var(--slate-500); }
    .filter-actions { display: flex; gap: 0.5rem; margin-top: 1rem; align-items: center; }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .warn-text { color: var(--red); font-weight: 600; }
    @media print {
      .tabs, .filter-bar, .pagination, nav, .app-sidebar { display: none !important; }
    }
  `],
})
export class ReportsComponent {
  reportTypes = REPORT_TYPES;
  statuses = STATUSES;
  phases = PHASES;
  months = MONTHS;

  activeType = signal<ReportType>('customer-support');
  filter: TicketReportFilter = {};

  loading = signal(false);
  loadError = signal<string | null>(null);
  exporting = signal(false);

  page = signal(1);
  pageSize = signal(20);
  totalCount = signal(0);
  totalPages = signal(0);

  customerSupportRows = signal<CustomerSupportReportRow[]>([]);
  employeePerformanceRows = signal<EmployeePerformanceReportRow[]>([]);
  regionalRows = signal<RegionalReportRow[]>([]);
  failureTypeRows = signal<FailureTypeReportRow[]>([]);
  resolutionTimeRows = signal<ResolutionTimeReportRow[]>([]);
  customerRatingRows = signal<CustomerRatingReportRow[]>([]);

  constructor(
    private reports: TicketReportService,
    public employeesSvc: EmployeeService,
    public failureTypesSvc: FailureTypeService,
    public locations: LocationService,
  ) {
    void this.load();
  }

  labelFor(t: ReportType): string {
    return REPORT_TYPE_LABELS[t];
  }

  selectType(t: ReportType) {
    this.activeType.set(t);
    this.page.set(1);
    void this.load();
  }

  setFilter<K extends keyof TicketReportFilter>(key: K, value: TicketReportFilter[K]) {
    this.filter = { ...this.filter, [key]: value === '' ? undefined : value };
    this.page.set(1);
    void this.load();
  }

  clearFilters() {
    this.filter = {};
    this.page.set(1);
    void this.load();
  }

  goToPage(p: number) {
    this.page.set(p);
    void this.load();
  }

  private applyResult<T>(result: { rows: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }, target: (rows: T[]) => void) {
    target(result.rows);
    this.page.set(result.page);
    this.pageSize.set(result.pageSize);
    this.totalCount.set(result.totalCount);
    this.totalPages.set(result.totalPages);
  }

  async load() {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      switch (this.activeType()) {
        case 'customer-support':
          this.applyResult(await this.reports.getCustomerSupport(this.filter, this.page(), this.pageSize()), r => this.customerSupportRows.set(r));
          break;
        case 'employee-performance':
          this.applyResult(await this.reports.getEmployeePerformance(this.filter, this.page(), this.pageSize()), r => this.employeePerformanceRows.set(r));
          break;
        case 'regional':
          this.applyResult(await this.reports.getRegional(this.filter, this.page(), this.pageSize()), r => this.regionalRows.set(r));
          break;
        case 'failure-type':
          this.applyResult(await this.reports.getFailureType(this.filter, this.page(), this.pageSize()), r => this.failureTypeRows.set(r));
          break;
        case 'resolution-time':
          this.applyResult(await this.reports.getResolutionTime(this.filter, this.page(), this.pageSize()), r => this.resolutionTimeRows.set(r));
          break;
        case 'customer-rating':
          this.applyResult(await this.reports.getCustomerRating(this.filter, this.page(), this.pageSize()), r => this.customerRatingRows.set(r));
          break;
      }
    } catch (err) {
      this.loadError.set('Could not load this report — please try again.');
      console.error(err);
    } finally {
      this.loading.set(false);
    }
  }

  print() {
    window.print();
  }

  async export(format: 'pdf' | 'csv') {
    this.exporting.set(true);
    try {
      if (format === 'pdf') {
        await this.reports.exportPdf(this.activeType(), this.filter);
      } else {
        await this.reports.exportCsv(this.activeType(), this.filter);
      }
    } catch (err) {
      console.error('Export failed', err);
    } finally {
      this.exporting.set(false);
    }
  }
}
