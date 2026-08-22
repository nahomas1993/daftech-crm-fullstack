/**
 * ⚠️ NOT CURRENTLY REACHABLE — app.routes.ts redirects 'portal/my-tickets'
 * straight to 'portal/maintenance-history', which now owns the client's
 * ticket-list view. Kept, not deleted, alongside submit-issue.component.ts
 * — see that file's header comment for why. Safe to delete once
 * MaintenanceHistoryComponent has fully absorbed anything useful this file
 * still does that it doesn't.
 */
import { Component, OnDestroy, OnInit, computed } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TicketService } from '../../core/services/ticket.service';
import { BadgeComponent } from '../../shared/badge.component';
import { TICKET_CATEGORY_LABELS } from '../../core/models';

/** How often to re-pull ticket status while this page is open, so a
 * technician resolving a ticket shows up here without the client having
 * to navigate away and back. There's no push channel (SignalR) wired up
 * on the client portal yet, so a short poll is the reliable stand-in. */
const REFRESH_INTERVAL_MS = 20_000;

@Component({
  selector: 'app-my-tickets',
  standalone: true,
  imports: [BadgeComponent, SlicePipe, RouterLink],
  template: `
    <h1>My Tickets</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Your submitted issues and their current status.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="table-scroll"><table>
        <thead><tr><th>Ticket</th><th>Category</th><th>Submitted</th><th>Chargeable</th><th>Status</th><th>Your Rating</th><th></th></tr></thead>
        <tbody>
          @for (t of tickets(); track t.id) {
            <tr>
              <td class="mono">{{ t.id.slice(0,8) }}</td>
              <td>{{ categoryLabel(t.category) }}</td>
              <td class="text-muted">{{ t.dateSubmitted | slice:0:10 }}</td>
              <td><app-badge [status]="t.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
              <td><app-badge [status]="t.status"></app-badge></td>
              <td class="text-muted">{{ t.satisfactionStars ? (t.satisfactionStars + '★') : '—' }}</td>
              <td>
                @if (t.status === 'Closed') {
                  <a [routerLink]="['/portal/survey', t.id]" class="btn btn-outline btn-sm">Take Survey</a>
                }
              </td>
            </tr>
          }
          @empty { <tr><td colspan="7" class="text-muted" style="text-align:center; padding:1.5rem;">You haven't submitted any issues yet.</td></tr> }
        </tbody>
      </table></div>
    </div>
  `,
})
export class MyTicketsComponent implements OnInit, OnDestroy {
  constructor(private auth: AuthService, private ticketsSvc: TicketService) {}

  tickets = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.ticketsSvc.forClient(client.id) : [];
  });

  private pollHandle: ReturnType<typeof setInterval> | undefined;

  ngOnInit(): void {
    this.refresh();
    // Poll while the page is open so a technician's status change (e.g.
    // marking a ticket Resolved) shows up here without a manual refresh.
    this.pollHandle = setInterval(() => this.refresh(), REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
  }

  private refresh(): void {
    const client = this.auth.currentClient();
    if (client) void this.ticketsSvc.refreshMyTickets(client.id);
  }

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }
}
