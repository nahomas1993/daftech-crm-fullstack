import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe, DecimalPipe } from '@angular/common';
import { TicketReportService } from '../../core/services/ticket-report.service';
import { ReportService } from '../../core/services/report.service';
import { EmployeeService } from '../../core/services/employee.service';
import { FailureTypeService } from '../../core/services/failure-type.service';
import { LocationService } from '../../core/services/location.service';
import { ClientService } from '../../core/services/client.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { BrandLogoComponent } from '../../shared/brand-logo.component';
import {
  ReportType, REPORT_TYPE_LABELS, TicketReportFilter, TableReportResult,
  CustomerSupportReportRow, EmployeePerformanceReportRow, RegionalReportRow,
  FailureTypeReportRow, ResolutionTimeReportRow, CustomerRatingReportRow,
  TicketStatus, SupportPhase, SupportOverview, OverallClientReport,
} from '../../core/models';

const REPORT_TYPES: ReportType[] = [
  'customer-support', 'employee-performance', 'regional', 'failure-type', 'resolution-time', 'customer-rating',
  'support-expiration', 'client-report',
];
const TABLE_REPORT_TYPES = new Set<ReportType>([
  'customer-support', 'employee-performance', 'regional', 'failure-type', 'resolution-time', 'customer-rating',
]);
const STATUSES: TicketStatus[] = ['Submitted', 'Forwarded', 'Assigned', 'InProgress', 'Resolved', 'AwaitingClientConfirmation', 'Escalated', 'Closed'];
const PHASES: SupportPhase[] = ['Intake', 'Diagnosis', 'Repair', 'Verification', 'Closed'];
const MONTHS = [
  { value: 1, label: 'January' }, { value: 2, label: 'February' }, { value: 3, label: 'March' },
  { value: 4, label: 'April' }, { value: 5, label: 'May' }, { value: 6, label: 'June' },
  { value: 7, label: 'July' }, { value: 8, label: 'August' }, { value: 9, label: 'September' },
  { value: 10, label: 'October' }, { value: 11, label: 'November' }, { value: 12, label: 'December' },
];

/**
 * The Reports module — the original six table-only reports
 * (Customer/Support, Employee Performance, Regional, Failure-Type,
 * Resolution-Time, Customer-Rating), each filterable/searchable/paginated
 * /printable/exportable via one shared filter bar (switching report type
 * re-fetches with the same filter state, since a support manager
 * typically wants to compare the same slice of tickets across report
 * types) — plus two further tabs that don't fit that shared
 * filter+table shape and are rendered separately:
 *
 *   - Support & Expiration: the same SupportOverviewDto the Dashboard's
 *     "Support & Expiration Overview" graph is built from, shown here as
 *     its underlying breakdown (approaching-expiration agreements,
 *     free/chargeable support clients) — this is the source of truth
 *     that graph's caption refers to.
 *   - Overall Client Report: a client picker plus that one client's full
 *     history in one place (profile, systems/products with agreements
 *     and training, every ticket, every satisfaction survey, and a
 *     summary) — see ReportService.getOverallClientReport.
 */
