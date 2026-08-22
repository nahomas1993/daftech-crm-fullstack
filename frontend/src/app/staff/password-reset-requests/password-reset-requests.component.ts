import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { PasswordResetService } from '../../core/services/password-reset.service';
import { PasswordResetOtpIssuedResult } from '../../core/models';

@Component({
  selector: 'app-password-reset-requests',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h1>Password Reset Requests</h1>
    <p class="text-muted" style="margin-top:0.3rem;">
      There's no automatic reset link in DAFTECH CRM — review requests below and issue a fresh one-time password by hand, the same way a new hire is credentialed.
    </p>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="table-scroll"><table>
        <thead><tr><th>Account</th><th>Type</th><th>Username</th><th>Note</th><th>Requested From</th><th>Requested</th><th></th></tr></thead>
        <tbody>
          @for (r of resets.pending(); track r.id) {
            <tr>
              <td>{{ r.displayName }}<div class="text-muted" style="font-size:0.75rem;">{{ r.email }}</div></td>
              <td>{{ r.accountType }}</td>
              <td class="mono">{{ r.username }}</td>
              <td class="text-muted">{{ r.note || '—' }}</td>
              <td class="mono text-muted">{{ r.requestIpAddress }}</td>
              <td class="text-muted">{{ r.requestedAt | date:'short' }}</td>
              <td class="actions">
                <button class="btn btn-primary btn-sm" [disabled]="issuingId() === r.id" (click)="issueOtp(r.id)">
                  {{ issuingId() === r.id ? 'Issuing…' : 'Issue New OTP' }}
                </button>
                <button class="btn btn-danger btn-sm" (click)="startDismiss(r.id)">Dismiss</button>
              </td>
            </tr>
            @if (dismissingId() === r.id) {
              <tr>
                <td colspan="7">
                  <div class="reject-box">
                    <input type="text" placeholder="Reason for dismissing (optional)…" [ngModel]="reason()" (ngModelChange)="reason.set($event)" />
                    <button class="btn btn-danger btn-sm" (click)="confirmDismiss(r.id)">Confirm Dismiss</button>
                    <button class="btn btn-outline btn-sm" (click)="cancelDismiss()">Cancel</button>
                  </div>
                </td>
              </tr>
            }
          }
          @empty {
            <tr><td colspan="7" class="text-muted" style="text-align:center; padding:1.5rem;">No pending password reset requests.</td></tr>
          }
        </tbody>
      </table></div>
    </div>

    @if (issuedResult(); as r) {
      <div class="overlay" (click)="issuedResult.set(null)">
        <div class="modal panel panel-pad" (click)="$event.stopPropagation()">
          <h3>New one-time password issued</h3>
          <p class="text-muted" style="margin: 0.5rem 0 1rem; font-size: 0.85rem;">
            This is shown once — it will not be retrievable again. The user will also need it if the credential email didn't send.
          </p>
          <div class="otp-box">
            <div><span class="text-muted">Username:</span> <span class="mono">{{ r.username }}</span></div>
            <div><span class="text-muted">Temporary password:</span> <span class="mono">{{ r.oneTimePassword }}</span></div>
          </div>
          @if (r.emailSent) {
            <p class="sent-ok">✅ Emailed to the account on file.</p>
          } @else {
            <p class="sent-fail">⚠️ Email failed to send{{ r.emailError ? ': ' + r.emailError : '' }} — relay these credentials manually.</p>
          }
          <div class="actions">
            <button type="button" class="btn btn-primary btn-sm" (click)="issuedResult.set(null)">Done</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .actions { display: flex; gap: 0.4rem; }
    .reject-box { display: flex; gap: 0.5rem; padding: 0.75rem 0.5rem; background: var(--slate-50); border-radius: 8px; }
    .reject-box input { flex: 1; }
    .overlay {
      position: fixed; inset: 0; background: rgba(15, 23, 42, 0.45); z-index: 100;
      display: flex; align-items: center; justify-content: center; padding: 1rem;
    }
    .modal { width: 400px; max-width: 100%; }
    .otp-box { background: var(--slate-50); border-radius: 8px; padding: 0.85rem; display: flex; flex-direction: column; gap: 0.4rem; font-size: 0.9rem; }
    .sent-ok { color: var(--green); font-size: 0.85rem; margin: 0.85rem 0 0; }
    .sent-fail { color: var(--red); font-size: 0.85rem; margin: 0.85rem 0 0; }
    .actions { display: flex; justify-content: flex-end; margin-top: 1.1rem; }
  `],
})
export class PasswordResetRequestsComponent implements OnInit {
  issuingId = signal<string | null>(null);
  dismissingId = signal<string | null>(null);
  reason = signal('');
  issuedResult = signal<PasswordResetOtpIssuedResult | null>(null);

  constructor(public resets: PasswordResetService) {}

  ngOnInit() {
    void this.resets.refreshPending();
  }

  async issueOtp(id: string) {
    this.issuingId.set(id);
    try {
      const result = await this.resets.issueOtp(id);
      this.issuedResult.set(result);
    } finally {
      this.issuingId.set(null);
    }
  }

  startDismiss(id: string) {
    this.dismissingId.set(id);
    this.reason.set('');
  }

  cancelDismiss() {
    this.dismissingId.set(null);
  }

  async confirmDismiss(id: string) {
    await this.resets.dismiss(id, this.reason());
    this.dismissingId.set(null);
  }
}
