import { Component, computed, effect, OnDestroy, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { EmployeeRole, NotificationRecipientType } from '../../core/models';
import { BrandLogoComponent } from '../../shared/brand-logo.component';

interface NavItem {
  label: string;
  path: string;
  icon: string;
  rolesAllowed?: EmployeeRole[];
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/admin/dashboard', icon: '📊' },
  { label: 'Clients', path: '/admin/clients', icon: '🏢', rolesAllowed: ['Admin'] },
  { label: 'Password Reset Requests', path: '/admin/password-reset-requests', icon: '🔑', rolesAllowed: ['Admin'] },
  { label: 'Agreements', path: '/admin/agreements', icon: '📄', rolesAllowed: ['Admin'] },
  { label: 'Tickets', path: '/admin/tickets', icon: '🎫' },
  { label: 'My Trainings', path: '/admin/my-trainings', icon: '🎓', rolesAllowed: ['Trainer'] },
  { label: 'Employees', path: '/admin/employees', icon: '👥', rolesAllowed: ['Admin'] },
  { label: 'Employee Performance', path: '/admin/employee-performance', icon: '📈', rolesAllowed: ['Admin'] },
  { label: 'Maintenance History', path: '/admin/maintenance', icon: '🛠️', rolesAllowed: ['Admin'] },
  { label: 'Notifications', path: '/admin/notifications', icon: '🔔' },
  { label: 'Reports', path: '/admin/reports', icon: '📈', rolesAllowed: ['Admin'] },
  { label: 'Session Activity', path: '/admin/session-activity', icon: '🟢', rolesAllowed: ['Admin'] },
  { label: 'Settings', path: '/admin/settings', icon: '⚙️' },
];

@Component({
  selector: 'app-staff-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BrandLogoComponent],
  template: `
    <div class="shell">
      <!-- Backdrop — clicking outside the open mobile sidebar closes it -->
      @if (sidebarOpen()) {
        <div class="backdrop" (click)="closeSidebar()"></div>
      }

      <aside class="sidebar" [class.open]="sidebarOpen()">
        <div class="brand">
          <app-brand-logo [size]="34"></app-brand-logo>
          <div>
            <div class="brand-name">DAFTECH</div>
            <div class="brand-sub">Admin / Staff</div>
          </div>
          <button class="close-btn" (click)="closeSidebar()" aria-label="Close menu">✕</button>
        </div>
        <nav>
          @for (item of visibleNavItems(); track item.path) {
            <a [routerLink]="item.path" routerLinkActive="active" class="nav-link" (click)="closeSidebar()">
              <span class="nav-icon">{{ item.icon }}</span>
              <span>{{ item.label }}</span>
            </a>
          }
        </nav>
        <div class="sidebar-footer">
          <div class="who">
            <div class="who-name">{{ auth.currentEmployee()?.fullName }}</div>
            <div class="who-role">{{ auth.currentEmployee()?.roles?.join(', ') }}</div>
          </div>
          <button class="btn btn-outline btn-sm" (click)="logout()">Log out</button>
        </div>
      </aside>

      <div class="main">
        <header class="topbar">
          <button class="hamburger" (click)="toggleSidebar()" aria-label="Open menu">
            <span></span><span></span><span></span>
          </button>
          <span class="et-clock" title="Ethiopia time (East Africa Time, UTC+3)">{{ ethiopiaTime() }}</span>
          <a routerLink="/admin/notifications" class="bell">
            🔔
            @if (unread() > 0) {
              <span class="bell-count">{{ unread() }}</span>
            }
          </a>
        </header>
        <main class="content">
          <router-outlet></router-outlet>
        </main>
        <footer class="app-footer">© {{ year }} DAFTECH Computer Engineering. All rights reserved.</footer>
      </div>
    </div>
  `,
  styles: [`
    .shell { display: flex; min-height: 100vh; }
    .backdrop {
      display: none;
    }
    .sidebar {
      width: 240px; flex-shrink: 0; background: var(--navy-950); color: #fff;
      display: flex; flex-direction: column; padding: 1.1rem 0.9rem;
      position: sticky; top: 0; height: 100vh; overflow: hidden;
    }
    .brand { display: flex; align-items: center; gap: 0.6rem; padding: 0.4rem 0.4rem 1.2rem; flex-shrink: 0; }
    .brand app-brand-logo { background: #fff; border-radius: 8px; padding: 3px; display: flex; }
    .brand-name { font-weight: 700; font-size: 0.95rem; color: #fff; }
    .brand-sub { font-size: 0.7rem; color: var(--slate-400); }
    .close-btn { display: none; margin-left: auto; background: none; border: none; color: #fff; font-size: 1.1rem; padding: 0.3rem; }
    nav {
      display: flex; flex-direction: column; gap: 0.15rem; flex: 1;
      overflow-y: auto; min-height: 0;
      scrollbar-width: thin; scrollbar-color: var(--navy-700) transparent;
    }
    nav::-webkit-scrollbar { width: 6px; }
    nav::-webkit-scrollbar-thumb { background: var(--navy-700); border-radius: 3px; }
    .nav-link {
      display: flex; align-items: center; gap: 0.65rem; padding: 0.55rem 0.7rem; border-radius: 8px;
      color: var(--slate-300); font-size: 0.87rem; font-weight: 500;
    }
    .nav-link:hover { background: var(--navy-800); color: #fff; }
    .nav-link.active { background: var(--accent); color: #fff; }
    .nav-icon { font-size: 0.95rem; width: 1.2rem; text-align: center; }
    .sidebar-footer {
      display: flex; align-items: center; justify-content: space-between; gap: 0.5rem;
      padding-top: 0.9rem; border-top: 1px solid var(--navy-700); flex-shrink: 0;
    }
    .who-name { font-size: 0.82rem; font-weight: 600; color: #fff; }
    .who-role { font-size: 0.7rem; color: var(--slate-400); }
    .main { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .topbar {
      height: 56px; flex-shrink: 0; background: #fff; border-bottom: 1px solid var(--slate-200);
      display: flex; align-items: center; justify-content: space-between; padding: 0 1.5rem;
    }
    .hamburger {
      display: none; flex-direction: column; justify-content: center; gap: 4px;
      background: none; border: none; padding: 0.4rem; cursor: pointer;
    }
    .hamburger span { width: 20px; height: 2px; background: var(--navy-800); border-radius: 2px; }
    .et-clock { margin-left: auto; font-size: 0.78rem; font-weight: 600; color: var(--slate-500); font-variant-numeric: tabular-nums; }
    .bell { position: relative; font-size: 1.15rem; }
    .bell-count {
      position: absolute; top: -6px; right: -8px; background: var(--red); color: #fff;
      font-size: 0.65rem; font-weight: 700; border-radius: 999px; padding: 0.05rem 0.35rem;
    }
    .content { padding: 1.75rem; flex: 1; }
    .app-footer {
      padding: 0.9rem 1.75rem; font-size: 0.75rem; color: var(--slate-400);
      border-top: 1px solid var(--slate-200); text-align: center;
    }

    /* Mobile: sidebar becomes an off-canvas drawer, opened by the hamburger */
    @media (max-width: 860px) {
      .sidebar {
        position: fixed; left: 0; top: 0; z-index: 40;
        transform: translateX(-100%);
        transition: transform 0.2s ease-out;
        box-shadow: 2px 0 12px rgba(0,0,0,0.15);
      }
      .sidebar.open { transform: translateX(0); }
      .close-btn { display: block; }
      .hamburger { display: flex; }
      .backdrop {
        display: block; position: fixed; inset: 0; background: rgba(15,23,42,0.5); z-index: 30;
      }
      .content { padding: 1.1rem; }
      .app-footer { padding: 0.8rem 1.1rem; }
    }
  `],
})
export class StaffShellComponent implements OnDestroy {
  sidebarOpen = signal(false);
  readonly year = new Date().getFullYear();

  // Ethiopia (East Africa Time, UTC+3, no DST) shown next to the bell so
  // staff always have a shared reference clock regardless of their own
  // device's timezone. Intl.DateTimeFormat with an explicit timeZone
  // handles this correctly (unlike a hardcoded +3 offset, which would
  // silently drift if that zone's rules ever changed). en-US + hour12
  // gives a 12-hour clock with an AM/PM suffix.
  private static readonly ET_FORMATTER = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Africa/Addis_Ababa', hour: 'numeric', minute: '2-digit', hour12: true,
  });
  ethiopiaTime = signal(StaffShellComponent.ET_FORMATTER.format(new Date()));
  private clockIntervalId = setInterval(
    () => this.ethiopiaTime.set(StaffShellComponent.ET_FORMATTER.format(new Date())),
    30_000,
  );

  constructor(
    public auth: AuthService,
    private notifications: NotificationService,
    private router: Router
  ) {
    effect(() => {
      const key = this.recipientKey();
      if (key) void this.notifications.loadFor(key.type, key.id);
    });

    // Close the mobile drawer automatically on every navigation, so tapping
    // a link doesn't leave the overlay open behind the new page.
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.sidebarOpen.set(false));
  }

  ngOnDestroy() {
    clearInterval(this.clockIntervalId);
  }

  toggleSidebar() {
    this.sidebarOpen.update(v => !v);
  }

  closeSidebar() {
    this.sidebarOpen.set(false);
  }

  private recipientKey = computed((): { type: NotificationRecipientType; id: string } | null => {
    const emp = this.auth.currentEmployee();
    if (!emp) return null;
    if (emp.roles.includes('Admin')) return { type: 'Admin', id: 'ALL_ADMIN' };
    return { type: 'Employee', id: emp.id };
  });

  unread = computed(() => {
    const key = this.recipientKey();
    return key ? this.notifications.unreadCountFor(key.type, key.id) : 0;
  });

  visibleNavItems = computed(() => {
    const emp = this.auth.currentEmployee();
    if (!emp) return [];
    return NAV_ITEMS.filter(item => !item.rolesAllowed || item.rolesAllowed.some(r => emp.roles.includes(r)));
  });

  async logout() {
    await this.auth.logoutStaff();
    this.router.navigateByUrl('/login');
  }
}