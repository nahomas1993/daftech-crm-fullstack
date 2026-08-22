import { Component, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { TicketService } from '../../core/services/ticket.service';
import { TICKET_CATEGORY_LABELS } from '../../core/models';

@Component({
  selector: 'app-portal-reports',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    <h1>Reports</h1>
    <p class="text-muted" style="margin-top:0.3rem;">A summary of your support activity with DAFTECH.</p>

    <div class="metrics-grid" style="margin-top:1.25rem;">
      <div class="panel panel-pad metric"><div class="metric-label">Total Requests</div><div class="metric-value">{{ total() }}</div></div>
      <div class="panel panel-pad metric"><div class="metric-label">Resolved</div><div class="metric-value">{{ resolved() }}</div></div>
      <div class="panel panel-pad metric"><div class="metric-label">Resolution Rate</div><div class="metric-value">{{ resolutionRate() }}%</div></div>
      <div class="panel panel-pad metric"><div class="metric-label">Avg. Rating Given</div><div class="metric-value">{{ avgRating() != null ? (avgRating() | number:'1.1-1') + '★' : '—' }}</div></div>
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <h3 style="margin-bottom:0.9rem;">Requests by Category</h3>
      <div class="table-scroll"><table>
        <thead><tr><th>Category</th><th>Total</th><th>Resolved</th><th>Open</th></tr></thead>
        <tbody>
          @for (row of byCategory(); track row.category) {
            <tr>
              <td>{{ row.label }}</td>
              <td>{{ row.total }}</td>
              <td>{{ row.resolved }}</td>
              <td>{{ row.total - row.resolved }}</td>
            </tr>
          }
          @empty { <tr><td colspan="4" class="text-muted" style="text-align:center; padding:1.5rem;">No support history yet.</td></tr> }
        </tbody>
      </table></div>
    </div>
  `,
  styles: [`
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 1rem; }
    .metric-label { font-size: 0.75rem; color: var(--slate-500); font-weight: 600; margin-bottom: 0.4rem; }
    .metric-value { font-size: 1.5rem; font-weight: 700; color: var(--navy-900); }
  `],
})
export class PortalReportsComponent {
  constructor(private auth: AuthService, private ticketsSvc: TicketService) {}

  private myTickets = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.ticketsSvc.forClient(client.id) : [];
  });

  total = computed(() => this.myTickets().length);
  resolved = computed(() => this.myTickets().filter(t => t.status === 'Closed').length);
  resolutionRate = computed(() => this.total() > 0 ? Math.round((this.resolved() / this.total()) * 100) : 0);

  avgRating = computed(() => {
    const rated = this.myTickets().filter(t => t.satisfactionStars != null);
    if (rated.length === 0) return null;
    return rated.reduce((sum, t) => sum + (t.satisfactionStars ?? 0), 0) / rated.length;
  });

  byCategory = computed(() => {
    const map = new Map<string, { total: number; resolved: number }>();
    for (const t of this.myTickets()) {
      const entry = map.get(t.category) ?? { total: 0, resolved: 0 };
      entry.total++;
      if (t.status === 'Closed') entry.resolved++;
      map.set(t.category, entry);
    }
    return Array.from(map.entries()).map(([category, stats]) => ({
      category,
      label: TICKET_CATEGORY_LABELS[category as keyof typeof TICKET_CATEGORY_LABELS] ?? category,
      ...stats,
    }));
  });
}
