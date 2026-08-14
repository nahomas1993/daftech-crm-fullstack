import { Component, computed } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TicketService } from '../../core/services/ticket.service';
import { BadgeComponent } from '../../shared/badge.component';
import { TICKET_CATEGORY_LABELS } from '../../core/models';

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
export class MyTicketsComponent {
  constructor(private auth: AuthService, private ticketsSvc: TicketService) {}

  tickets = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.ticketsSvc.forClient(client.id) : [];
  });

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }
}
