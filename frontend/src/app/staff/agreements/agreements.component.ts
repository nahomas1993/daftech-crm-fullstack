import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AgreementService } from '../../core/services/agreement.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';

/**
 * Cross-client read-only agreement listing. Creating a new agreement
 * requires picking a client's System/Product first (Client ->
 * SystemProduct -> Agreement -> AgreementType), so that flow lives on the
 * Client Detail page (see ClientDetailComponent) rather than here — this
 * page links each row into that client's detail page instead of
 * duplicating the creation form.
 */
@Component({
  selector: 'app-agreements',
  standalone: true,
  imports: [RouterLink, BadgeComponent, PaginationComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Agreements</h1>
        <p class="text-muted" style="margin-top:0.3rem;">All signed agreements across every client. Open a client to add a System/Product or sign a new agreement.</p>
      </div>
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
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
              <td>{{ a.expiryDate }}</td>
              <td>{{ a.billingTier }}</td>
              <td><app-badge [status]="a.status"></app-badge></td>
              <td>
                @if (a.scannedFileUrl) {
                  <button class="btn btn-outline btn-sm" (click)="download(a.id)">Download</button>
                } @else {
                  <span class="text-muted">None</span>
                }
              </td>
              <td>
                <a [routerLink]="['/admin/clients', a.clientId]" [queryParams]="{ tab: 'agreements' }" class="btn btn-outline btn-sm">Open Client</a>
              </td>
            </tr>
          }
          @empty { <tr><td colspan="10" class="text-muted">No agreements yet.</td></tr> }
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
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
  `],
})
export class AgreementsComponent {
  constructor(public agreements: AgreementService) {}

  async download(agreementId: string) {
    try {
      const blob = await this.agreements.downloadScannedFile(agreementId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = '';
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Failed to download scanned document', err);
    }
  }
}
