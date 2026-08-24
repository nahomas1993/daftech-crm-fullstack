import { Component, computed, effect, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';
import { TicketService } from '../../core/services/ticket.service';
import { AgreementService } from '../../core/services/agreement.service';
import { EmployeeService } from '../../core/services/employee.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { LocationService } from '../../core/services/location.service';
import { NotificationRecipientType, DashboardData, DashboardFilter } from '../../core/models';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';
import { CountBarChartComponent, CountBarDatum } from '../../shared/count-bar-chart.component';
import { DonutChartComponent, DonutSlice } from '../../shared/donut-chart.component';
import { LineChartComponent, LineSeries } from '../../shared/line-chart.component';

const STATUS_COLORS: Record<string, string> = {
  Submitted: '#94a3b8', Forwarded: '#60a5fa', Assigned: '#38bdf8', InProgress: '#fbbf24',
  Resolved: '#34d399', AwaitingClientConfirmation: '#a78bfa', Escalated: '#f87171', Closed: '#64748b',
};
const RATING_COLORS = ['#f87171', '#fb923c', '#facc15', '#a3e635', '#34d399'];

/** Turns whatever the dashboard request threw into something a support manager can act on. */
function describeDashboardError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return 'Could not reach the server (network or CORS error). Check your connection and that this site’s address is allowed by the API, then retry.';
    }
    if (err.status === 401 || err.status === 403) {
      return 'Your session is not allowed to view dashboard analytics. Sign out and sign back in, then retry.';
    }
    return `The server returned an error (${err.status}) while building the dashboard. Please retry in a moment.`;
  }
  if (err instanceof TimeoutError) {
    return 'The dashboard took too long to respond (the API may be waking up). Please retry.';
  }
  return 'Could not load dashboard data — please try again.';
}

