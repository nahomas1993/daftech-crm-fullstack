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
import { CountBarChartComponent, CountBarDatum } from '../../shared/count-bar-chart.component';
import { DonutChartComponent, DonutSlice } from '../../shared/donut-chart.component';
import { LineChartComponent, LineSeries } from '../../shared/line-chart.component';

const STATUS_COLORS: Record<string, string> = {
  Submitted: '#94a3b8', Forwarded: '#60a5fa', Assigned: '#38bdf8', InProgress: '#fbbf24',
  Resolved: '#34d399', AwaitingClientConfirmation: '#a78bfa', Escalated: '#f87171', Closed: '#64748b',
};
const RATING_COLORS = ['#f87171', '#fb923c', '#facc15', '#a3e635', '#34d399'];

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

      @if (dashboardError()) {
        <p class="upload-error">{{ dashboardError() }}</p>
      } @else if (dashboardData(); as d) {
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
        </div>

        <div class="chart-grid">
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Tickets by Region</h3>
            <app-count-bar-chart [chartData]="regionBars(d)"></app-count-bar-chart>
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Tickets by Failure Type</h3>
            <app-count-bar-chart [chartData]="failureTypeBars(d)"></app-count-bar-chart>
          </div>
          <div class="panel panel-pad">
            <h3 style="margin-bottom:0.9rem;">Employee Performance (Resolved Tickets)</h3>
            <app-count-bar-chart [chartData]="employeeBars(d)"></app-count-bar-chart>
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
            <app-line-chart [data]="trendSeries(d)" [xLabels]="trendLabels(d)"></app-line-chart>
          </div>
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
    effect(() => {
      const key = this.recipientKey();
      if (key) void this.notificationsSvc.loadFor(key.type, key.id);
    });

    effect(() => {
      if (!this.isAdmin()) return;
      const f = this.filter(); // re-run whenever the filter changes
      void this.loadDashboard(f);
    });
  }

  private async loadDashboard(filter: DashboardFilter) {
    this.dashboardError.set(null);
    try {
      this.dashboardData.set(await this.dashboardSvc.getDashboardData(filter));
    } catch (err) {
      this.dashboardError.set('Could not load dashboard data — please try again.');
      console.error(err);
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
