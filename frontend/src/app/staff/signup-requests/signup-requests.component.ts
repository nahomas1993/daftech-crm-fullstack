import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';

@Component({
  selector: 'app-signup-requests',
  standalone: true,
  template: `
    <h1>Client Signup Requests</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Review and approve or reject pending portal access requests.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="table-scroll"><table>
        <thead><tr><th>Client Name</th><th>ID Number</th><th>Phone</th><th>Office</th><th>Location</th><th>Submitted</th><th></th></tr></thead>
        <tbody>
          @for (r of clients.pendingRequests(); track r.id) {
            <tr>
              <td>{{ r.name }}</td>
              <td class="mono text-muted">{{ r.idNumber }}</td>
              <td>{{ r.phoneNumber }}</td>
              <td>{{ r.office }}</td>
              <td>{{ r.location }}</td>
              <td class="text-muted">{{ r.onboardingDate }}</td>
              <td class="actions">
                <button class="btn btn-primary btn-sm" (click)="approve(r.id)">Approve</button>
                <button class="btn btn-danger btn-sm" (click)="startReject(r.id)">Reject</button>
              </td>
            </tr>
            @if (rejectingId() === r.id) {
              <tr>
                <td colspan="7">
                  <div class="reject-box">
                    <input type="text" placeholder="Reason for rejection…" [ngModel]="reason()" (ngModelChange)="reason.set($event)" />
                    <button class="btn btn-danger btn-sm" (click)="confirmReject(r.id)">Confirm Reject</button>
                    <button class="btn btn-outline btn-sm" (click)="cancelReject()">Cancel</button>
                  </div>
                </td>
              </tr>
            }
          }
          @empty {
            <tr><td colspan="7" class="text-muted" style="text-align:center; padding:1.5rem;">No pending signup requests.</td></tr>
          }
        </tbody>
      </table></div>
    </div>
  `,
  styles: [`
    .actions { display: flex; gap: 0.4rem; }
    .reject-box { display: flex; gap: 0.5rem; padding: 0.75rem 0.5rem; background: var(--slate-50); border-radius: 8px; }
    .reject-box input { flex: 1; }
  `],
  imports: [FormsModule],
})
export class SignupRequestsComponent {
  rejectingId = signal<string | null>(null);
  reason = signal('');

  constructor(public clients: ClientService) {}

  async approve(id: string) {
    await this.clients.approve(id);
  }

  startReject(id: string) {
    this.rejectingId.set(id);
    this.reason.set('');
  }

  cancelReject() {
    this.rejectingId.set(null);
  }

  async confirmReject(id: string) {
    await this.clients.reject(id, this.reason() || 'No reason provided.');
    this.rejectingId.set(null);
  }
}