/**
 * Charts + KPIs only — see the Reports page for filterable/exportable
 * tables (the product's Reports-vs-Dashboard split: this page never shows
 * a data table, Reports never shows a chart). Admin sees the full
 * chart/KPI suite, scoped by the filter bar; a non-admin employee sees
 * their personal KPI cards only, same as before this page had charts.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe, CountBarChartComponent, DonutChartComponent, LineChartComponent],
  template: `
    <h1>Dashboard</h1>
    <p class="text-muted" style="margin-top:0.3rem;">{{ subtitle() }}</p>

    @if (isAdmin()) {
      <div class="panel panel-pad filter-bar">
        <div class="filter-grid">
          <div class="field">
            <label>From</label>
            <input type="date" [ngModel]="filter().fromDate" (ngModelChange)="setFilter('fromDate', $event)" />
          </div>
          <div class="field">
            <label>To</label>
            <input type="date" [ngModel]="filter().toDate" (ngModelChange)="setFilter('toDate', $event)" />
          </div>
          <div class="field">
            <label>Region</label>
            <select [ngModel]="filter().region" (ngModelChange)="setFilter('region', $event)">
              <option [ngValue]="undefined">All regions</option>
              @for (r of locations.options().regions; track r.id) { <option [value]="r.name">{{ r.name }}</option> }
            </select>
          </div>
          <div class="field">
            <button class="btn btn-outline btn-sm" style="margin-top:1.35rem;" (click)="clearFilters()">Clear</button>
          </div>
        </div>
      </div>

      <div class="cards" style="margin-top:1.25rem;">
        <a routerLink="/admin/clients" class="panel panel-pad card">
          <div class="card-label">Active Clients</div>
          <div class="card-value">{{ activeClients() }}</div>
        </a>
        <a routerLink="/admin/signup-requests" class="panel panel-pad card">
          <div class="card-label">Pending Signup Requests</div>
          <div class="card-value" [class.warn]="pendingSignups() > 0">{{ pendingSignups() }}</div>
        </a>
        <a routerLink="/admin/agreements" class="panel panel-pad card">
          <div class="card-label">Agreements Near/Over Expiry</div>
          <div class="card-value" [class.warn]="expiringAgreements() > 0">{{ expiringAgreements() }}</div>
        </a>
        <a routerLink="/admin/notifications" class="panel panel-pad card">
          <div class="card-label">Unread Notifications</div>
          <div class="card-value" [class.warn]="unreadNotifications() > 0">{{ unreadNotifications() }}</div>
        </a>
      </div>

      @if (dashboardData(); as d) {
        @if (sectionFailed(d, 'kpis')) {
          <div class="panel panel-pad" style="margin-top:1.25rem;">
            <p class="upload-error" style="margin-top:0;">Ticket KPIs could not be calculated on this load — the figures below may be incomplete.</p>
            <button class="btn btn-outline btn-sm" style="margin-top:0.75rem;" (click)="reloadDashboard()">Retry</button>
          </div>
        }
        <div class="cards">
          <div class="panel panel-pad card">
            <div class="card-label">Total Tickets</div>
            <div class="card-value">{{ d.kpis.totalTickets }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Open</div>
            <div class="card-value">{{ d.kpis.openTickets }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Resolved</div>
            <div class="card-value">{{ d.kpis.resolvedTickets }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Overdue</div>
            <div class="card-value" [class.warn]="d.kpis.overdueTickets > 0">{{ d.kpis.overdueTickets }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Resolution Rate (On-Time)</div>
            <div class="card-value">{{ d.kpis.resolutionRatePercent }}%</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Customer Satisfaction</div>
            <div class="card-value">{{ d.kpis.averageSatisfactionScore != null ? (d.kpis.averageSatisfactionScore | number:'1.0-0') + '/100' : '—' }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Approaching Expiration</div>
            <div class="card-value" [class.warn]="d.supportOverview.approachingExpirationCount > 0">{{ d.supportOverview.approachingExpirationCount }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Free Support Clients</div>
            <div class="card-value">{{ d.supportOverview.freeSupportClientCount }}</div>
          </div>
          <div class="panel panel-pad card">
            <div class="card-label">Chargeable Support Clients</div>
            <div class="card-value">{{ d.supportOverview.chargeableSupportClientCount }}</div>
          </div>
        </div>

        <div class="chart-grid">
          <div class="panel panel-pad" style="grid-column: 1 / -1;">
            <h3 style="margin-bottom:0.9rem;">Support &amp; Expiration Overview</h3>
            @if (sectionFailed(d, 'supportOverview')) {
              <p class="upload-error" style="margin-top:0;">This chart could not be loaded. The rest of the dashboard is up to date.</p>
              <button class="btn btn-outline btn-sm" (click)="reloadDashboard()">Retry</button>
            } @else {
            <app-count-bar-chart [chartData]="supportOverviewBars(d)"></app-count-bar-chart>
            <p class="text-muted" style="font-size:0.75rem; margin-top:0.7rem;">This graph is derived directly from the Support &amp; Expiration metrics on the Reports page.</p>
            }
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Tickets by Region</h3>
            @if (sectionFailed(d, 'ticketsByRegion')) {
              <p class="upload-error" style="margin-top:0;">This chart could not be loaded. The rest of the dashboard is up to date.</p>
              <button class="btn btn-outline btn-sm" (click)="reloadDashboard()">Retry</button>
            } @else {
            <app-count-bar-chart [chartData]="regionBars(d)"></app-count-bar-chart>
            }
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Tickets by Failure Type</h3>
            @if (sectionFailed(d, 'ticketsByFailureType')) {
              <p class="upload-error" style="margin-top:0;">This chart could not be loaded. The rest of the dashboard is up to date.</p>
              <button class="btn btn-outline btn-sm" (click)="reloadDashboard()">Retry</button>
            } @else {
            <app-count-bar-chart [chartData]="failureTypeBars(d)"></app-count-bar-chart>
            }
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Employee Performance (Resolved Tickets)</h3>
            @if (sectionFailed(d, 'ticketsByEmployee')) {
              <p class="upload-error" style="margin-top:0;">This chart could not be loaded. The rest of the dashboard is up to date.</p>
              <button class="btn btn-outline btn-sm" (click)="reloadDashboard()">Retry</button>
            } @else {
            <app-count-bar-chart [chartData]="employeeBars(d)"></app-count-bar-chart>
            }
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Ticket Status</h3>
            <app-donut-chart [data]="statusSlices(d)" centerLabel="Tickets" centerSuffix="" [centerOverride]="d.kpis.totalTickets"></app-donut-chart>
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Customer Rating Distribution</h3>
            <app-donut-chart [data]="ratingSlices(d)" centerLabel="Ratings" centerSuffix=""></app-donut-chart>
          </div>
          <div class="panel panel-pad" style="grid-column: 1 / -1;">
            <h3 style="margin-bottom:0.9rem;">Monthly Trend</h3>
            @if (sectionFailed(d, 'monthlyTrend')) {
              <p class="upload-error" style="margin-top:0;">This chart could not be loaded. The rest of the dashboard is up to date.</p>
              <button class="btn btn-outline btn-sm" (click)="reloadDashboard()">Retry</button>
            } @else {
            <app-line-chart [data]="trendSeries(d)" [xLabels]="trendLabels(d)"></app-line-chart>
            }
          </div>
        </div>
      } @else if (dashboardError()) {
        <div class="panel panel-pad" style="margin-top:1.25rem;">
          <p class="upload-error" style="margin-top:0;">{{ dashboardError() }}</p>
          <button class="btn btn-outline btn-sm" style="margin-top:0.75rem;" (click)="reloadDashboard()">Retry</button>
        </div>
      } @else {
        <p class="text-muted" style="margin-top:1.5rem;">Loading dashboard…</p>
      }

      <div class="panel panel-pad" style="margin-top: 1.5rem;">
        <h3 style="margin-bottom: 0.9rem;">Employee Workload</h3>
        <div class="table-scroll"><table>
          <thead><tr><th>Employee</th><th>Role(s)</th><th>Open Tickets</th><th>Avg. Satisfaction</th><th>Account Status</th></tr></thead>
          <tbody>
            @for (e of employees.employees(); track e.id) {
              <tr>
                <td>{{ e.fullName }}</td>
                <td class="text-muted">{{ e.roles.join(', ') }}</td>
                <td>{{ e.openTicketCount }}</td>
                <td class="text-muted">{{ e.averageSatisfactionScore != null ? e.averageSatisfactionScore.toFixed(0) + '/100' : '—' }}</td>
                <td>
                  <span class="badge" [class]="e.accountStatus === 'Active' ? 'badge-green' : 'badge-red'">{{ e.accountStatus }}</span>
                </td>
              </tr>
            }
          </tbody>
        </table></div>
      </div>
    } @else {
      <div class="cards">
        <a routerLink="/admin/tickets" class="panel panel-pad card">
          <div class="card-label">My Open Tickets</div>
          <div class="card-value">{{ myOpenTickets() }}</div>
        </a>
        <a routerLink="/admin/notifications" class="panel panel-pad card">
          <div class="card-label">Unread Notifications</div>
          <div class="card-value" [class.warn]="unreadNotifications() > 0">{{ unreadNotifications() }}</div>
        </a>
        <a routerLink="/admin/tickets" class="panel panel-pad card">
          <div class="card-label">Escalated Tickets</div>
          <div class="card-value" [class.warn]="myEscalatedTickets() > 0">{{ myEscalatedTickets() }}</div>
        </a>
      </div>
    }
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 1rem; margin-top: 1.25rem; }
    .card { display: block; }
    .card-label { font-size: 0.78rem; color: var(--slate-500); font-weight: 600; margin-bottom: 0.4rem; }
    .card-value { font-size: 1.9rem; font-weight: 700; color: var(--navy-900); }
    .card-value.warn { color: var(--amber); }
    .filter-bar { margin-top: 1.25rem; }
    .filter-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 0.75rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.74rem; font-weight: 600; color: var(--slate-500); }
    .chart-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1.1rem; margin-top: 1.25rem; }
    .upload-error { color: var(--red); font-size: 0.85rem; margin-top: 1rem; }
  `],
})
export class DashboardComponent {
  filter = signal<DashboardFilter>({});
  dashboardData = signal<DashboardData | null>(null);
  dashboardError = signal<string | null>(null);

  constructor(
    private clientsSvc: ClientService,
    private ticketsSvc: TicketService,
    private agreementsSvc: AgreementService,
    public employees: EmployeeService,
    private notificationsSvc: NotificationService,
    private auth: AuthService,
    private dashboardSvc: DashboardService,
    public locations: LocationService,
  ) {
    // allowSignalWrites: both effects kick off async loads that write
    // signals (loadDashboard clears dashboardError synchronously, the
    // services set their own state). Without this flag Angular throws
    // NG0600 the moment the effect runs, the fetch never happens, and the
    // page sits on "Loading dashboard..." forever with no charts and no error.
    effect(() => {
      const key = this.recipientKey();
      if (key) void this.notificationsSvc.loadFor(key.type, key.id);
    }, { allowSignalWrites: true });

    effect(() => {
      if (!this.isAdmin()) return;
      const f = this.filter(); // re-run whenever the filter changes
      void this.loadDashboard(f);
    }, { allowSignalWrites: true });
  }

  /** Guards against an out-of-order response from a superseded filter overwriting a newer one. */
  private requestSeq = 0;

  reloadDashboard() {
    void this.loadDashboard(this.filter());
  }

  private async loadDashboard(filter: DashboardFilter) {
    const seq = ++this.requestSeq;
    this.dashboardError.set(null);
    try {
      const data = await this.dashboardSvc.getDashboardData(filter);
      if (seq !== this.requestSeq) return; // a newer request already won
      this.dashboardData.set(data);
    } catch (err) {
      if (seq !== this.requestSeq) return;
      // Surface WHY it failed — a bare "please try again" made a timed-out or
      // CORS-blocked request look identical to a server error, and before the
      // timeout in DashboardService existed the page could sit on
      // "Loading dashboard…" forever with nothing rendered at all.
      this.dashboardError.set(describeDashboardError(err));
      console.error('Dashboard data request failed', err);
    }
  }

  setFilter<K extends keyof DashboardFilter>(key: K, value: DashboardFilter[K]) {
    this.filter.set({ ...this.filter(), [key]: value === '' ? undefined : value });
  }

  clearFilters() {
    this.filter.set({});
  }

  isAdmin = computed(() => this.auth.currentEmployee()?.roles.includes('Admin') ?? false);

  subtitle = computed(() => {
    if (this.isAdmin()) return 'Live charts and KPIs across clients, tickets, and staff workload.';
    return 'Your assigned tickets and attendance.';
  });

  activeClients = computed(() => this.clientsSvc.approvedClients().length);
  pendingSignups = computed(() => this.clientsSvc.pendingRequests().length);
  expiringAgreements = computed(() => this.agreementsSvc.expiringSoon().length);

  private static readonly OPEN_STATUSES = ['Assigned', 'InProgress'];
  myOpenTickets = computed(() => {
    const emp = this.auth.currentEmployee();
    if (!emp) return 0;
    return this.ticketsSvc.forEmployee(emp.id).filter(t => DashboardComponent.OPEN_STATUSES.includes(t.status)).length;
  });

  myEscalatedTickets = computed(() => {
    const emp = this.auth.currentEmployee();
    if (!emp) return 0;
    return this.ticketsSvc.forEmployee(emp.id).filter(t => t.status === 'Escalated').length;
  });

  private recipientKey = computed((): { type: NotificationRecipientType; id: string } | null => {
    const emp = this.auth.currentEmployee();
    if (!emp) return null;
    if (emp.roles.includes('Admin')) return { type: 'Admin', id: 'ALL_ADMIN' };
    return { type: 'Employee', id: emp.id };
  });

  unreadNotifications = computed(() => {
    const key = this.recipientKey();
    return key ? this.notificationsSvc.unreadCountFor(key.type, key.id) : 0;
  });

  /** True when the API reported it couldn't build this dashboard section on the last load. */
  sectionFailed(d: DashboardData, section: string): boolean {
    return (d.failedSections ?? []).includes(section);
  }

  // --- Chart data shaping ---

  regionBars(d: DashboardData): CountBarDatum[] {
    return d.ticketsByRegion.map(r => ({ label: r.region, value: r.ticketCount }));
  }

  failureTypeBars(d: DashboardData): CountBarDatum[] {
    return d.ticketsByFailureType.map(f => ({ label: f.failureTypeName, value: f.ticketCount }));
  }

  employeeBars(d: DashboardData): CountBarDatum[] {
    return d.ticketsByEmployee.map(e => ({ label: e.employeeName, value: e.resolvedCount }));
  }

  supportOverviewBars(d: DashboardData): CountBarDatum[] {
    return [
      { label: 'Approaching Expiration', value: d.supportOverview.approachingExpirationCount },
      { label: 'Free Support', value: d.supportOverview.freeSupportClientCount },
      { label: 'Chargeable Support', value: d.supportOverview.chargeableSupportClientCount },
    ];
  }

  statusSlices(d: DashboardData): DonutSlice[] {
    return d.ticketsByStatus
      .filter(s => s.count > 0)
      .map(s => ({ label: s.status, value: s.count, color: STATUS_COLORS[s.status] ?? '#94a3b8' }));
  }

  ratingSlices(d: DashboardData): DonutSlice[] {
    return d.ratingDistribution
      .filter(r => r.count > 0)
      .map(r => ({
        label: `${r.stars} star${r.stars === 1 ? '' : 's'}`,
        value: r.count,
        // Half-star values (e.g. 4.5) share their whole-star-below's color band — Math.floor(4.5) - 1 = 3, same bucket as a plain 4.
        color: RATING_COLORS[Math.floor(r.stars) - 1] ?? '#94a3b8',
      }));
  }

  trendLabels(d: DashboardData): string[] {
    return d.monthlyTrend.map(p => p.month);
  }

  trendSeries(d: DashboardData): LineSeries[] {
    return [
      { label: 'Tickets', color: '#60a5fa', values: d.monthlyTrend.map(p => p.ticketCount) },
      { label: 'Resolved', color: '#34d399', values: d.monthlyTrend.map(p => p.resolvedCount) },
      { label: 'On-Time %', color: '#fbbf24', values: d.monthlyTrend.map(p => p.onTimeRatePercent ?? null) },
    ];
  }
}
