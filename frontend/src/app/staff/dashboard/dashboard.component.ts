import { Component, computed, effect } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClientService } from '../../core/services/client.service';
import { TicketService } from '../../core/services/ticket.service';
import { AgreementService } from '../../core/services/agreement.service';
import { EmployeeService } from '../../core/services/employee.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationRecipientType } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <h1>Dashboard</h1>
    <p class="text-muted" style="margin-top:0.3rem;">{{ subtitle() }}</p>

    @if (isAdmin()) {
      <div class="cards">
        <a routerLink="/admin/clients" class="panel panel-pad card">
          <div class="card-label">Active Clients</div>
          <div class="card-value">{{ activeClients() }}</div>
        </a>
        <a routerLink="/admin/signup-requests" class="panel panel-pad card">
          <div class="card-label">Pending Signup Requests</div>
          <div class="card-value" [class.warn]="pendingSignups() > 0">{{ pendingSignups() }}</div>
        </a>
        <a routerLink="/admin/tickets" class="panel panel-pad card">
          <div class="card-label">Open Tickets</div>
          <div class="card-value">{{ openTickets() }}</div>
        </a>
        <a routerLink="/admin/tickets" class="panel panel-pad card">
          <div class="card-label">Escalated (Below CSAT Threshold)</div>
          <div class="card-value" [class.warn]="escalated() > 0">{{ escalated() }}</div>
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
  `],
})
export class DashboardComponent {
  constructor(
    private clientsSvc: ClientService,
    private ticketsSvc: TicketService,
    private agreementsSvc: AgreementService,
    public employees: EmployeeService,
    private notificationsSvc: NotificationService,
    private auth: AuthService
  ) {
    effect(() => {
      const key = this.recipientKey();
      if (key) void this.notificationsSvc.loadFor(key.type, key.id);
    });
  }

  isAdmin = computed(() => this.auth.currentEmployee()?.roles.includes('Admin') ?? false);

  subtitle = computed(() => {
    if (this.isAdmin()) return 'Overview across clients, tickets, and staff workload.';
    return 'Your assigned tickets and attendance.';
  });

  activeClients = computed(() => this.clientsSvc.approvedClients().length);
  pendingSignups = computed(() => this.clientsSvc.pendingRequests().length);
  openTickets = computed(() =>
    this.ticketsSvc.tickets().filter(t => !['Resolved', 'Closed', 'Escalated'].includes(t.status)).length
  );
  escalated = computed(() => this.ticketsSvc.escalated().length);
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
}
