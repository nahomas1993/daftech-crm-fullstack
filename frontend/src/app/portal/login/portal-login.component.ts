import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { ForgotPasswordModalComponent } from '../../shared/forgot-password-modal.component';
import { BrandLogoComponent } from '../../shared/brand-logo.component';

@Component({
  selector: 'app-portal-login',
  standalone: true,
  imports: [FormsModule, RouterLink, ForgotPasswordModalComponent, BrandLogoComponent],
  template: `
    <div class="wrap">
      <div class="card panel panel-pad">
        <app-brand-logo [size]="44"></app-brand-logo>
        <h2>Client Portal</h2>
        <p class="text-muted" style="margin: 0.35rem 0 1.25rem;">Sign in with the username and password your Admin gave you.</p>

        <label class="lbl">Username</label>
        <div class="input-group">
          <span class="input-icon" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
          </span>
          <input type="text" [ngModel]="username()" (ngModelChange)="username.set($event)" placeholder="e.g. at2001" autocomplete="username" />
        </div>

        <label class="lbl" style="margin-top:0.8rem;">Password</label>
        <div class="input-group">
          <span class="input-icon" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
          </span>
          <input [type]="showPassword() ? 'text' : 'password'" [ngModel]="password()" (ngModelChange)="password.set($event)" placeholder="Password" autocomplete="current-password" (keydown.enter)="attempt()" />
          <button type="button" class="visibility-toggle" (click)="showPassword.set(!showPassword())" [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
            @if (showPassword()) {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
            } @else {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8Z"/><circle cx="12" cy="12" r="3"/></svg>
            }
          </button>
        </div>

        <button class="btn btn-primary" style="width:100%; margin-top:1rem;" [disabled]="submitting()" (click)="attempt()">
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>

        @if (error(); as e) { <div class="err">{{ e }}</div> }

        <button type="button" class="link-btn" (click)="showForgotPassword.set(true)">Forgot password?</button>

        <p class="alt-link">Don't have an account yet? Ask DAFTECH to register you, or request access below.</p>
        <a routerLink="/portal/signup" class="btn btn-outline btn-sm" style="width:100%;">Request Access</a>

        <p class="alt-link">Admin or Staff? <a routerLink="/admin/login">Click here to sign in</a>.</p>
      </div>
      <footer class="app-footer">© {{ year }} DAFTECH Computer Engineering. All rights reserved.</footer>
    </div>

    <app-forgot-password-modal
      [open]="showForgotPassword()"
      accountType="Client"
      (close)="showForgotPassword.set(false)"
    />
  `,
  styles: [`
    .wrap {
      min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center;
      background: var(--portal-bg); padding: 1rem;
    }
    .card { width: 380px; max-width: 100%; text-align: center; }
    .card .lbl, .card .input-group, .card .err { text-align: left; }
    app-brand-logo { display: block; margin: 0 auto 0.75rem; }
    .lbl { display: block; font-size: 0.78rem; font-weight: 600; color: var(--slate-500); margin-bottom: 0.3rem; }
    .input-group { position: relative; display: flex; align-items: center; }
    .input-icon {
      position: absolute; left: 0.75rem; display: flex; color: var(--slate-400); pointer-events: none;
    }
    .input-group input { width: 100%; padding-left: 2.5rem; }
    .visibility-toggle {
      position: absolute; right: 0.6rem; background: none; border: none; padding: 0.3rem;
      color: var(--slate-400); display: flex; cursor: pointer; border-radius: 6px;
    }
    .visibility-toggle:hover { color: var(--slate-600); background: var(--slate-100); }
    .input-group:has(.visibility-toggle) input { padding-right: 2.4rem; }
    .err { margin-top: 0.9rem; padding: 0.65rem 0.8rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.83rem; }
    .link-btn {
      display: block; margin: 0.85rem auto 0; background: none; border: none; padding: 0;
      color: var(--slate-500); font-size: 0.8rem; cursor: pointer; text-decoration: underline;
    }
    .link-btn:hover { color: var(--slate-700); }
    .alt-link { font-size: 0.78rem; margin: 1rem 0 0.6rem; text-align: center; color: var(--slate-500); }
    .alt-link a { color: var(--navy-600, #2563eb); text-decoration: underline; }
    .app-footer { margin-top: 1.25rem; font-size: 0.75rem; color: var(--slate-400); text-align: center; }
  `],
})
export class PortalLoginComponent {
  username = signal('');
  password = signal('');
  showPassword = signal(false);
  submitting = signal(false);
  error = signal<string | null>(null);
  showForgotPassword = signal(false);
  readonly year = new Date().getFullYear();

  constructor(private auth: AuthService, private router: Router) {}

  async attempt() {
    if (!this.username().trim() || !this.password()) return;
    this.submitting.set(true);
    try {
      const res = await this.auth.loginClient(this.username().trim(), this.password());
      if (res.success) {
        this.error.set(null);
        const dest = this.auth.clientMustChangePassword() ? '/portal/change-password' : '/portal/dashboard';
        this.router.navigateByUrl(dest);
      } else {
        this.error.set(res.message ?? 'Unable to log in.');
      }
    } finally {
      this.submitting.set(false);
    }
  }
}