import { Component, computed, effect } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { AgreementService } from '../../core/services/agreement.service';
import { BadgeComponent } from '../../shared/badge.component';

/**
 * Client-facing view of all of the logged-in client's agreements — a
 * client can have multiple over time, each with its own training
 * session(s). Read-only: uploads/edits stay staff-side. Populated via
 * AgreementService.refreshMyAgreements()/forClient(), the client-scoped
 * endpoint (see AgreementService class comments for why this is kept
 * separate from the staff-only full agreements list).
 */
@Component({
  selector: 'app-portal-agreements',
  standalone: true,
  imports: [BadgeComponent],
  template: `
    <h1>Agreements</h1>
    <p class="text-muted" style="margin-top:0.3rem;">All agreements on file for your organization.</p>

    @for (a of agreements(); track a.id) {
      <div class="panel panel-pad agreement-card">
        <div class="header-row">
          <div>
            <h3 style="margin:0;">{{ a.documentNumber }}</h3>
            <p class="text-muted" style="margin:0.2rem 0 0; font-size:0.82rem;">{{ a.agreementPlace }}</p>
          </div>
          <app-badge [status]="a.status"></app-badge>
        </div>

        <dl>
          <dt>Sign Date</dt>
          <dd>{{ a.signDate }}</dd>
          <dt>Expiry</dt><dd>{{ a.expiryDate }}</dd>
          <dt>Support Window</dt><dd>{{ a.supportWindowMonths }} months</dd>
          <dt>Billing Tier</dt><dd>{{ a.billingTier }}</dd>
        </dl>

        @if (a.trainings.length > 0) {
          <h4 style="margin: 1rem 0 0.5rem;">Training</h4>
          <div class="table-scroll"><table>
            <thead><tr><th>Start</th><th>End</th><th>Description</th></tr></thead>
            <tbody>
              @for (t of a.trainings; track t.id) {
                <tr>
                  <td>{{ t.startDate || '—' }}</td>
                  <td>{{ t.endDate || '—' }}</td>
                  <td class="text-muted">{{ t.description || '—' }}</td>
                </tr>
              }
            </tbody>
          </table></div>
        }
      </div>
    }
    @empty {
      <div class="panel panel-pad">
        <p class="text-muted">No agreements on file yet.</p>
      </div>
    }
  `,
  styles: [`
    .agreement-card { margin-top: 1.25rem; }
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    dl { display: grid; grid-template-columns: auto 1fr; gap: 0.35rem 1rem; margin-top: 0.9rem; font-size: 0.85rem; }
    dt { color: var(--slate-500); font-weight: 600; }
    dd { margin: 0; }
  `],
})
export class PortalAgreementsComponent {
  constructor(private auth: AuthService, private agreementsSvc: AgreementService) {
    effect(() => {
      const client = this.auth.currentClient();
      if (client) {
        void this.agreementsSvc.refreshMyAgreements(client.id);
      }
    });
  }

  agreements = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.agreementsSvc.forClient(client.id) : [];
  });
}
