import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { ForgotPasswordModalComponent } from '../../shared/forgot-password-modal.component';
import { BrandLogoComponent } from '../../shared/brand-logo.component';

/**
 * Single sign-in screen for Admins, Employees, and Clients alike — replaces
 * the old separate /admin/login and /portal/login pages (both still exist
 * as thin redirects here, so no old links/bookmarks break). The server
 * (AuthController.Login) determines which kind of account the username
 * belongs to; this page never asks and never guesses. Routing after a
 * successful sign-in is driven entirely by the accountType the server
 * returns, and role/permission enforcement happens via JWT claims exactly
 * as it did before — this page is purely a UI merge, not a security change.
 *
 * Layout: standard two-pane enterprise sign-in — brand/value panel on the
 * left (hidden on small screens), form on the right.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink, ForgotPasswordModalComponent, BrandLogoComponent],
  template: `
    <div class="auth">
      <!-- Brand panel -->
      <aside class="brand-pane">
        <div class="brand-pane-inner">
          <app-brand-logo [size]="52" variant="full" tone="light"></app-brand-logo>

          <h1 class="headline">Support operations,<br />under control.</h1>
          <p class="sub">
            The DAFTECH Customer Relationship &amp; Maintenance platform — one place for client
            agreements, maintenance tickets, technician workload, and service performance.
          </p>

          <ul class="points">
            <li>
              <span class="tick" aria-hidden="true">✓</span>
              Log, assign, and track maintenance tickets end to end
            </li>
            <li>
              <span class="tick" aria-hidden="true">✓</span>
              Monitor SLA, resolution rates, and customer satisfaction
            </li>
            <li>
              <span class="tick" aria-hidden="true">✓</span>
              Give every client a self-service portal for their requests
            </li>
          </ul>
        </div>

        <p class="pane-footer">© {{ year }} DAFTECH Computer Engineering. All rights reserved.</p>
      </aside>

      <!-- Form panel -->
      <main class="form-pane">
        <div class="form-wrap">
          <div class="mobile-brand">
            <app-brand-logo [size]="44" variant="full"></app-brand-logo>
          </div>

          <h2 class="title">Sign in</h2>
          <p class="text-muted subtitle">
            Use the credentials issued to you. Admins, employees, and clients all sign in here —
            we'll take you to the right workspace.
          </p>

          <form (ngSubmit)="attempt()">
            <label class="lbl" for="username">Username</label>
            <div class="input-group">
              <span class="input-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
              </span>
              <input id="username" name="username" type="text" [ngModel]="username()" (ngModelChange)="username.set($event)" placeholder="e.g. na1001" autocomplete="username" />
            </div>

            <div class="lbl-row">
              <label class="lbl" for="password">Password</label>
              <button type="button" class="link-btn" (click)="showForgotPassword.set(true)">Forgot password?</button>
            </div>
            <div class="input-group">
              <span class="input-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
              </span>
              <input id="password" name="password" [type]="showPassword() ? 'text' : 'password'" [ngModel]="password()" (ngModelChange)="password.set($event)" placeholder="Enter your password" autocomplete="current-password" />
              <button type="button" class="visibility-toggle" (click)="showPassword.set(!showPassword())" [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
                @if (showPassword()) {
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
                } @else {
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8Z"/><circle cx="12" cy="12" r="3"/></svg>
                }
              </button>
            </div>

            @if (error(); as e) {
              <div class="result blocked" role="alert">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
                <span>{{ e }}</span>
              </div>
            }

            <button type="submit" class="btn btn-primary submit" [disabled]="submitting() || !canSubmit()">
              {{ submitting() ? 'Signing in…' : 'Sign in' }}
            </button>
          </form>

          <p class="secure-note">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
            Secure sign-in. Sessions end automatically when idle.
          </p>
        </div>
      </main>
    </div>

    <app-forgot-password-modal
      [open]="showForgotPassword()"
      (close)="showForgotPassword.set(false)"
    />
  `,
  styles: [`
    .auth { min-height: 100vh; display: grid; grid-template-columns: 1.05fr 1fr; }

    /* ---- Brand panel ---- */
    .brand-pane {
      position: relative;
      display: flex; flex-direction: column; justify-content: space-between;
      padding: 3rem 3.5rem;
      color: #fff;
      background:
        radial-gradient(900px 480px at 12% 8%, rgba(52, 87, 178, 0.42), transparent 60%),
        radial-gradient(700px 420px at 88% 92%, rgba(224, 52, 43, 0.32), transparent 62%),
        linear-gradient(160deg, #101214 0%, var(--navy-950) 45%, #0b0d0f 100%);
      overflow: hidden;
    }
    .brand-pane::after {
      content: '';
      position: absolute; inset: 0;
      background-image:
        linear-gradient(rgba(255,255,255,0.035) 1px, transparent 1px),
        linear-gradient(90deg, rgba(255,255,255,0.035) 1px, transparent 1px);
      background-size: 42px 42px;
      pointer-events: none;
    }
    .brand-pane-inner { position: relative; z-index: 1; margin-top: auto; margin-bottom: auto; max-width: 30rem; }
    .headline {
      margin: 2.75rem 0 0; font-size: 2.35rem; line-height: 1.15; font-weight: 700;
      letter-spacing: -0.02em; color: #fff;
    }
    .sub { margin-top: 1rem; font-size: 0.95rem; line-height: 1.65; color: rgba(255,255,255,0.72); }
    .points { list-style: none; margin: 2rem 0 0; padding: 0; display: flex; flex-direction: column; gap: 0.85rem; }
    .points li {
      display: flex; align-items: flex-start; gap: 0.65rem;
      font-size: 0.88rem; color: rgba(255,255,255,0.85); line-height: 1.5;
    }
    .tick {
      flex-shrink: 0; width: 20px; height: 20px; border-radius: 6px;
      display: inline-flex; align-items: center; justify-content: center;
      background: rgba(52, 87, 178, 0.28); color: #93b1ff; font-size: 0.7rem; font-weight: 700;
    }
    .pane-footer { position: relative; z-index: 1; font-size: 0.72rem; color: rgba(255,255,255,0.45); }

    /* ---- Form panel ---- */
    .form-pane {
      display: flex; align-items: center; justify-content: center;
      padding: 3rem 1.5rem; background: var(--slate-50);
    }
    .form-wrap { width: 100%; max-width: 380px; }
    .mobile-brand { display: none; margin-bottom: 1.75rem; }
    .title { font-size: 1.6rem; letter-spacing: -0.015em; }
    .subtitle { margin: 0.5rem 0 1.75rem; font-size: 0.86rem; line-height: 1.6; }

    .lbl { display: block; font-size: 0.78rem; font-weight: 600; color: var(--navy-800); margin-bottom: 0.35rem; }
    .lbl-row { display: flex; align-items: baseline; justify-content: space-between; margin-top: 1rem; }
    .lbl-row .lbl { margin-bottom: 0.35rem; }

    .input-group { position: relative; display: flex; align-items: center; }
    .input-icon { position: absolute; left: 0.8rem; display: flex; color: var(--slate-400); pointer-events: none; }
    .input-group input {
      width: 100%; padding-left: 2.5rem; height: 44px;
      background: #fff; border: 1px solid var(--slate-200); border-radius: 10px;
    }
    .input-group input:focus {
      outline: none; border-color: var(--accent);
      box-shadow: 0 0 0 3px rgba(52, 87, 178, 0.14);
    }
    .visibility-toggle {
      position: absolute; right: 0.55rem; background: none; border: none; padding: 0.3rem;
      color: var(--slate-400); display: flex; cursor: pointer; border-radius: 6px;
    }
    .visibility-toggle:hover { color: var(--navy-800); background: var(--slate-100); }
    .input-group:has(.visibility-toggle) input { padding-right: 2.4rem; }

    .submit { width: 100%; margin-top: 1.35rem; height: 44px; font-weight: 600; border-radius: 10px; }
    .submit:disabled { opacity: 0.6; cursor: not-allowed; }

    .result {
      display: flex; align-items: center; gap: 0.5rem;
      margin-top: 1.1rem; padding: 0.7rem 0.85rem; border-radius: 10px;
      font-size: 0.83rem; line-height: 1.45;
      background: var(--red-bg); color: var(--red);
    }
    .result svg { flex-shrink: 0; }

    .link-btn {
      background: none; border: none; padding: 0;
      color: var(--accent); font-size: 0.78rem; font-weight: 600; cursor: pointer;
    }
    .link-btn:hover { text-decoration: underline; }

    .divider {
      display: flex; align-items: center; gap: 0.75rem;
      margin: 1.75rem 0 1rem; color: var(--slate-400); font-size: 0.72rem;
      text-transform: uppercase; letter-spacing: 0.06em;
    }
    .divider::before, .divider::after { content: ''; flex: 1; height: 1px; background: var(--slate-200); }

    .alt-link { font-size: 0.83rem; text-align: center; color: var(--slate-500); }
    .alt-link a { color: var(--accent); font-weight: 600; }
    .alt-link a:hover { text-decoration: underline; }

    .secure-note {
      display: flex; align-items: center; justify-content: center; gap: 0.4rem;
      margin-top: 2rem; font-size: 0.72rem; color: var(--slate-400);
    }

    @media (max-width: 900px) {
      .auth { grid-template-columns: 1fr; }
      .brand-pane { display: none; }
      .mobile-brand { display: flex; justify-content: center; }
      .form-pane { padding: 2.5rem 1.25rem; }
      .title, .subtitle { text-align: center; }
    }
  `],
})
export class LoginComponent {
  username = signal('');
  password = signal('');
  showPassword = signal(false);
  submitting = signal(false);
  error = signal<string | null>(null);
  showForgotPassword = signal(false);
  readonly year = new Date().getFullYear();

  constructor(private auth: AuthService, private router: Router) {}

  canSubmit() {
    return this.username().trim().length > 0 && this.password().length > 0;
  }

  async attempt() {
    if (!this.username().trim() || !this.password()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      const res = await this.auth.login(this.username().trim(), this.password());
      if (!res.success) {
        this.error.set(res.message ?? 'Unable to log in.');
        return;
      }

      if (res.accountType === 'Employee') {
        const dest = this.auth.staffMustChangePassword() ? '/admin/change-password' : '/admin/dashboard';
        this.router.navigateByUrl(dest);
      } else if (res.accountType === 'Client') {
        const dest = this.auth.clientMustChangePassword() ? '/portal/change-password' : '/portal/dashboard';
        this.router.navigateByUrl(dest);
      }
    } catch (err) {
      // A thrown error here (network failure, CORS, unexpected non-2xx
      // response) previously propagated silently past the finally block
      // below with nothing shown to the user — the form just reset to
      // idle after a delay, looking like nothing happened. Surface it
      // instead so a real connectivity/server problem is distinguishable
      // from an actual wrong-password rejection (which comes back as
      // res.success === false above, not a thrown exception).
      console.error('Login request failed', err);
      this.error.set('Could not reach the server. Please check your connection and try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
