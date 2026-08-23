import { Component, computed, effect, signal } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { AgreementService } from '../../core/services/agreement.service';
import { BadgeComponent } from '../../shared/badge.component';

/**
 * Client-facing view of all of the logged-in client's agreements — a
 * client can have multiple systems/products, and each can have multiple
 * agreements over time (e.g. several Support agreements as each expires
 * and is renewed). Read-only: uploads/edits stay staff-side, but the
 * client CAN view/download the scanned copy of their own signed
 * agreement — the backend already scopes GET /agreements/{id}/scanned-file
 * to the owning client (see AgreementsController.DownloadScannedFile),
 * this page just needed a button wired to it. Populated via
 * AgreementService.refreshMyAgreements()/forClient(), the client-scoped
 * endpoint (see AgreementService class comments for why this is kept
 * separate from the staff-only full agreements list). Training itself is
 * no longer part of Agreement (see SystemProduct.trainingAssignments/
 * trainingRecords) so isn't shown here — the client's own training status
 * isn't currently surfaced in the portal.
 */
@Component({
  selector: 'app-portal-agreements',
  standalone: true,
  imports: [BadgeComponent],
  template: `
    <h1>Agreements</h1>
    <p class="text-muted" style="margin-top:0.3rem;">All agreements on file for your organization, by system/product.</p>

    @for (a of agreements(); track a.id) {
      <div class="panel panel-pad agreement-card">
        <div class="header-row">
          <div>
            <h3 style="margin:0;">{{ a.systemProductName }} — {{ a.agreementTypeName }}</h3>
            <p class="text-muted mono" style="margin:0.2rem 0 0; font-size:0.8rem;">{{ a.documentNumber }} · {{ a.agreementPlace }}</p>
          </div>
          <app-badge [status]="a.status"></app-badge>
        </div>

        <dl>
          <dt>Signed Date</dt>
          <dd>{{ a.signDate }}</dd>
          <dt>Expiry</dt><dd>{{ a.expiryDate }}</dd>
          @if (a.agreementTypeName === 'Support') {
            <dt>Support Window</dt><dd>{{ a.supportWindowMonths }} months</dd>
            <dt>Billing Tier</dt><dd>{{ a.billingTier }}</dd>
          }
          @if (a.details) { <dt>Details</dt><dd>{{ a.details }}</dd> }
        </dl>

        <div class="doc-row">
          @if (a.scannedFileUrl) {
            <button class="btn btn-outline btn-sm" [disabled]="downloadingId() === a.id" (click)="downloadScan(a.id)">
              {{ downloadingId() === a.id ? 'Opening…' : '📄 View Scanned Agreement' }}
            </button>
          } @else {
            <span class="text-muted" style="font-size:0.8rem;">No scanned copy has been uploaded for this agreement yet.</span>
          }
        </div>
        @if (downloadError() && downloadErrorFor() === a.id) {
          <p class="doc-error">{{ downloadError() }}</p>
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
    .doc-row { margin-top: 0.9rem; }
    .doc-error { color: var(--red); font-size: 0.8rem; margin-top: 0.5rem; }
  `],
})
export class PortalAgreementsComponent {
  downloadingId = signal<string | null>(null);
  downloadError = signal<string | null>(null);
  downloadErrorFor = signal<string | null>(null);

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

  private openBlob(blob: Blob) {
    const url = URL.createObjectURL(blob);
    // Open in a new tab rather than forcing a download — a client
    // reviewing their own signed agreement usually wants to read it, not
    // save a copy; they can still save from the browser's viewer if they
    // want to.
    window.open(url, '_blank', 'noopener');
    // Give the new tab a moment to actually load the blob URL before
    // revoking it — revoking immediately can race the tab's navigation.
    setTimeout(() => URL.revokeObjectURL(url), 30_000);
  }

  async downloadScan(agreementId: string) {
    this.downloadingId.set(agreementId);
    this.downloadError.set(null);
    try {
      const blob = await this.agreementsSvc.downloadScannedFile(agreementId);
      this.openBlob(blob);
    } catch (err) {
      this.downloadError.set('Could not open this document — please try again.');
      this.downloadErrorFor.set(agreementId);
      console.error('Failed to download scanned agreement', err);
    } finally {
      this.downloadingId.set(null);
    }
  }
}
