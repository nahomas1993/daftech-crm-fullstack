import { Component, computed, effect } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationRecipientType } from '../../core/models';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="header-row">
      <div>
        <h1>Notifications</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Ticket events, password reset requests, and agreement expiry alerts.</p>
      </div>
      <button class="btn btn-outline" (click)="markAllRead()">Mark all as read</button>
    </div>

    <div class="panel" style="margin-top:1.25rem;">
      @for (n of items(); track n.id) {
        <div class="row" [class.unread]="!n.readStatus" (click)="markRead(n.id)">
          <div class="dot" [class.on]="!n.readStatus"></div>
          <div class="body">
            <div class="msg">{{ n.message }}</div>
            <div class="meta">{{ n.dateSent | date:'medium' }} · {{ n.eventType }}</div>
          </div>
        </div>
      }
      @empty {
        <div class="empty">No notifications yet.</div>
      }
    </div>
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .row { display: flex; gap: 0.75rem; align-items: flex-start; padding: 0.9rem 1.1rem; border-bottom: 1px solid var(--slate-100); cursor: pointer; }
    .row:last-child { border-bottom: none; }
    .row:hover { background: var(--slate-50); }
    .row.unread { background: #f5f8ff; }
    .dot { width: 8px; height: 8px; border-radius: 50%; margin-top: 0.35rem; background: transparent; }
    .dot.on { background: var(--accent); }
    .msg { font-size: 0.88rem; }
    .meta { font-size: 0.75rem; color: var(--slate-500); margin-top: 0.15rem; }
    .empty { padding: 2rem; text-align: center; color: var(--slate-500); }
  `],
})
export class NotificationsComponent {
  constructor(public notifications: NotificationService, private auth: AuthService) {
    effect(() => {
      const key = this.roleKey();
      if (key) void this.notifications.loadFor(key.type, key.id);
    });
  }

  private roleKey = computed((): { type: NotificationRecipientType; id: string } | null => {
    const emp = this.auth.currentEmployee();
    if (!emp) return null;
    if (emp.roles.includes('Admin')) return { type: 'Admin', id: 'ALL_ADMIN' };
    return { type: 'Employee', id: emp.id };
  });

  items = computed(() => {
    const key = this.roleKey();
    return key ? this.notifications.forRecipient(key.type, key.id) : [];
  });

  async markRead(notificationId: string) {
    const key = this.roleKey();
    if (key) await this.notifications.markRead(key.type, key.id, notificationId);
  }

  async markAllRead() {
    const key = this.roleKey();
    if (key) await this.notifications.markAllReadFor(key.type, key.id);
  }
}
