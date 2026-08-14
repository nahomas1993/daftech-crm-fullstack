import { Component, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { SessionService } from '../../core/services/session.service';
import { SessionActivity } from '../../core/models';

@Component({
  selector: 'app-session-activity',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="header-row">
      <div>
        <h1>Session Activity</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Who's online right now, and when everyone was last seen.</p>
      </div>
      <button class="btn btn-outline btn-sm" [disabled]="loading()" (click)="refresh()">
        {{ loading() ? 'Refreshing…' : 'Refresh' }}
      </button>
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="table-scroll"><table>
        <thead><tr><th>Account</th><th>Type</th><th>Status</th><th>Last Seen</th><th>Most Recent IP</th></tr></thead>
        <tbody>
          @for (s of activity(); track s.accountType + s.accountId) {
            <tr>
              <td>{{ s.accountName }}</td>
              <td class="text-muted">{{ s.accountType }}</td>
              <td>
                <span class="badge" [class]="s.onlineStatus ? 'badge-green' : 'badge-slate'">
                  <span class="dot" [class.on]="s.onlineStatus"></span>
                  {{ s.onlineStatus ? 'Online' : 'Offline' }}
                </span>
              </td>
              <td class="text-muted">{{ s.lastSeen | date:'medium' }}</td>
              <td class="mono text-muted">{{ s.mostRecentIpAddress ?? '—' }}</td>
            </tr>
          }
          @empty {
            <tr><td colspan="5" class="text-muted" style="text-align:center; padding:1.5rem;">No session activity recorded yet.</td></tr>
          }
        </tbody>
      </table></div>
    </div>
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .dot { display: inline-block; width: 6px; height: 6px; border-radius: 50%; background: var(--slate-400); margin-right: 0.35rem; }
    .dot.on { background: var(--green); }
  `],
})
export class SessionActivityComponent {
  activity = signal<SessionActivity[]>([]);
  loading = signal(true);

  constructor(private sessions: SessionService) {
    void this.refresh();
  }

  async refresh() {
    this.loading.set(true);
    try {
      this.activity.set(await this.sessions.getActivity());
    } finally {
      this.loading.set(false);
    }
  }
}