@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, SlicePipe, DecimalPipe, BadgeComponent, PaginationComponent, BrandLogoComponent],
  template: `
    <!-- Print-only letterhead: the app's own inline-SVG brand mark, so a
         printed report carries the logo without any pasted-in image. -->
    <div class="print-letterhead">
      <app-brand-logo [size]="42" variant="full"></app-brand-logo>
      <div class="print-meta">
        <div class="print-title">{{ printTitle() }}</div>
        <div class="print-sub">Generated {{ today }}</div>
      </div>
    </div>

    <h1>Reports</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Filterable, exportable tables, plus support/expiration and per-client reports — for charts and live KPIs, see the Dashboard.</p>

    <div class="tabs">
      @for (t of reportTypes; track t) {
        <button type="button" class="tab" [class.active]="activeType() === t" (click)="selectType(t)">
          {{ labelFor(t) }}
        </button>
      }
    </div>

    @if (isTableReport(activeType())) {
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
    }

    @if (activeType() === 'support-expiration') {
      <div class="panel panel-pad filter-bar">
        <div class="filter-actions">
          <div style="flex:1;"></div>
          <button class="btn btn-outline btn-sm" (click)="print()">Print</button>
        </div>
      </div>

      <div class="panel panel-pad" id="report-table-panel">
        @if (supportOverviewLoading()) {
          <p class="text-muted">Loading support &amp; expiration data…</p>
        } @else if (supportOverviewError()) {
          <p class="upload-error">{{ supportOverviewError() }}</p>
          <button class="btn btn-outline btn-sm" style="margin-top:0.75rem;" (click)="loadSupportOverview()">Retry</button>
        } @else if (supportOverview(); as s) {
          <p class="text-muted" style="margin-top:0;">
            These are the same figures behind the Dashboard's "Support &amp; Expiration Overview" graph — shown here as
            the underlying breakdown, since that graph is derived directly from this data.
          </p>

          <div class="cards" style="margin-top:1rem;">
            <div class="panel panel-pad card">
              <div class="card-label">Approaching Expiration</div>
              <div class="card-value" [class.warn]="s.approachingExpirationCount > 0">{{ s.approachingExpirationCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Free Support Clients</div>
              <div class="card-value">{{ s.freeSupportClientCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Chargeable Support Clients</div>
              <div class="card-value">{{ s.chargeableSupportClientCount }}</div>
            </div>
          </div>

          <h3 style="margin:1.5rem 0 0.75rem;">Agreements Approaching Expiration (next 30 days)</h3>
          <div class="table-scroll">
            <table>
              <thead><tr><th>Client</th><th>System/Product</th><th>Expires</th><th>Days Left</th></tr></thead>
              <tbody>
                @for (e of s.approachingExpiration; track e.agreementId) {
                  <tr>
                    <td>{{ e.clientName }}</td><td>{{ e.systemProductName }}</td>
                    <td>{{ e.expiryDate | slice:0:10 }}</td>
                    <td [class.warn-text]="e.daysUntilExpiry <= 7">{{ e.daysUntilExpiry }}</td>
                  </tr>
                }
                @empty { <tr><td colspan="4" class="text-muted">No agreements expiring in the next 30 days.</td></tr> }
              </tbody>
            </table>
          </div>

          <h3 style="margin:1.5rem 0 0.75rem;">Free Support Clients</h3>
          <div class="table-scroll">
            <table>
              <thead><tr><th>Client</th><th>Ticket Count</th></tr></thead>
              <tbody>
                @for (c of s.freeSupportClients; track c.clientId) {
                  <tr><td>{{ c.clientName }}</td><td>{{ c.ticketCount }}</td></tr>
                }
                @empty { <tr><td colspan="2" class="text-muted">No free-support tickets on record.</td></tr> }
              </tbody>
            </table>
          </div>

          <h3 style="margin:1.5rem 0 0.75rem;">Chargeable Support Clients</h3>
          <div class="table-scroll">
            <table>
              <thead><tr><th>Client</th><th>Ticket Count</th></tr></thead>
              <tbody>
                @for (c of s.chargeableSupportClients; track c.clientId) {
                  <tr><td>{{ c.clientName }}</td><td>{{ c.ticketCount }}</td></tr>
                }
                @empty { <tr><td colspan="2" class="text-muted">No chargeable tickets on record.</td></tr> }
              </tbody>
            </table>
          </div>
        }
      </div>
    }

    @if (activeType() === 'client-report') {
      <div class="panel panel-pad filter-bar">
        <div class="filter-grid">
          <div class="field" style="grid-column: span 2;">
            <label>Client</label>
            <select [ngModel]="selectedClientId()" (ngModelChange)="selectClient($event)">
              <option [ngValue]="undefined">Select a client…</option>
              @for (c of clientsSvc.clients(); track c.id) { <option [value]="c.id">{{ c.name }} ({{ c.accountRefId }})</option> }
            </select>
          </div>
        </div>
        <div class="filter-actions">
          <div style="flex:1;"></div>
          @if (clientReport()) {
            <button class="btn btn-outline btn-sm" (click)="print()">Print</button>
            <button class="btn btn-primary btn-sm" (click)="print()">Export PDF</button>
          }
        </div>
      </div>

      <div class="panel panel-pad" id="report-table-panel">
        @if (!selectedClientId()) {
          <p class="text-muted">Choose a client above to view their full report.</p>
        } @else if (clientReportLoading()) {
          <p class="text-muted">Loading client report…</p>
        } @else if (clientReportError()) {
          <p class="upload-error">{{ clientReportError() }}</p>
          <button class="btn btn-outline btn-sm" style="margin-top:0.75rem;" (click)="loadClientReport()">Retry</button>
        } @else if (clientReport()) {
          @if (clientReport(); as r) {
          <div class="client-report-header">
            <div>
              <h2 style="margin:0;">{{ r.clientName }}</h2>
              <p class="text-muted" style="margin:0.2rem 0 0;">{{ r.accountRefId }} · {{ r.email }} · {{ r.phoneNumber }}</p>
              <p class="text-muted" style="margin:0.2rem 0 0;">{{ r.office }}, {{ r.location }}{{ r.region ? ' · ' + r.region : '' }}{{ r.zone ? ' / ' + r.zone : '' }}{{ r.woreda ? ' / ' + r.woreda : '' }}</p>
            </div>
            <app-badge [status]="r.accountStatus"></app-badge>
          </div>

          <div class="cards" style="margin-top:1rem;">
            <div class="panel panel-pad card">
              <div class="card-label">Systems/Products</div>
              <div class="card-value">{{ r.summary.systemProductCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Active Agreements</div>
              <div class="card-value">{{ r.summary.activeAgreementCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Total Tickets</div>
              <div class="card-value">{{ r.summary.totalTicketCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Open Tickets</div>
              <div class="card-value" [class.warn]="r.summary.openTicketCount > 0">{{ r.summary.openTicketCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Resolved Tickets</div>
              <div class="card-value">{{ r.summary.resolvedTicketCount }}</div>
            </div>
            <div class="panel panel-pad card">
              <div class="card-label">Avg Satisfaction</div>
              <div class="card-value">{{ r.summary.averageSatisfactionScore != null ? (r.summary.averageSatisfactionScore | number:'1.0-1') : '—' }}</div>
            </div>
          </div>

          <h3 style="margin:1.5rem 0 0.75rem;">Systems/Products, Agreements &amp; Training</h3>
          @for (sp of r.systemProducts; track sp.id) {
            <div class="panel panel-pad" style="margin-bottom:0.9rem;">
              <div style="display:flex; justify-content:space-between; align-items:center; gap:1rem;">
                <div>
                  <strong>{{ sp.name }}</strong>
                  <span class="text-muted"> — {{ sp.referenceNumber }}</span>
                  @if (sp.description) { <p class="text-muted" style="margin:0.2rem 0 0;">{{ sp.description }}</p> }
                </div>
                <app-badge [status]="sp.trainingCompletionStatus"></app-badge>
              </div>

              @if (sp.agreements.length > 0) {
                <div class="table-scroll" style="margin-top:0.75rem;">
                  <table>
                    <thead><tr><th>Type</th><th>Document #</th><th>Signed</th><th>Expires</th><th>Status</th><th>Tier</th></tr></thead>
                    <tbody>
                      @for (a of sp.agreements; track a.id) {
                        <tr>
                          <td>{{ a.agreementTypeName }}</td><td>{{ a.documentNumber }}</td>
                          <td>{{ a.signDate | slice:0:10 }}</td><td>{{ a.expiryDate | slice:0:10 }}</td>
                          <td><app-badge [status]="a.status"></app-badge></td><td>{{ a.billingTier }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }

              @if (sp.trainingRecords.length > 0) {
                <div class="table-scroll" style="margin-top:0.75rem;">
                  <table>
                    <thead><tr><th>Trainer</th><th>Date</th><th>Description</th><th>File</th></tr></thead>
                    <tbody>
                      @for (tr of sp.trainingRecords; track tr.id) {
                        <tr>
                          <td>{{ tr.trainerEmployeeName }}</td><td>{{ tr.trainingDate | slice:0:10 }}</td>
                          <td>{{ tr.description }}</td><td>{{ tr.fileName || '—' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          } @empty {
            <p class="text-muted">No systems/products on record for this client.</p>
          }

          <h3 style="margin:1.5rem 0 0.75rem;">Tickets</h3>
          <div class="table-scroll">
            <table>
              <thead><tr><th>Description</th><th>Failure Type</th><th>Submitted</th><th>Assigned To</th><th>Status</th><th>Chargeable</th><th>Satisfaction</th><th>Attachment</th><th>Voice Note</th></tr></thead>
              <tbody>
                @for (t of r.tickets; track t.id) {
                  <tr>
                    <td>{{ t.description }}</td><td>{{ t.failureTypeName || '—' }}</td>
                    <td>{{ t.dateSubmitted | slice:0:10 }}</td><td>{{ t.assignedEmployeeName || 'Unassigned' }}</td>
                    <td><app-badge [status]="t.status"></app-badge></td>
                    <td><app-badge [status]="t.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
                    <td>{{ t.satisfactionScore ?? '—' }}</td>
                    <td>{{ t.attachmentFileName || '—' }}</td>
                    <td>{{ t.voiceNoteFileName || '—' }}</td>
                  </tr>
                }
                @empty { <tr><td colspan="9" class="text-muted">No tickets on record for this client.</td></tr> }
              </tbody>
            </table>
          </div>

          <h3 style="margin:1.5rem 0 0.75rem;">Satisfaction Surveys</h3>
          <div class="survey-list">
            @for (s of r.satisfactionSurveys; track s.id) {
              <div class="panel panel-pad survey-card">
                <div class="text-muted" style="font-size:0.8rem; margin-bottom:0.5rem;">Submitted {{ s.submittedAt | slice:0:10 }}</div>
                <table class="survey-answers-table">
                  <tbody>
                    @for (a of s.answers; track a.questionText) {
                      <tr>
                        <td class="survey-question-cell">{{ a.questionText }}</td>
                        <td class="survey-rating-cell">{{ a.rating }} / 5</td>
                      </tr>
                    }
                  </tbody>
                </table>
                @if (s.satisfactionComment) {
                  <div class="survey-comment">
                    <div class="text-muted" style="font-size:0.78rem; margin-bottom:0.2rem;">In the client's own words:</div>
                    <p>{{ s.satisfactionComment }}</p>
                  </div>
                }
              </div>
            }
            @empty {
              <p class="text-muted">No satisfaction surveys submitted by this client.</p>
            }
          </div>
          }
        }
      </div>
    }
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
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; }
    .card { text-align: left; }
    .card-label { font-size: 0.78rem; color: var(--slate-500); font-weight: 600; margin-bottom: 0.4rem; }
    .card-value { font-size: 1.6rem; font-weight: 700; color: var(--navy-900); }
    .card-value.warn { color: var(--amber); }
    .client-report-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
    .survey-list { display: grid; gap: 0.75rem; }
    .survey-card { padding: 0.9rem 1rem; }
    .survey-answers-table { width: 100%; border-collapse: collapse; }
    .survey-answers-table td { padding: 0.3rem 0.4rem; font-size: 0.85rem; border-bottom: 1px solid var(--slate-100); }
    .survey-question-cell { color: var(--slate-600); }
    .survey-rating-cell { text-align: right; font-weight: 600; white-space: nowrap; width: 70px; }
    .survey-comment { margin-top: 0.7rem; padding-top: 0.6rem; border-top: 1px dashed var(--slate-200); }
    .survey-comment p { margin: 0; font-size: 0.88rem; white-space: pre-wrap; }
    .print-letterhead { display: none; }
    @media print {
      .tabs, .filter-bar, .pagination, nav, .app-sidebar { display: none !important; }
      .print-letterhead {
        display: flex !important;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        padding-bottom: 0.6rem;
        margin-bottom: 0.9rem;
        border-bottom: 2px solid var(--brand-blue, #1d4ed8);
      }
      .print-meta { text-align: right; }
      .print-title { font-weight: 700; font-size: 1rem; }
      .print-sub { font-size: 0.72rem; color: var(--slate-500, #64748b); }
    }
  `],
})
export class ReportsComponent {
  reportTypes = REPORT_TYPES;
  statuses = STATUSES;
  phases = PHASES;
  months = MONTHS;

