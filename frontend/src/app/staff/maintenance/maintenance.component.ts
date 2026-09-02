import { Component, computed, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MaintenanceService } from '../../core/services/maintenance.service';
import { EmployeeService } from '../../core/services/employee.service';
import { AuthService } from '../../core/services/auth.service';
import { ClientService } from '../../core/services/client.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { TicketService } from '../../core/services/ticket.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { MaintenanceCategory, MaintenanceStatus } from '../../core/models';

const CATEGORIES: MaintenanceCategory[] = ['SQL/Database error', 'Front-end error', 'Back-end/server error', 'Security patch', 'Performance update'];

@Component({
  selector: 'app-maintenance',
  standalone: true,
  imports: [FormsModule, BadgeComponent, PaginationComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Maintenance History</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Internal DAFTECH system issues — separate from client tickets.</p>
      </div>
      <button class="btn btn-primary" (click)="showForm.set(!showForm())">{{ showForm() ? 'Cancel' : '+ New Record' }}</button>
    </div>

    @if (showForm()) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <div class="form-grid">
          <div class="field">
            <label>Client <span class="req">*</span></label>
            <select [ngModel]="form.clientId" (ngModelChange)="onClientChange($event)">
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
              @for (t of ticketsForClient(); track t.id) { <option [value]="t.id">{{ t.id.slice(0,8).toUpperCase() }} — {{ t.description.slice(0,40) }}</option> }
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

    <div class="panel panel-pad" style="margin-top:1.25rem;">
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
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; margin-top: 0; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    .filters { margin-bottom: 1rem; }
    textarea { resize: vertical; }
  `],
})
export class MaintenanceComponent {
  showForm = signal(false);
  categoryFilter = signal('');
  customCategory = '';
  extraCategories = signal<string[]>([]);

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

  /** Loaded on demand once a client is selected, since system/products aren't fetched client-wide by default — see SystemProductService.refreshForClient. */
  systemProductsForClient = computed(() => this.form.clientId ? this.systemProducts.systemProductsFor(this.form.clientId) : []);
  ticketsForClient = computed(() => this.form.clientId ? this.tickets.forClient(this.form.clientId) : []);

  async onClientChange(clientId: string) {
    this.form.clientId = clientId;
    this.form.systemProductId = '';
    this.form.ticketId = '';
    if (clientId) {
      await this.systemProducts.refreshForClient(clientId);
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
