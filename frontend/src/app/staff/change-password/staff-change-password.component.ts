import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { PASSWORD_STRENGTH_HINT, passwordStrengthError } from '../../core/password-strength';
import { DaftechLogoComponent } from '../../shared/daftech-logo.component';

@Component({
  selector: 'app-staff-change-password',
  standalone: true,
  imports: [FormsModule, DaftechLogoComponent],
  template: `
    <div class="wrap">
      <div class="card panel panel-pad">
        <daftech-logo variant="full" [size]="64" class="brand-logo brand-logo-block"></daftech-logo>
        <h2>Set a New Password</h2>
        <p class="text-muted" style="margin: 0.35rem 0 1.25rem;">
          You're signing in with a one-time password from your Admin. Choose a new password to continue —
          you won't be able to use the app until this is done.
        </p>

        <label class="lbl">Current (one-time) password</label>
        <div class="pw-field">
          <input [type]="showCurrent() ? 'text' : 'password'" [ngModel]="currentPassword()" (ngModelChange)="currentPassword.set($event)" autocomplete="current-password" />
          <button type="button" class="pw-toggle" (click)="showCurrent.set(!showCurrent())" [attr.aria-label]="showCurrent() ? 'Hide password' : 'Show password'">
            {{ showCurrent() ? 'Hide' : 'Show' }}
          </button>
        </div>

        <label class="lbl" style="margin-top:0.8rem;">New password</label>
        <div class="pw-field">
          <input [type]="showNew() ? 'text' : 'password'" [ngModel]="newPassword()" (ngModelChange)="newPassword.set($event)" autocomplete="new-password" />
          <button type="button" class="pw-toggle" (click)="showNew.set(!showNew())" [attr.aria-label]="showNew() ? 'Hide password' : 'Show password'">
            {{ showNew() ? 'Hide' : 'Show' }}
          </button>
        </div>

        <label class="lbl" style="margin-top:0.8rem;">Confirm new password</label>
        <div class="pw-field">
          <input [type]="showConfirm() ? 'text' : 'password'" [ngModel]="confirmPassword()" (ngModelChange)="confirmPassword.set($event)" autocomplete="new-password" (keydown.enter)="submit()" />
          <button type="button" class="pw-toggle" (click)="showConfirm.set(!showConfirm())" [attr.aria-label]="showConfirm() ? 'Hide password' : 'Show password'">
            {{ showConfirm() ? 'Hide' : 'Show' }}
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
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: var(--navy-950); padding: 1rem; }
    .card { width: 400px; text-align: center; }
    .card .lbl, .card input, .card .hint, .card .err { text-align: left; }
    .lbl { display: block; font-size: 0.78rem; font-weight: 600; color: var(--slate-500); margin-bottom: 0.3rem; }
    input { width: 100%; }
    .pw-field { position: relative; display: flex; align-items: center; }
    .pw-field input { padding-right: 3.4rem; }
    .pw-toggle {
      position: absolute; right: 0.6rem; background: none; border: none; cursor: pointer;
      font-size: 0.72rem; font-weight: 600; color: var(--slate-500); padding: 0.2rem 0.3rem;
    }
    .pw-toggle:hover { color: var(--navy-950); }
    .hint { font-size: 0.75rem; margin-top: 0.5rem; }
    .err { margin-top: 0.9rem; padding: 0.65rem 0.8rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.83rem; }
  `],
})
export class StaffChangePasswordComponent {
  currentPassword = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  showCurrent = signal(false);
  showNew = signal(false);
  showConfirm = signal(false);
  submitting = signal(false);
  error = signal<string | null>(null);
  readonly passwordHint = PASSWORD_STRENGTH_HINT;

  constructor(private auth: AuthService, private router: Router) {}

  async submit() {
    this.error.set(null);

    // Trim the one-time password: it's a 10-character random string that
    // people usually copy-paste out of the credential email, and a stray
    // trailing space/newline from that paste silently fails hash
    // verification while showing an input that "looks" identical to the
    // one that was emailed. New/confirm passwords are left untrimmed since
    // a deliberate leading/trailing space in a self-chosen password is
    // technically valid and shouldn't be silently altered.
    const currentPw = this.currentPassword().trim();

    if (!currentPw || !this.newPassword() || !this.confirmPassword()) {
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
      await this.auth.changeEmployeePassword(currentPw, this.newPassword(), this.confirmPassword());
      this.router.navigateByUrl('/admin/dashboard');
    } catch (e: any) {
      this.error.set(e?.error?.text ?? e?.error ?? 'Could not change password — check your current password and try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
