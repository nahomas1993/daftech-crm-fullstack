import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AgreementService } from '../../core/services/agreement.service';
import { Agreement } from '../../core/models';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';

/**
 * Cross-client agreement listing. Creating a new agreement requires
 * picking a client's System/Product first (Client -> SystemProduct ->
 * Agreement -> AgreementType), so that flow lives on the Client Detail
 * page (see ClientDetailComponent). This page lets the admin filter by
 * expiry (all / active / expired, and expired for at least N years) and
 * add, replace or remove each agreement's scanned document.
 */
@Component({
  selector: 'app-agreements',
  standalone: true,
  imports: [RouterLink, BadgeComponent, PaginationComponent, FilePreviewModalComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Agreements</h1>
        <p class="text-muted" style="margin-top:0.3rem;">All signed agreements across every client. Open a client to add a System/Product or sign a new agreement.</p>
      </div>
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="filter-bar">
        <label>Show
          <select [value]="agreements.expiryFilter()" (change)="onExpiryChange($any($event.target).value)">
            <option value="all">All agreements</option>
            <option value="active">Active (not expired)</option>
            <option value="expired">Expired</option>
          </select>
        </label>
        @if (agreements.expiryFilter() === 'expired') {
          <label>Expired for
            <select [value]="agreements.minYearsExpired()" (change)="onYearsChange($any($event.target).value)">
              <option value="0">Any length of time</option>
              <option value="1">1 year or more</option>
              <option value="2">2 years or more</option>
              <option value="3">3 years or more</option>
              <option value="4">4 years or more</option>
              <option value="5">5 years or more</option>
            </select>
          </label>
        }
        <span class="text-muted" style="font-size:0.8rem;">{{ agreements.totalCount() }} result(s)</span>
      </div>

      @if (fileError()) { <p class="file-error">{{ fileError() }}</p> }

      <div class="table-scroll"><table>
        <thead><tr><th>Client</th><th>System/Product</th><th>Type</th><th>Doc #</th><th>Sign Date</th><th>Expiry</th><th>Tier</th><th>Status</th><th>Document</th><th></th></tr></thead>
        <tbody>
          @for (a of agreements.pagedAgreements(); track a.id) {
            <tr>
              <td>{{ a.clientName }}</td>
              <td>{{ a.systemProductName }}</td>
              <td>{{ a.agreementTypeName }}</td>
              <td class="mono">{{ a.documentNumber }}</td>
              <td>{{ a.signDate }}</td>
              <td>
                {{ a.expiryDate }}
                @if (a.isExpired) {
                  <div class="expired-note">Expired {{ expiredLabel(a) }}</div>
                }
              </td>
              <td>{{ a.billingTier }}</td>
              <td><app-badge [status]="a.status"></app-badge></td>
              <td class="doc-cell">
                @if (a.scannedFileUrl) {
                  <button class="btn btn-outline btn-sm" (click)="view(a.id, a.scannedFileUrl)">View</button>
                  <label class="btn btn-outline btn-sm" [class.disabled]="busyId() === a.id">
                    Replace
                    <input type="file" hidden accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" [disabled]="busyId() === a.id" (change)="onFilePicked($event, a)" />
                  </label>
                  <button class="btn btn-outline btn-sm btn-danger" [disabled]="busyId() === a.id" (click)="remove(a)">Delete</button>
                } @else {
                  <label class="btn btn-outline btn-sm" [class.disabled]="busyId() === a.id">
                    {{ busyId() === a.id ? 'Uploading…' : 'Upload' }}
                    <input type="file" hidden accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" [disabled]="busyId() === a.id" (change)="onFilePicked($event, a)" />
                  </label>
                }
              </td>
              <td>
                <a [routerLink]="['/admin/clients', a.clientId]" [queryParams]="{ tab: 'agreements' }" class="btn btn-outline btn-sm">Open Client</a>
              </td>
            </tr>
          }
          @empty { <tr><td colspan="10" class="text-muted">No agreements match this filter.</td></tr> }
        </tbody>
      </table></div>
      <app-pagination
        [page]="agreements.page()"
        [totalPages]="agreements.totalPages()"
        [totalCount]="agreements.totalCount()"
        [pageSize]="agreements.pageSize()"
        (pageChange)="agreements.goToPage($event)">
      </app-pagination>
    </div>

    <app-file-preview-modal
      [open]="previewOpen"
      title="Scanned Agreement"
      [fileName]="previewFileName"
      [kind]="previewKind"
      [load]="previewLoader"
      (closed)="closePreview()">
    </app-file-preview-modal>
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .filter-bar { display: flex; gap: 1rem; align-items: center; flex-wrap: wrap; margin-bottom: 0.9rem; }
    .filter-bar label { display: flex; align-items: center; gap: 0.45rem; font-size: 0.85rem; }
    .filter-bar select { padding: 0.3rem 0.5rem; border-radius: 6px; border: 1px solid var(--slate-200); }
    .expired-note { color: var(--red, #b3261e); font-size: 0.74rem; font-weight: 600; }
    .doc-cell { white-space: nowrap; }
    .doc-cell .btn { margin-right: 0.25rem; cursor: pointer; }
    .doc-cell .btn.disabled { opacity: 0.5; pointer-events: none; }
    .file-error { color: var(--red, #b3261e); font-size: 0.8rem; margin: 0 0 0.7rem; }
  `],
})
export class AgreementsComponent {
  constructor(public agreements: AgreementService) {}

  previewOpen = false;
  previewFileName = '';
  previewKind: FilePreviewKind = 'other';
  previewLoader?: () => Promise<Blob>;

  /** Agreement whose document is being uploaded/removed right now. */
  busyId = signal<string | null>(null);
  fileError = signal<string | null>(null);

  onExpiryChange(value: 'all' | 'active' | 'expired') {
    void this.agreements.setExpiryFilter(value, 0);
  }

  onYearsChange(value: string) {
    void this.agreements.setExpiryFilter('expired', Number(value) || 0);
  }

  /** "less than a year ago", "1 year ago", "3 years ago". */
  expiredLabel(a: Agreement): string {
    if (a.yearsExpired <= 0) return 'less than a year ago';
    return a.yearsExpired === 1 ? '1 year ago' : `${a.yearsExpired} years ago`;
  }

  // Shown inline in the same modal used for ticket attachments (audio
  // player / image / PDF viewer), rather than forcing the scanned
  // document to save to the browser's Downloads folder.
  view(agreementId: string, scannedFileUrl: string) {
    this.previewFileName = scannedFileUrl;
    this.previewKind = filePreviewKindFor(scannedFileUrl);
    this.previewLoader = () => this.agreements.downloadScannedFile(agreementId);
    this.previewOpen = true;
  }

  closePreview() {
    this.previewOpen = false;
  }

  /** Upload (no file yet) or replace (file already attached). The server swaps the file and removes the old one. */
  async onFilePicked(evt: Event, a: Agreement) {
    const input = evt.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.fileError.set(null);
    this.busyId.set(a.id);
    try {
      await this.agreements.uploadScannedFile(a.id, file);
    } catch (err: any) {
      this.fileError.set(typeof err?.error === 'string' && err.error ? err.error : 'Could not upload this file. Please try again.');
    } finally {
      this.busyId.set(null);
    }
  }

  async remove(a: Agreement) {
    if (!confirm(`Delete the scanned document for ${a.clientName} (${a.documentNumber})? The agreement itself is kept.`)) return;

    this.fileError.set(null);
    this.busyId.set(a.id);
    try {
      await this.agreements.deleteScannedFile(a.id);
    } catch (err: any) {
      this.fileError.set(typeof err?.error === 'string' && err.error ? err.error : 'Could not delete this file. Please try again.');
    } finally {
      this.busyId.set(null);
    }
  }
}
