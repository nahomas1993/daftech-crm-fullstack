import { Component, computed, effect, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { TicketService } from '../../core/services/ticket.service';
import { AgreementService } from '../../core/services/agreement.service';
import { DaftechLogoComponent } from '../../shared/daftech-logo.component';

@Component({
  selector: 'app-portal-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, DaftechLogoComponent],
  template: `
    <div class="shell">
      @if (menuOpen()) {
        <div class="backdrop" (click)="closeMenu()"></div>
      }

      <header class="topbar">
        <div class="brand">
          <daftech-logo [size]="30" class="brand-logo"></daftech-logo>
          <span class="brand-name">DAFTECH Client Portal</span>
        </div>

        <button class="hamburger" (click)="toggleMenu()" aria-label="Open menu">
          <span></span><span></span><span></span>
        </button>

        <nav [class.open]="menuOpen()">
          <a routerLink="/portal/dashboard" routerLinkActive="active" (click)="closeMenu()">Dashboard</a>
          <a routerLink="/portal/agreements" routerLinkActive="active" (click)="closeMenu()">Agreements</a>
          <a routerLink="/portal/maintenance-history" routerLinkActive="active" (click)="closeMenu()">Maintenance History</a>
          <a routerLink="/portal/confirm-resolution" routerLinkActive="active" class="bell" (click)="closeMenu()">
            Confirm Resolution
            @if (awaitingCount() > 0) { <span class="bell-count">{{ awaitingCount() }}</span> }
          </a>
          <a routerLink="/portal/notifications" routerLinkActive="active" class="bell" (click)="closeMenu()">
            Notifications
            @if (unread() > 0) { <span class="bell-count">{{ unread() }}</span> }
          </a>
          <a routerLink="/portal/reports" routerLinkActive="active" (click)="closeMenu()">Reports</a>
          <div class="who mobile-who">
            <span>{{ auth.currentClient()?.name }}</span>
            <button class="btn btn-outline btn-sm" (click)="logout()">Log out</button>
          </div>
        </nav>

        <div class="who desktop-who">
          <span>{{ auth.currentClient()?.name }}</span>
          <button class="btn btn-outline btn-sm" (click)="logout()">Log out</button>
        </div>
      </header>
      <main class="content">
        <router-outlet></router-outlet>
      </main>
      <footer class="app-footer">© {{ year }} DAFTECH Computer Engineering. All rights reserved.</footer>
    </div>
  `,
  styles: [`
    .shell { min-height: 100vh; background: var(--portal-bg); }
    .backdrop { display: none; }
    .topbar {
      background: #fff; border-bottom: 1px solid var(--slate-200); padding: 0.8rem 1.5rem;
      display: flex; align-items: center; gap: 1.5rem; flex-wrap: wrap; position: relative;
    }
    .brand { display: flex; align-items: center; gap: 0.55rem; }
    .brand-name { font-weight: 600; font-size: 0.9rem; }
    nav { display: flex; gap: 1.2rem; flex: 1; }
    nav a { font-size: 0.86rem; color: var(--slate-500); font-weight: 500; padding: 0.3rem 0; position: relative; }
    nav a.active { color: var(--portal-accent); }
    .bell-count {
      background: var(--red); color: #fff; font-size: 0.62rem; font-weight: 700;
      border-radius: 999px; padding: 0.05rem 0.35rem; margin-left: 0.3rem;
    }
    .who { display: flex; align-items: center; gap: 0.7rem; font-size: 0.85rem; }
    .mobile-who { display: none; }
    .content { padding: 2rem 1.5rem; max-width: 900px; margin: 0 auto; }
    .app-footer {
      padding: 0.9rem 1.5rem; font-size: 0.75rem; color: var(--slate-400);
      border-top: 1px solid var(--slate-200); text-align: center;
    }
    .hamburger {
      display: none; flex-direction: column; justify-content: center; gap: 4px;
      background: none; border: none; padding: 0.4rem; cursor: pointer; margin-left: auto;
    }
    .hamburger span { width: 20px; height: 2px; background: var(--navy-800); border-radius: 2px; }

    /* Mobile: nav collapses into a hamburger-triggered dropdown panel */
    @media (max-width: 720px) {
      .hamburger { display: flex; }
      .desktop-who { display: none; }
      nav {
        display: none; position: absolute; top: 100%; left: 0; right: 0; z-index: 40;
        background: #fff; border-bottom: 1px solid var(--slate-200); flex-direction: column;
        gap: 0; padding: 0.5rem 0; box-shadow: 0 8px 16px rgba(0,0,0,0.08);
      }
      nav.open { display: flex; }
      nav a { padding: 0.75rem 1.5rem; border-bottom: 1px solid var(--slate-100); }
      .mobile-who {
        display: flex; justify-content: space-between; align-items: center;
        padding: 0.9rem 1.5rem 0.6rem; margin-top: 0.3rem; border-top: 1px solid var(--slate-200);
      }
      .backdrop { display: block; position: fixed; inset: 0; background: rgba(15,23,42,0.35); z-index: 30; }
      .content { padding: 1.25rem 1rem; }
      .app-footer { padding: 0.8rem 1rem; }
    }
  `],
})
export class PortalShellComponent {
  menuOpen = signal(false);
  readonly year = new Date().getFullYear();

  constructor(
    public auth: AuthService,
    private notifications: NotificationService,
    private ticketsSvc: TicketService,
    private agreementsSvc: AgreementService,
    private router: Router
  ) {
    effect(() => {
      const client = this.auth.currentClient();
      if (client) {
        void this.notifications.loadFor('Client', client.id);
        // Every portal page (dashboard, maintenance history, confirm
        // resolution, reports) reads tickets/agreements via forClient(),
        // which filters the client-scoped cache below — populate it once
        // here so no individual page needs to remember to fetch it.
        void this.ticketsSvc.refreshMyTickets(client.id);
        void this.agreementsSvc.refreshMyAgreements(client.id);
      }
    });

    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.menuOpen.set(false));
  }

  toggleMenu() {
    this.menuOpen.update(v => !v);
  }

  closeMenu() {
    this.menuOpen.set(false);
  }

  awaitingCount = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.ticketsSvc.awaitingConfirmationForClient(client.id).length : 0;
  });

  unread = computed(() => {
    const client = this.auth.currentClient();
    if (!client) return 0;
    return this.notifications.unreadCountFor('Client', client.id);
  });

  async logout() {
    await this.auth.logoutClient();
    this.router.navigateByUrl('/login');
  }
}