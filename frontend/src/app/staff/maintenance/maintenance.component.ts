import { Component, computed, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { MaintenanceService } from '../../core/services/maintenance.service';
import { EmployeeService } from '../../core/services/employee.service';
import { AuthService } from '../../core/services/auth.service';
import { ClientService } from '../../core/services/client.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { TicketService } from '../../core/services/ticket.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { MaintenanceCategory, MaintenanceStatus, TICKET_CATEGORY_LABELS } from '../../core/models';

const CATEGORIES: MaintenanceCategory[] = ['SQL/Database error', 'Front-end error', 'Back-end/server error', 'Security patch', 'Performance update'];

@Component({
  selector: 'app-maintenance',
  standalone: true,
  imports: [FormsModule, BadgeComponent, PaginationComponent, SlicePipe, FilePreviewModalComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Maintenance History</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Pick a client to see their complete maintenance/ticket history — issue, technician assigned, dates, status, work performed, attachments and voice notes.</p>
      </div>
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="field" style="max-width:420px;">
        <label>Client</label>
        <select [ngModel]="selectedClientId()" (ngModelChange)="onSelectClient($event)">
          <option value="">Select a client to view their maintenance history…</option>
          @for (c of clients.approvedClients(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
        </select>
      </div>

      @if (selectedClientId()) {
        <div style="margin-top:1.25rem;">
          <h3 style="margin:0;">Maintenance History — {{ selectedClientName() }}</h3>
          <p class="text-muted" style="font-size:0.8rem; margin: 0.2rem 0 0.9rem;">
            Complete history for this client, newest first — which technician handled each issue, when it was assigned and completed, its status, work performed, and any attachments or voice notes submitted with it.
          </p>

          @if (clientTicketsLoading()) {
            <p class="text-muted" style="font-size:0.85rem;">Loading…</p>
          } @else {
            <div class="table-scroll"><table>
              <thead><tr><th>Ticket</th><th>Issue / Problem</th><th>Technician Assigned</th><th>Assigned</th><th>Completed</th><th>Status</th><th>Files</th><th></th></tr></thead>
              <tbody>
                @for (t of clientTicketHistory(); track t.id) {
                  <tr>
                    <td class="mono">{{ t.id.slice(0,8).toUpperCase() }}</td>
                    <td class="description-cell" [title]="t.description">
                      {{ t.description }}
                      <div class="text-muted" style="font-size:0.74rem; margin-top:0.15rem;">
                        {{ categoryLabel(t.category) }}@if (t.failureTypeName) { · {{ t.failureTypeName }} }@if (t.systemProductName) { · {{ t.systemProductName }} }
                      </div>
                    </td>
                    <td>{{ t.assignedEmployeeName ?? '—' }}</td>
                    <td class="text-muted" style="font-size:0.8rem;">
                      @if (t.assignedAt) { {{ t.assignedAt | slice:0:10 }} {{ t.assignedAt | slice:11:16 }} } @else { Not yet assigned }
                    </td>
                    <td class="text-muted" style="font-size:0.8rem;">
                      @if (t.completedAt) { {{ t.completedAt | slice:0:10 }} {{ t.completedAt | slice:11:16 }} } @else { — }
                    </td>
                    <td><app-badge [status]="t.status"></app-badge></td>
                    <td>
                      @if (t.attachmentFileName) {
                        <button class="btn btn-outline btn-sm" (click)="viewTicketAttachment(t.id, t.attachmentFileName)">📎 Attachment</button>
                      }
                      @if (t.voiceNoteFileName) {
                        <button class="btn btn-outline btn-sm" (click)="viewTicketVoiceNote(t.id, t.voiceNoteFileName)">🎤 Voice note</button>
                      }
                      @if (!t.attachmentFileName && !t.voiceNoteFileName) { <span class="text-muted">—</span> }
                    </td>
                    <td>
                      @if (hasExtraTicketDetails(t)) {
                        <button class="btn btn-outline btn-sm" (click)="toggleTicketDetails(t.id)">{{ expandedTicketId() === t.id ? 'Hide Details' : 'Details' }}</button>
                      }
                    </td>
                  </tr>
                  @if (expandedTicketId() === t.id) {
                    <tr class="details-row">
                      <td colspan="8">
                        <div class="details-grid">
                          <div class="detail-item"><span class="detail-label">Chargeable</span><span>{{ t.chargeable ? 'Chargeable' : 'Free' }}</span></div>
                          <div class="detail-item"><span class="detail-label">Priority</span><span>{{ t.priority }}</span></div>
                          <div class="detail-item"><span class="detail-label">Submitted</span><span>{{ t.dateSubmitted | slice:0:10 }} {{ t.dateSubmitted | slice:11:16 }}</span></div>
                          @if (t.workingMinutesToComplete != null) {
                            <div class="detail-item"><span class="detail-label">Working Minutes to Complete</span><span>{{ t.workingMinutesToComplete }}</span></div>
                          }
                          @if (t.completedByEmployeeName) {
                            <div class="detail-item"><span class="detail-label">Completed By</span><span>{{ t.completedByEmployeeName }}</span></div>
                          }
                          @if (t.requiredSpecialization) {
                            <div class="detail-item"><span class="detail-label">Required Specialization</span><span>{{ t.requiredSpecialization }}</span></div>
                          }
                          @if (t.itSupportContact) {
                            <div class="detail-item"><span class="detail-label">IT Support Contact</span><span>{{ t.itSupportContact }}</span></div>
                          }
                          @if (t.satisfactionScore != null) {
                            <div class="detail-item"><span class="detail-label">Satisfaction</span><span>{{ t.satisfactionScore }}/100</span></div>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                }
                @empty { <tr><td colspan="8" class="text-muted">No tickets submitted yet for this client.</td></tr> }
              </tbody>
            </table></div>
          }
        </div>
      } @else {
        <p class="text-muted" style="font-size:0.85rem; margin-top:1rem;">Select a client above to see their maintenance history.</p>
      }
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="header-row">
        <h3 style="margin:0;">Internal Maintenance Log</h3>
        <button class="btn btn-primary" (click)="showForm.set(!showForm())">{{ showForm() ? 'Cancel' : '+ New Record' }}</button>
      </div>
      <p class="text-muted" style="font-size:0.8rem; margin: 0.2rem 0 0.9rem;">Internal DAFTECH system issues manually logged by staff — separate from client tickets above.</p>

      @if (showForm()) {
        <div class="panel-pad" style="border:1px solid var(--slate-200); border-radius:10px; margin-bottom:1.25rem;">
          <div class="form-grid">
            <div class="field">
              <label>Client <span class="req">*</span></label>
              <select [ngModel]="form.clientId" (ngModelChange)="onFormClientChange($event)">
                <option value="">Select a client…</option>
                @for (c of clients.approvedClients(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
              </select>
            </div>
            <div class="field">
              <label>System/Product (optional)</label>
              <select [ngModel]="form.systemProductId" (ngModelChange)="form.systemProductId = $event" [disabled]="!form.clientId">
                <option value="">None / not specific to one system</option>
                @for (sp of systemProductsForClient(); track sp.id) { <option [value]="sp.id">{{ sp.name }}</option> }
              </select>
            </div>
            <div class="field">
              <label>Ticket (optional)</label>
              <select [ngModel]="form.ticketId" (ngModelChange)="form.ticketId = $event" [disabled]="!form.clientId">
                <option value="">None / not related to a ticket</option>
                @for (t of ticketsForForm(); track t.id) { <option [value]="t.id">{{ t.id.slice(0,8).toUpperCase() }} — {{ t.description.slice(0,40) }}</option> }
              </select>
            </div>
            <div class="field">
              <label>Category</label>
              <select [ngModel]="form.category" (ngModelChange)="form.category = $event">
                @for (c of categories(); track c) { <option [value]="c">{{ c }}</option> }
              </select>
            </div>
            <div class="field">
              <label>Custom Category (optional)</label>
              <input type="text" [ngModel]="customCategory" (ngModelChange)="customCategory = $event" placeholder="Add a new category…" />
            </div>
            <div class="field">
              <label>Performed By</label>
              <select [ngModel]="form.performedByEmployeeId" (ngModelChange)="form.performedByEmployeeId = $event">
                @for (e of employees.activeEmployees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
              </select>
            </div>
            <div class="field">
              <label>Status</label>
              <select [ngModel]="form.status" (ngModelChange)="form.status = $event">
                <option value="InProgress">In Progress</option>
                <option value="Resolved">Resolved</option>
                <option value="Recurring">Recurring</option>
              </select>
            </div>
          </div>
          <div class="field" style="margin-top:0.8rem;">
            <label>Description</label>
            <textarea rows="2" [ngModel]="form.description" (ngModelChange)="form.description = $event"></textarea>
          </div>
          <div class="field" style="margin-top:0.8rem;">
            <label>Remarks (optional)</label>
            <input type="text" [ngModel]="form.remarks" (ngModelChange)="form.remarks = $event" placeholder="Root cause, follow-up needed…" />
          </div>
          <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="!form.clientId" (click)="submit()">Save Record</button>
        </div>
      }

      <div class="filters">
        <select [ngModel]="categoryFilter()" (ngModelChange)="categoryFilter.set($event)">
          <option value="">All categories</option>
          @for (c of categories(); track c) { <option [value]="c">{{ c }}</option> }
        </select>
      </div>
      <table>
        <thead><tr><th>ID</th><th>Date</th><th>Client</th><th>Category</th><th>Description</th><th>Performed By</th><th>Status</th></tr></thead>
        <tbody>
          @for (r of displayedRecords(); track r.id) {
            <tr>
              <td class="mono">{{ r.id }}</td>
              <td class="text-muted">{{ r.date }}</td>
              <td>{{ r.clientName || '—' }}</td>
              <td>{{ r.category }}</td>
              <td>{{ r.description }}</td>
              <td>{{ r.performedByEmployeeName || employeeName(r.performedByEmployeeId) }}</td>
              <td><app-badge [status]="r.status"></app-badge></td>
            </tr>
          }
        </tbody>
      </table>
      @if (!categoryFilter()) {
        <app-pagination
          [page]="maintenance.page()"
          [totalPages]="maintenance.totalPages()"
          [totalCount]="maintenance.totalCount()"
          [pageSize]="maintenance.pageSize()"
          (pageChange)="maintenance.goToPage($event)">
        </app-pagination>
      } @else {
        <p class="text-muted" style="font-size:0.78rem; margin-top:0.75rem;">
          Showing all matches for the selected category. Clear the filter to page through the full list.
        </p>
      }
    </div>

    <app-file-preview-modal
      [open]="previewOpen()"
      [title]="previewTitle()"
      [fileName]="previewFileName()"
      [kind]="previewKind()"
      [load]="previewLoader"
      (closed)="closePreview()">
    </app-file-preview-modal>
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; margin-top: 0; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    .filters { margin-bottom: 1rem; }
    textarea { resize: vertical; }
    .description-cell { max-width: 280px; font-size: 0.82rem; }
    .details-row td { background: var(--slate-50, #f8fafc); padding: 0.75rem 1rem; }
    .details-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.6rem 1.2rem; }
    .detail-item { display: flex; flex-direction: column; gap: 0.15rem; font-size: 0.82rem; }
    .detail-label { font-size: 0.72rem; font-weight: 600; color: var(--slate-500); }
  `],
})
export class MaintenanceComponent {
  showForm = signal(false);
  categoryFilter = signal('');
  customCategory = '';
  extraCategories = signal<string[]>([]);

  // --- Client ticket history (top of page) ---
  selectedClientId = signal('');
  clientTicketsLoading = signal(false);

  form: {
    clientId: string; systemProductId: string; ticketId: string;
    category: MaintenanceCategory; performedByEmployeeId: string; status: MaintenanceStatus; description: string; remarks: string;
  } = {
    clientId: '', systemProductId: '', ticketId: '',
    category: 'SQL/Database error', performedByEmployeeId: '', status: 'InProgress', description: '', remarks: '',
  };

  constructor(
    public maintenance: MaintenanceService,
    public employees: EmployeeService,
    public clients: ClientService,
    public systemProducts: SystemProductService,
    public tickets: TicketService,
    private auth: AuthService,
  ) {
    effect(() => {
      const list = employees.activeEmployees();
      if (list.length > 0 && !this.form.performedByEmployeeId) {
        this.form.performedByEmployeeId = list[0].id;
      }
    });
  }

  categories = computed(() => [...CATEGORIES, ...this.extraCategories()]);

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }

  selectedClientName(): string {
    return this.clients.approvedClients().find(c => c.id === this.selectedClientId())?.name ?? '';
  }

  /** Fetches this client's ticket history on demand — TicketService.forClient() only ever returns what's already in cache, so a fresh fetch is needed each time a different client is picked. */
  async onSelectClient(clientId: string) {
    this.selectedClientId.set(clientId);
    if (!clientId) return;
    this.clientTicketsLoading.set(true);
    try {
      await this.tickets.refreshMyTickets(clientId);
    } catch (err) {
      console.error('Failed to load maintenance history for client', err);
    } finally {
      this.clientTicketsLoading.set(false);
    }
  }

  clientTicketHistory = computed(() => {
    const clientId = this.selectedClientId();
    return clientId ? this.tickets.forClient(clientId) : [];
  });

  expandedTicketId = signal<string | null>(null);

  toggleTicketDetails(ticketId: string) {
    this.expandedTicketId.set(this.expandedTicketId() === ticketId ? null : ticketId);
  }

  hasExtraTicketDetails(t: {
    completedAt?: string; workingMinutesToComplete?: number; completedByEmployeeName?: string;
    requiredSpecialization?: string; itSupportContact?: string;
  }): boolean {
    return !!(t.completedAt || t.workingMinutesToComplete != null || t.completedByEmployeeName || t.requiredSpecialization || t.itSupportContact);
  }

  // Shown inline in the shared preview modal (audio player / image / PDF
  // viewer) rather than forcing a download.
  previewOpen = signal(false);
  previewTitle = signal('');
  previewFileName = signal('');
  previewKind = signal<FilePreviewKind>('other');
  previewLoader: (() => Promise<Blob>) | undefined;

  viewTicketAttachment(ticketId: string, fileName: string) {
    this.previewOpen.set(false);
    this.previewTitle.set('Ticket Attachment');
    this.previewFileName.set(fileName);
    this.previewKind.set(filePreviewKindFor(fileName));
    this.previewLoader = () => this.tickets.downloadAttachment(ticketId);
    setTimeout(() => this.previewOpen.set(true));
  }

  viewTicketVoiceNote(ticketId: string, fileName: string) {
    this.previewOpen.set(false);
    this.previewTitle.set('Ticket Voice Note');
    this.previewFileName.set(fileName);
    this.previewKind.set('audio');
    this.previewLoader = () => this.tickets.downloadVoiceNote(ticketId);
    setTimeout(() => this.previewOpen.set(true));
  }

  closePreview() {
    this.previewOpen.set(false);
  }

  // --- Internal Maintenance Log (bottom of page, unchanged behaviour) ---

  /** Loaded on demand once a client is selected in the "+ New Record" form, since system/products aren't fetched client-wide by default — see SystemProductService.refreshForClient. */
  systemProductsForClient = computed(() => this.form.clientId ? this.systemProducts.systemProductsFor(this.form.clientId) : []);
  ticketsForForm = computed(() => this.form.clientId ? this.tickets.forClient(this.form.clientId) : []);

  async onFormClientChange(clientId: string) {
    this.form.clientId = clientId;
    this.form.systemProductId = '';
    this.form.ticketId = '';
    if (clientId) {
      await Promise.all([
        this.systemProducts.refreshForClient(clientId),
        this.tickets.refreshMyTickets(clientId),
      ]);
    }
  }

  filtered = computed(() => {
    const filter = this.categoryFilter();
    return this.maintenance.records().filter(r => !filter || r.category === filter);
  });

  /** Filtered results when a category is selected, otherwise the current server-fetched page. */
  displayedRecords = computed(() => this.categoryFilter() ? this.filtered() : this.maintenance.pagedRecords());

  employeeName(id: string): string {
    return this.employees.getById(id)?.fullName ?? id;
  }

  async submit() {
    if (!this.form.clientId || !this.form.description || !this.form.performedByEmployeeId) return;
    const category = this.customCategory.trim() || this.form.category;
    if (this.customCategory.trim()) {
      this.extraCategories.update(list => (list.includes(category) ? list : [...list, category]));
    }
    await this.maintenance.create({
      category,
      description: this.form.description,
      performedByEmployeeId: this.form.performedByEmployeeId,
      status: this.form.status,
      remarks: this.form.remarks || undefined,
      clientId: this.form.clientId,
      systemProductId: this.form.systemProductId || undefined,
      ticketId: this.form.ticketId || undefined,
    });
    this.showForm.set(false);
    this.form = {
      clientId: '', systemProductId: '', ticketId: '',
      category: 'SQL/Database error', performedByEmployeeId: this.employees.activeEmployees()[0]?.id ?? '', status: 'InProgress', description: '', remarks: '',
    };
    this.customCategory = '';
  }
}

