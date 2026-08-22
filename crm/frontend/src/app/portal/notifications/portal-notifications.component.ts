import { Component, computed, effect } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-portal-notifications',
  standalone: true,
  imports: [DatePipe],
  template: `
    <h1>Notifications</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Updates on your signup and ticket status.</p>

    <div class="panel" style="margin-top:1.25rem;">
      @for (n of items(); track n.id) {
        <div class="row" [class.unread]="!n.readStatus" (click)="markRead(n.id)">
          <div class="dot" [class.on]="!n.readStatus"></div>
          <div>
            <div class="msg">{{ n.message }}</div>
            <div class="meta">{{ n.dateSent | date:'medium' }}</div>
          </div>
        </div>
      }
      @empty { <div class="empty">Nothing here yet.</div> }
    </div>
  `,
  styles: [`
    .row { display: flex; gap: 0.75rem; align-items: flex-start; padding: 0.9rem 1.1rem; border-bottom: 1px solid var(--slate-100); cursor: pointer; }
    .row:last-child { border-bottom: none; }
    .row.unread { background: var(--portal-accent-soft); }
    .dot { width: 8px; height: 8px; border-radius: 50%; margin-top: 0.35rem; background: transparent; }
    .dot.on { background: var(--portal-accent); }
    .msg { font-size: 0.88rem; }
    .meta { font-size: 0.75rem; color: var(--slate-500); margin-top: 0.15rem; }
    .empty { padding: 2rem; text-align: center; color: var(--slate-500); }
  `],
})
export class PortalNotificationsComponent {
  constructor(public notifications: NotificationService, private auth: AuthService) {
    effect(() => {
      const client = this.auth.currentClient();
      if (client) void this.notifications.loadFor('Client', client.id);
    });
  }

  items = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.notifications.forRecipient('Client', client.id) : [];
  });

  async markRead(notificationId: string) {
    const client = this.auth.currentClient();
    if (client) await this.notifications.markRead('Client', client.id, notificationId);
  }
}
