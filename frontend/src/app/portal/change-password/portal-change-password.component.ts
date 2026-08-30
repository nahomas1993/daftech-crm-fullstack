import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { PASSWORD_STRENGTH_HINT, passwordStrengthError } from '../../core/password-strength';
import { BrandLogoComponent } from '../../shared/brand-logo.component';

@Component({
  selector: 'app-portal-change-password',
  standalone: true,
  imports: [FormsModule, BrandLogoComponent],
  template: `
    <div class="wrap">
      <div class="card panel panel-pad">
        <app-brand-logo [size]="44" style="margin: 0 auto 0.75rem;"></app-brand-logo>
        <h2>Set a New Password</h2>
        <p class="text-muted" style="margin: 0.35rem 0 1.25rem;">
          You're signing in with a one-time password. Choose a new password to continue.
        </p>

        <label class="lbl">Current (one-time) password</label>
        <div class="pw-group">
          <input [type]="showCurrentPassword() ? 'text' : 'password'" [ngModel]="currentPassword()" (ngModelChange)="currentPassword.set($event)" autocomplete="current-password" />
          <button type="button" class="visibility-toggle" (click)="showCurrentPassword.set(!showCurrentPassword())" [attr.aria-label]="showCurrentPassword() ? 'Hide password' : 'Show password'">
            @if (showCurrentPassword()) {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
            } @else {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8Z"/><circle cx="12" cy="12" r="3"/></svg>
            }
          </button>
        </div>

        <label class="lbl" style="margin-top:0.8rem;">New password</label>
        <div class="pw-group">
          <input [type]="showNewPassword() ? 'text' : 'password'" [ngModel]="newPassword()" (ngModelChange)="newPassword.set($event)" autocomplete="new-password" />
          <button type="button" class="visibility-toggle" (click)="showNewPassword.set(!showNewPassword())" [attr.aria-label]="showNewPassword() ? 'Hide password' : 'Show password'">
            @if (showNewPassword()) {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
            } @else {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8Z"/><circle cx="12" cy="12" r="3"/></svg>
            }
          </button>
        </div>

        <label class="lbl" style="margin-top:0.8rem;">Confirm new password</label>
        <div class="pw-group">
          <input [type]="showConfirmPassword() ? 'text' : 'password'" [ngModel]="confirmPassword()" (ngModelChange)="confirmPassword.set($event)" autocomplete="new-password" (keydown.enter)="submit()" />
          <button type="button" class="visibility-toggle" (click)="showConfirmPassword.set(!showConfirmPassword())" [attr.aria-label]="showConfirmPassword() ? 'Hide password' : 'Show password'">
            @if (showConfirmPassword()) {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
            } @else {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8Z"/><circle cx="12" cy="12" r="3"/></svg>
            }
          </button>
        </div>

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
    .pw-group { position: relative; }
    .pw-group input { padding-right: 2.4rem; }
    .visibility-toggle {
      position: absolute; right: 0.55rem; top: 50%; transform: translateY(-50%);
      background: none; border: none; padding: 0.3rem;
      color: var(--slate-400); display: flex; cursor: pointer; border-radius: 6px;
    }
    .visibility-toggle:hover { color: var(--navy-800); background: var(--slate-100); }
    .hint { font-size: 0.75rem; margin-top: 0.5rem; }
    .err { margin-top: 0.9rem; padding: 0.65rem 0.8rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.83rem; }
  `],
})
export class PortalChangePasswordComponent {
  currentPassword = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  showCurrentPassword = signal(false);
  showNewPassword = signal(false);
  showConfirmPassword = signal(false);
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
