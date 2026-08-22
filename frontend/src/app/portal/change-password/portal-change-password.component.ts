import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { PASSWORD_STRENGTH_HINT, passwordStrengthError } from '../../core/password-strength';

@Component({
  selector: 'app-portal-change-password',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <div class="card panel panel-pad">
        <img src="assets/daftech-logo.png" alt="DAFTECH" class="brand-logo-img brand-logo-md" style="margin: 0 auto 0.75rem;" />
        <h2>Set a New Password</h2>
        <p class="text-muted" style="margin: 0.35rem 0 1.25rem;">
          You're signing in with a one-time password. Choose a new password to continue.
        </p>

        <label class="lbl">Current (one-time) password</label>
        <input type="password" [ngModel]="currentPassword()" (ngModelChange)="currentPassword.set($event)" autocomplete="current-password" />

        <label class="lbl" style="margin-top:0.8rem;">New password</label>
        <input type="password" [ngModel]="newPassword()" (ngModelChange)="newPassword.set($event)" autocomplete="new-password" />

        <label class="lbl" style="margin-top:0.8rem;">Confirm new password</label>
        <input type="password" [ngModel]="confirmPassword()" (ngModelChange)="confirmPassword.set($event)" autocomplete="new-password" (keydown.enter)="submit()" />

        <p class="text-muted hint">{{ passwordHint }}</p>

        @if (error()) { <div class="err">{{ error() }}</div> }

        <button class="btn btn-primary" style="width:100%; margin-top:1rem;" [disabled]="submitting()" (click)="submit()">
          {{ submitting() ? 'Saving…' : 'Change Password & Continue' }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: var(--portal-bg); padding: 1rem; }
    .card { width: 400px; text-align: center; }
    .card .lbl, .card input, .card .hint, .card .err { text-align: left; }
    .lbl { display: block; font-size: 0.78rem; font-weight: 600; color: var(--slate-500); margin-bottom: 0.3rem; }
    input { width: 100%; }
    .hint { font-size: 0.75rem; margin-top: 0.5rem; }
    .err { margin-top: 0.9rem; padding: 0.65rem 0.8rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.83rem; }
  `],
})
export class PortalChangePasswordComponent {
  currentPassword = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  submitting = signal(false);
  error = signal<string | null>(null);
  readonly passwordHint = PASSWORD_STRENGTH_HINT;

  constructor(private auth: AuthService, private router: Router) {}

  async submit() {
    this.error.set(null);

    if (!this.currentPassword() || !this.newPassword() || !this.confirmPassword()) {
      this.error.set('Please fill in all three fields.');
      return;
    }
    if (this.newPassword() !== this.confirmPassword()) {
      this.error.set('New password and confirmation do not match.');
      return;
    }
    const strengthError = passwordStrengthError(this.newPassword());
    if (strengthError) {
      this.error.set(strengthError);
      return;
    }

    this.submitting.set(true);
    try {
      await this.auth.changeClientPassword(this.currentPassword(), this.newPassword(), this.confirmPassword());
      this.router.navigateByUrl('/portal/dashboard');
    } catch (e: any) {
      this.error.set(e?.error?.text ?? e?.error ?? 'Could not change password — check your current password and try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