  readonly today = new Date().toLocaleString();

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

  // --- Support & Expiration tab ---
  supportOverview = signal<SupportOverview | null>(null);
  supportOverviewLoading = signal(false);
  supportOverviewError = signal<string | null>(null);

  // --- Overall Client Report tab ---
  selectedClientId = signal<string | undefined>(undefined);
  clientReport = signal<OverallClientReport | null>(null);
  clientReportLoading = signal(false);
  clientReportError = signal<string | null>(null);

  constructor(
    private reports: TicketReportService,
    private reportSvc: ReportService,
    public employeesSvc: EmployeeService,
    public failureTypesSvc: FailureTypeService,
    public locations: LocationService,
    public clientsSvc: ClientService,
  ) {
    void this.load();
  }

  labelFor(t: ReportType): string {
    return REPORT_TYPE_LABELS[t];
  }

  /** "X Report" for the print letterhead — the two new tabs already have "Report" in their own name, so avoid the doubled-up "Overall Client Report Report". */
  printTitle(): string {
    const label = this.labelFor(this.activeType());
    return label.toLowerCase().includes('report') ? label : `${label} Report`;
  }

  isTableReport(t: ReportType): boolean {
    return TABLE_REPORT_TYPES.has(t);
  }

  selectType(t: ReportType) {
    this.activeType.set(t);
    this.page.set(1);
    if (t === 'support-expiration') {
      void this.loadSupportOverview();
    } else if (t === 'client-report') {
      // Nothing to fetch yet — waiting on a client to be picked; see selectClient().
    } else {
      void this.load();
    }
  }

  async loadSupportOverview() {
    this.supportOverviewLoading.set(true);
    this.supportOverviewError.set(null);
    try {
      this.supportOverview.set(await this.reportSvc.getSupportOverview());
    } catch (err) {
      this.supportOverviewError.set('Could not load support & expiration data — please try again.');
      console.error(err);
    } finally {
      this.supportOverviewLoading.set(false);
    }
  }

  selectClient(clientId: string | undefined) {
    this.selectedClientId.set(clientId);
    this.clientReport.set(null);
    if (clientId) {
      void this.loadClientReport();
    }
  }

  async loadClientReport() {
    const clientId = this.selectedClientId();
    if (!clientId) return;
    this.clientReportLoading.set(true);
    this.clientReportError.set(null);
    try {
      this.clientReport.set(await this.reportSvc.getOverallClientReport(clientId));
    } catch (err) {
      this.clientReportError.set('Could not load this client\'s report — please try again.');
      console.error(err);
    } finally {
      this.clientReportLoading.set(false);
    }
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
