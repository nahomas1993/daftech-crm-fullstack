import { Component, OnDestroy, OnInit, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe, DatePipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { TicketService } from '../../core/services/ticket.service';
import { AgreementService } from '../../core/services/agreement.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { BadgeComponent } from '../../shared/badge.component';
import { TICKET_CATEGORY_LABELS } from '../../core/models';
import { DonutChartComponent, DonutSlice } from '../../shared/donut-chart.component';
import { CountBarChartComponent, CountBarDatum } from '../../shared/count-bar-chart.component';
import { LineChartComponent, LineSeries } from '../../shared/line-chart.component';

/** Status palette shared with the staff dashboard so a status reads the same color everywhere. */
const STATUS_COLORS: Record<string, string> = {
  Submitted: '#94a3b8', Forwarded: '#60a5fa', Assigned: '#38bdf8', InProgress: '#fbbf24',
  Resolved: '#34d399', AwaitingClientConfirmation: '#a78bfa', Escalated: '#f87171', Closed: '#64748b',
};

/** Poll interval so a technician's status change (e.g. Resolved) shows
 * up on the dashboard without the client refreshing the page. */
const REFRESH_INTERVAL_MS = 20_000;

@Component({
  selector: 'app-portal-dashboard',
  standalone: true,
  imports: [RouterLink, SlicePipe, DatePipe, BadgeComponent, DonutChartComponent, CountBarChartComponent, LineChartComponent],
  template: `
    <h1>Dashboard</h1>
    <p class="text-muted" style="margin-top:0.3rem;">{{ client()?.name }} — a quick look at your support activity.</p>

    <div class="cards">
      <a routerLink="/portal/maintenance-history" class="panel panel-pad card">
        <div class="card-label">Total Support Requests</div>
        <div class="card-value">{{ totalSupport() }}</div>
      </a>
      <a routerLink="/portal/maintenance-history" [queryParams]="{ filter: 'pending' }" class="panel panel-pad card">
        <div class="card-label">Pending</div>
        <div class="card-value" [class.warn]="pending() > 0">{{ pending() }}</div>
      </a>
      <a routerLink="/portal/maintenance-history" [queryParams]="{ filter: 'accomplished' }" class="panel panel-pad card">
        <div class="card-label">Accomplished</div>
        <div class="card-value">{{ accomplished() }}</div>
      </a>
      <a routerLink="/portal/confirm-resolution" class="panel panel-pad card">
        <div class="card-label">Awaiting Your Confirmation</div>
        <div class="card-value" [class.warn]="awaitingConfirmation() > 0">{{ awaitingConfirmation() }}</div>
      </a>
      <a routerLink="/portal/maintenance-history" [queryParams]="{ filter: 'escalated' }" class="panel panel-pad card">
        <div class="card-label">Escalated</div>
        <div class="card-value" [class.warn]="escalated() > 0">{{ escalated() }}</div>
      </a>
      <a routerLink="/portal/maintenance-history" class="panel panel-pad card">
        <div class="card-label">Expired Agreements</div>
        <div class="card-value" [class.warn]="expiredAgreements() > 0">{{ expiredAgreements() }}</div>
      </a>
    </div>

    <div class="panel panel-pad" style="margin-top: 1.5rem;">
      <h3>My Systems/Products</h3>
      @if (myProducts().length > 0) {
        <div class="products-grid">
          @for (p of myProducts(); track p.id) {
            <div class="product-card">
              <div class="product-name">{{ p.name }}</div>
              @if (p.description) { <div class="text-muted" style="font-size:0.8rem;">{{ p.description }}</div> }
              <div class="product-expiry">
                @if (p.expiryDate) {
                  <span [class.warn]="isExpiringSoon(p.expiryDate)" [class.expired]="isExpired(p.expiryDate)">
                    @if (isExpired(p.expiryDate)) {
                      Expired {{ p.expiryDate | date:'mediumDate' }}
                    } @else {
                      Expires {{ p.expiryDate | date:'mediumDate' }}
                    }
                  </span>
                } @else {
                  <span class="text-muted">No expiry date set</span>
                }
              </div>
            </div>
          }
        </div>
      } @else {
        <p class="text-muted" style="font-size:0.85rem;">No systems/products are on your account yet.</p>
      }
    </div>

    <div class="chart-grid">
      <div class="panel panel-pad">
        <h3 class="chart-title">Requests by Status</h3>
        <app-donut-chart [data]="statusSlices()" centerLabel="Requests" centerSuffix="" [centerOverride]="totalSupport()"></app-donut-chart>
      </div>
      <div class="panel panel-pad">
        <h3 class="chart-title">Requests by Category</h3>
        <app-count-bar-chart [chartData]="categoryBars()"></app-count-bar-chart>
      </div>
      <div class="panel panel-pad" style="grid-column: 1 / -1;">
        <h3 class="chart-title">Last 6 Months</h3>
        <app-line-chart [data]="trendSeries()" [xLabels]="trendLabels()"></app-line-chart>
      </div>
    </div>

    <div class="panel panel-pad" style="margin-top: 1.5rem;">
      <div class="section-head">
        <h3>Recent Activity</h3>
        <a routerLink="/portal/maintenance-history" class="text-muted see-all">See all →</a>
      </div>
      <div class="table-scroll"><table>
        <thead><tr><th>Ticket #</th><th>Category</th><th>Submitted</th><th>Assigned To</th><th>Status</th></tr></thead>
        <tbody>
          @for (t of recentActivity(); track t.id) {
            <tr>
              <td class="mono">{{ ticketNumber(t.id) }}</td>
              <td>{{ categoryLabel(t.category) }}</td>
              <td class="text-muted">{{ t.dateSubmitted | slice:0:10 }}</td>
              <td class="text-muted">{{ t.assignedEmployeeName || '—' }}</td>
              <td><app-badge [status]="t.status"></app-badge></td>
            </tr>
          }
          @empty { <tr><td colspan="5" class="text-muted" style="text-align:center; padding:1.5rem;">No activity yet.</td></tr> }
        </tbody>
      </table></div>
    </div>
  `,
  styles: [`
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; margin-top: 1.25rem; }
    .card { display: block; }
    .card-label { font-size: 0.78rem; color: var(--slate-500); font-weight: 600; margin-bottom: 0.4rem; }
    .card-value { font-size: 1.9rem; font-weight: 700; color: var(--navy-900); }
    .card-value.warn { color: var(--amber); }
    .section-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.9rem; }
    .see-all { font-size: 0.8rem; font-weight: 600; }
    .chart-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1rem; margin-top: 1.5rem; }
    .chart-title { margin-bottom: 0.9rem; font-size: 0.98rem; }
    .products-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.9rem; margin-top: 0.8rem; }
    .product-card { border: 1px solid var(--slate-200); border-radius: 10px; padding: 0.85rem 1rem; }
    .product-name { font-weight: 700; color: var(--navy-900); margin-bottom: 0.2rem; }
    .product-expiry { margin-top: 0.5rem; font-size: 0.82rem; }
    .product-expiry .warn { color: var(--amber); font-weight: 600; }
    .product-expiry .expired { color: var(--red, #b3261e); font-weight: 600; }
  `],
})
export class PortalDashboardComponent implements OnInit, OnDestroy {
  constructor(private auth: AuthService, private ticketsSvc: TicketService, private agreementsSvc: AgreementService, public systemProductsSvc: SystemProductService) {}

  private pollHandle: ReturnType<typeof setInterval> | undefined;

  ngOnInit(): void {
    this.refresh();
    this.pollHandle = setInterval(() => this.refresh(), REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
  }

  private refresh(): void {
    const client = this.auth.currentClient();
    if (client) {
      void this.ticketsSvc.refreshMyTickets(client.id);
      void this.systemProductsSvc.refreshForClient(client.id);
    }
  }

  client = computed(() => this.auth.currentClient());

  /** All of this client's systems/products, so multiple assigned products (and each one's own expiry) are all visible at once — not just the first/active one. */
  myProducts = computed(() => {
    const client = this.client();
    if (!client) return [];
    // Depend on byClient() so this recomputes once refreshForClient() populates the cache.
    this.systemProductsSvc.byClient();
    return this.systemProductsSvc.systemProductsFor(client.id);
  });

  /** True once a product's expiry date has passed — flagged distinctly on the dashboard so it doesn't read the same as a healthy one. */
  isExpired(expiryDate?: string): boolean {
    if (!expiryDate) return false;
    return new Date(expiryDate) < new Date(new Date().toDateString());
  }

  /** True when a product expires within 30 days — an early warning, distinct from already-expired. */
  isExpiringSoon(expiryDate?: string): boolean {
    if (!expiryDate) return false;
    const days = (new Date(expiryDate).getTime() - new Date(new Date().toDateString()).getTime()) / 86400000;
    return days >= 0 && days <= 30;
  }

  private myTickets = computed(() => {
    const client = this.client();
    return client ? this.ticketsSvc.forClient(client.id) : [];
  });

  totalSupport = computed(() => this.myTickets().length);

  pending = computed(() =>
    this.myTickets().filter(t => ['Submitted', 'Forwarded', 'Assigned', 'InProgress', 'Resolved', 'AwaitingClientConfirmation'].includes(t.status)).length
  );

  accomplished = computed(() => this.myTickets().filter(t => t.status === 'Closed').length);

  awaitingConfirmation = computed(() => this.myTickets().filter(t => t.status === 'AwaitingClientConfirmation').length);

  escalated = computed(() => this.myTickets().filter(t => t.status === 'Escalated').length);

  expiredAgreements = computed(() => {
    const client = this.client();
    if (!client) return 0;
    return this.agreementsSvc.agreements().filter(a => a.clientId === client.id && a.status === 'Expired').length;
  });

  recentActivity = computed(() => this.myTickets().slice(0, 10));

  /** Donut of the client's own requests by status — same palette as the staff dashboard. */
  statusSlices = computed((): DonutSlice[] => {
    const counts = new Map<string, number>();
    for (const t of this.myTickets()) counts.set(t.status, (counts.get(t.status) ?? 0) + 1);
    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([status, value]) => ({ label: status.replace(/([a-z])([A-Z])/g, '$1 $2'), value, color: STATUS_COLORS[status] ?? 'var(--accent)' }));
  });

  /** Which kinds of problem this client reports most. */
  categoryBars = computed((): CountBarDatum[] => {
    const counts = new Map<string, number>();
    for (const t of this.myTickets()) counts.set(t.category, (counts.get(t.category) ?? 0) + 1);
    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([category, value]) => ({ label: this.categoryLabel(category), value }));
  });

  /** Rolling 6-month window, oldest first — labels and series stay index-aligned. */
  private trendMonths = computed(() => {
    const now = new Date();
    return Array.from({ length: 6 }, (_, i) => {
      const d = new Date(now.getFullYear(), now.getMonth() - (5 - i), 1);
      return { key: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`, label: d.toLocaleString('en', { month: 'short' }) };
    });
  });

  trendLabels = computed(() => this.trendMonths().map(m => m.label));

  trendSeries = computed((): LineSeries[] => {
    const months = this.trendMonths();
    const submitted = new Map<string, number>();
    const closed = new Map<string, number>();
    for (const t of this.myTickets()) {
      const key = (t.dateSubmitted ?? '').slice(0, 7);
      if (!key) continue;
      submitted.set(key, (submitted.get(key) ?? 0) + 1);
      if (t.status === 'Closed') closed.set(key, (closed.get(key) ?? 0) + 1);
    }
    return [
      { label: 'Submitted', color: 'var(--brand-blue)', values: months.map(m => submitted.get(m.key) ?? 0) },
      { label: 'Completed', color: '#16a34a', values: months.map(m => closed.get(m.key) ?? 0) },
    ];
  });

  ticketNumber(id: string): string {
    return id.slice(0, 8).toUpperCase();
  }

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }
}
