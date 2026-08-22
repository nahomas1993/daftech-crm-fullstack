import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../core/services/auth.service';
import { SessionAccountType } from '../core/models';

/**
 * "Forgot password?" — there's no emailed reset link in this system, so
 * this just submits the username to an Admin's review queue
 * (PasswordResetRequest). The Admin issues a fresh one-time password by
 * hand, the same way a new hire is credentialed. Shared between the client
 * portal and staff login screens since the flow is identical for both,
 * distinguished only by accountType.
 */
@Component({
  selector: 'app-forgot-password-modal',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="overlay" (click)="close.emit()">
        <div class="modal panel panel-pad" (click)="$event.stopPropagation()">
          @if (!submitted()) {
            <h3>Forgot your password?</h3>
            <p class="text-muted" style="margin: 0.35rem 0 1rem; font-size: 0.85rem;">
              There's no automatic reset link — enter your username and an Admin will issue you a new temporary password by email.
            </p>

            <label class="lbl">Username</label>
            <input type="text" [ngModel]="username()" (ngModelChange)="username.set($event)" placeholder="e.g. mf4821" autocomplete="username" />

            <label class="lbl" style="margin-top:0.8rem;">Note (optional)</label>
            <input type="text" [ngModel]="note()" (ngModelChange)="note.set($event)" placeholder="e.g. lost access to old device" />

            @if (error(); as e) { <div class="err">{{ e }}</div> }

            <div class="actions">
              <button type="button" class="btn btn-outline btn-sm" (click)="close.emit()">Cancel</button>
              <button type="button" class="btn btn-primary btn-sm" [disabled]="submitting() || !username().trim()" (click)="submit()">
                {{ submitting() ? 'Sending…' : 'Send request' }}
              </button>
            </div>
          } @else {
            <h3>Request sent</h3>
            <p class="text-muted" style="margin: 0.6rem 0 1.25rem; font-size: 0.85rem;">{{ submitted() }}</p>
            <div class="actions">
              <button type="button" class="btn btn-primary btn-sm" (click)="close.emit()">Done</button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .overlay {
      position: fixed; inset: 0; background: rgba(15, 23, 42, 0.45); z-index: 100;
      display: flex; align-items: center; justify-content: center; padding: 1rem;
    }
    .modal { width: 360px; max-width: 100%; }
    .lbl { display: block; font-size: 0.78rem; font-weight: 600; color: var(--slate-500); margin-bottom: 0.3rem; }
    input { width: 100%; }
    .err { margin-top: 0.75rem; padding: 0.6rem 0.75rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.8rem; }
    .actions { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 1.25rem; }
  `],
})
export class ForgotPasswordModalComponent {
  @Input() open = false;
  /**
   * Kept as an input for backward compatibility with the dedicated staff/
   * portal login pages, which still know their own account type. The
   * unified login page doesn't know it up front (that's the point), so it
   * leaves this at the default — harmless either way, since the backend
   * (see PasswordResetService.SubmitAsync) resolves the real account type
   * itself by looking up the username, not by trusting this field.
   */
  @Input() accountType: SessionAccountType = 'Employee';
  @Output() close = new EventEmitter<void>();

  username = signal('');
  note = signal('');
  submitting = signal(false);
  submitted = signal<string | null>(null);
  error = signal<string | null>(null);

  constructor(private auth: AuthService) {}

  async submit() {
    if (!this.username().trim()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      const message = await this.auth.forgotPassword(this.accountType, this.username().trim(), this.note().trim() || undefined);
      this.submitted.set(message);
    } catch {
      this.error.set('Something went wrong sending your request. Please try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
