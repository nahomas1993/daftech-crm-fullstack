import { Component, Input } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { BadgeComponent } from './badge.component';
import { MaintenanceRecord } from '../core/models';

/**
 * Read-only, newest-first table of maintenance records — shared by the
 * Client Detail page (GET /api/maintenance/client/{clientId}) and the
 * System/Product panel (GET /api/maintenance/system-product/{id}). The
 * owning component is responsible for fetching and sorting; this just
 * renders whatever list it's given.
 */
@Component({
  selector: 'app-maintenance-history-list',
  standalone: true,
  imports: [SlicePipe, BadgeComponent],
  template: `
    @if (loading) {
      <p class="text-muted" style="font-size:0.85rem;">Loading maintenance history…</p>
    } @else if (records.length === 0) {
      <p class="text-muted" style="font-size:0.85rem;">{{ emptyMessage }}</p>
    } @else {
      <div class="table-scroll">
        <table>
          <thead>
            <tr><th>Date</th><th>Category</th><th>Description</th><th>Performed By</th><th>Status</th><th>Remarks</th></tr>
          </thead>
          <tbody>
            @for (r of records; track r.id) {
              <tr>
                <td class="text-muted">{{ r.date | slice:0:10 }}</td>
                <td>{{ r.category }}</td>
                <td>{{ r.description }}</td>
                <td>{{ r.performedByEmployeeName }}</td>
                <td><app-badge [status]="r.status"></app-badge></td>
                <td class="text-muted">{{ r.remarks || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class MaintenanceHistoryListComponent {
  @Input() records: MaintenanceRecord[] = [];
  @Input() loading = false;
  @Input() emptyMessage = 'No maintenance records yet.';
}
