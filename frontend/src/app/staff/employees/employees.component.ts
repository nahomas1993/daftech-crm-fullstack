import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EmployeeService } from '../../core/services/employee.service';
import { LocationService } from '../../core/services/location.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { EmployeeRegisteredResult, EmployeeRole, EMPLOYEE_ROLE_LABELS } from '../../core/models';

// ItSupport is retired — Admin absorbs that scope, so it's no longer offered
// when creating/editing an employee. Kept off this list even though the
// EmployeeRole type still technically allows it (see core/models.ts) so any
// existing employee record with that role can still deserialize/display correctly.
const ALL_ROLES: EmployeeRole[] = ['Admin', 'EmployeeTechnician'];

@Component({
  selector: 'app-employees',
  standalone: true,
  imports: [FormsModule, BadgeComponent, PaginationComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Employees</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Staff accounts, roles, and device/IP access.</p>
      </div>
      <button class="btn btn-primary" (click)="toggleForm()">{{ showForm() ? 'Cancel' : '+ Register Employee' }}</button>
    </div>

    @if (showForm()) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        @if (!justRegistered()) {
          <div class="form-grid">
            <div class="field"><label>Full Name</label><input type="text" [ngModel]="form.fullName" (ngModelChange)="form.fullName = $event" /></div>
            <div class="field"><label>Phone Number</label><input type="text" [ngModel]="form.phoneNumber" (ngModelChange)="form.phoneNumber = $event" /></div>
            <div class="field"><label>Email</label><input type="email" [ngModel]="form.email" (ngModelChange)="form.email = $event" placeholder="used to send login credentials" /></div>
            <div class="field">
              <label>Specialization</label>
              <select [ngModel]="form.specialization" (ngModelChange)="form.specialization = $event">
                <option value="">Select specialization…</option>
                @for (s of locations.options().specializations; track s.id) {
                  <option [value]="s.name">{{ s.name }}</option>
                }
              </select>
            </div>
            <div class="field"><label>Allowed IP Addresses (optional)</label><input type="text" [ngModel]="form.allowedIpAddressesRaw" (ngModelChange)="form.allowedIpAddressesRaw = $event" placeholder="comma-separated — blank = no restriction" /></div>
            <div class="field">
              <label>Roles</label>
              <div class="role-checks">
                @for (r of allRoles; track r) {
                  <label class="role-check">
                    <input type="checkbox" [checked]="form.roles.includes(r)" (change)="toggleRole(r)" />
                    {{ roleLabel(r) }}
                  </label>
                }
              </div>
            </div>
            @if (locations.options().customRoles.length > 0) {
              <div class="field">
                <label>Additional Roles</label>
                <p class="text-muted" style="font-size:0.72rem; margin: -0.1rem 0 0.3rem;">Descriptive only — these don't change what the employee can access.</p>
                <div class="role-checks">
                  @for (r of locations.options().customRoles; track r.id) {
                    <label class="role-check">
                      <input type="checkbox" [checked]="form.extraRoleLabels.includes(r.name)" (change)="toggleExtraRole(r.name)" />
                      {{ r.name }}
                    </label>
                  }
                </div>
              </div>
            }
          </div>
          @if (registerError()) {
            <p class="register-error" style="margin-top:0.75rem;">{{ registerError() }}</p>
          }
          <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="registering()" (click)="submit()">
            {{ registering() ? 'Registering…' : 'Register Employee' }}
          </button>
        } @else {
          <div class="credential-panel">
            <h4>✅ Account created — share these credentials now</h4>
            <p class="text-muted" style="font-size:0.82rem; margin: 0.3rem 0 0.9rem;">
              This one-time password will not be shown again.
              @if (justRegistered()!.emailSent) {
                An email with these details was also sent to {{ justRegistered()!.employee.email }}.
              } @else {
                The credential email could not be sent{{ justRegistered()!.emailError ? ' (' + justRegistered()!.emailError + ')' : '' }} — relay these to {{ justRegistered()!.employee.fullName }} directly, or retry below.
              }
            </p>
            <div class="cred-row"><span class="cred-label">Username</span><span class="mono cred-value">{{ justRegistered()!.username }}</span></div>
            <div class="cred-row"><span class="cred-label">One-time password</span><span class="mono cred-value">{{ justRegistered()!.oneTimePassword }}</span></div>
            <div style="display:flex; gap:0.5rem; margin-top:1rem;">
              @if (!justRegistered()!.emailSent) {
                <button class="btn btn-outline btn-sm" [disabled]="resending()" (click)="retryEmail(justRegistered()!.employee.id)">
                  {{ resending() ? 'Retrying…' : 'Retry Email' }}
                </button>
              }
              <button class="btn btn-secondary btn-sm" (click)="closeCredentialPanel()">Done</button>
            </div>
          </div>
        }
      </div>
    }

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="filters">
        <input type="text" placeholder="Search by name or email…" [ngModel]="query()" (ngModelChange)="query.set($event)" />
        <select [ngModel]="statusFilter()" (ngModelChange)="statusFilter.set($event)">
          <option value="">All statuses</option>
          <option value="Active">Active</option>
          <option value="Disabled">Disabled</option>
        </select>
      </div>

      <div class="table-scroll"><table>
        <thead>
          <tr><th>Account ID</th><th>Name</th><th>Email</th><th>Specialization</th><th>Roles</th><th>Open Tickets</th><th>Status</th><th></th></tr>
        </thead>
        <tbody>
          @for (e of displayedEmployees(); track e.id) {
            <tr>
              <td class="mono" title="Assigned once at account creation — reflects the employee's role at that time, not necessarily their current roles below.">{{ e.accountRefId }}</td>
              <td>{{ e.fullName }}</td>
              <td class="text-muted">{{ e.email }}</td>
              <td>{{ e.specialization }}</td>
              <td>
                @for (r of e.roles; track r) {
                  <span class="role-chip role-chip-real" [title]="'Real permission-granting role'">{{ roleLabel(r) }}</span>
                }
                @for (label of e.extraRoleLabels; track label) {
                  <span class="role-chip role-chip-label" [title]="'Custom label only — grants no permissions'">{{ label }}</span>
                }
              </td>
              <td class="mono">{{ e.openTicketCount }}</td>
              <td><app-badge [status]="e.accountStatus"></app-badge></td>
              <td class="actions-cell">
                @if (e.accountStatus === 'Active') {
                  <button class="btn btn-outline btn-sm" [disabled]="disabling() === e.id" (click)="disable(e.id)">
                    {{ disabling() === e.id ? 'Disabling…' : 'Disable' }}
                  </button>
                } @else {
                  <button class="btn btn-outline btn-sm" [disabled]="enabling() === e.id" (click)="enable(e.id)">
                    {{ enabling() === e.id ? 'Enabling…' : 'Enable' }}
                  </button>
                }
                <button class="btn btn-outline btn-sm" (click)="startEdit(e)">Edit</button>
                <button class="btn btn-outline btn-sm btn-danger" [disabled]="deleting() === e.id" (click)="deleteEmployee(e.id, e.fullName)">
                  {{ deleting() === e.id ? 'Deleting…' : 'Delete' }}
                </button>
              </td>
            </tr>
            @if (editingId() === e.id) {
              <tr class="edit-row">
                <td colspan="8">
                  <div class="edit-form">
                    <div class="field"><label>Full Name</label><input type="text" [ngModel]="editForm.fullName" (ngModelChange)="editForm.fullName = $event" /></div>
                    <div class="field"><label>Email</label><input type="email" [ngModel]="editForm.email" (ngModelChange)="editForm.email = $event" /></div>
                    <div class="field"><label>Phone Number</label><input type="text" [ngModel]="editForm.phoneNumber" (ngModelChange)="editForm.phoneNumber = $event" /></div>
                    <div class="field">
                      <label>Specialization</label>
                      <select [ngModel]="editForm.specialization" (ngModelChange)="editForm.specialization = $event">
                        <option value="">Select specialization…</option>
                        @for (s of locations.options().specializations; track s.id) {
                          <option [value]="s.name">{{ s.name }}</option>
                        }
                      </select>
                    </div>
                    <div class="edit-actions">
                      <button class="btn btn-primary btn-sm" [disabled]="savingEdit()" (click)="saveEdit(e.id)">{{ savingEdit() ? 'Saving…' : 'Save' }}</button>
                      <button class="btn btn-secondary btn-sm" (click)="cancelEdit()">Cancel</button>
                    </div>
                    @if (editError()) { <p class="register-error" style="margin-top:0.5rem;">{{ editError() }}</p> }
                  </div>
                </td>
              </tr>
            }
          }
          @empty {
            <tr><td colspan="8" class="text-muted" style="text-align:center; padding: 1.5rem;">No employees match your filters.</td></tr>
          }
        </tbody>
      </table></div>

      @if (!isFiltering()) {
        <app-pagination
          [page]="employeeService.page()"
          [totalPages]="employeeService.totalPages()"
          [totalCount]="employeeService.totalCount()"
          [pageSize]="employeeService.pageSize()"
          (pageChange)="employeeService.goToPage($event)">
        </app-pagination>
      } @else {
        <p class="text-muted" style="font-size:0.78rem; margin-top:0.75rem;">
          Showing all matches for your search/filter across every employee. Clear the filters to page through the full list.
        </p>
      }
    </div>
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .register-error { color: var(--red); font-size: 0.85rem; }
    .filters { display: flex; gap: 0.6rem; margin-bottom: 1rem; }
    .filters input { flex: 1; }
    .filters select { width: 180px; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    .role-checks { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 0.2rem; }
    .role-check { display: flex; align-items: center; gap: 0.35rem; font-size: 0.85rem; font-weight: 400; color: var(--navy-900); }
    .credential-panel { background: var(--green-bg); border-radius: 10px; padding: 1.1rem; }
    .credential-panel h4 { color: var(--green); font-size: 0.92rem; }
    .cred-row { display: flex; justify-content: space-between; align-items: center; padding: 0.5rem 0; border-top: 1px solid rgba(0,0,0,0.06); }
    .cred-row:first-of-type { border-top: none; }
    .cred-label { font-size: 0.8rem; color: var(--slate-500); }
    .cred-value { font-size: 0.95rem; font-weight: 700; color: var(--navy-900); }
    .actions-cell { display: flex; gap: 0.4rem; flex-wrap: wrap; }
    .edit-row td { background: var(--slate-50, #f8fafc); padding: 1rem; }
    .edit-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.9rem; align-items: end; }
    .edit-actions { display: flex; gap: 0.5rem; }
    /* Real roles (grant real permissions) vs extraRoleLabels (org-chart
       labels only, e.g. "Team Lead" — see Employee.ExtraRoleLabels) must
       never look the same, or an Admin scanning this table could mistake
       a decorative label for an actual permission. Real roles get a
       solid, permission-looking chip; labels get a clearly lighter,
       outlined one — reinforced by the title tooltip on each. */
    .role-chip { display: inline-block; font-size: 0.78rem; font-weight: 600; padding: 0.15rem 0.5rem; border-radius: 999px; margin: 0.1rem 0.25rem 0.1rem 0; white-space: nowrap; }
    .role-chip-real { background: var(--navy-900); color: white; }
    .role-chip-label { background: transparent; border: 1px dashed var(--slate-500); color: var(--slate-500); }
  `],
})
export class EmployeesComponent {
  allRoles = ALL_ROLES;

  query = signal('');
  statusFilter = signal('');
  showForm = signal(false);
  registering = signal(false);
  registerError = signal('');
  resending = signal(false);
  disabling = signal<string | null>(null);
  enabling = signal<string | null>(null);
  justRegistered = signal<EmployeeRegisteredResult | null>(null);

  editingId = signal<string | null>(null);
  savingEdit = signal(false);
  editError = signal('');
  deleting = signal<string | null>(null);
  editForm = { fullName: '', email: '', phoneNumber: '', specialization: '' };

  form = {
    fullName: '', phoneNumber: '', email: '', specialization: '',
    allowedIpAddressesRaw: '', roles: [] as EmployeeRole[], extraRoleLabels: [] as string[],
  };

  constructor(public employeeService: EmployeeService, public locations: LocationService) {}

  roleLabel = (r: EmployeeRole) => EMPLOYEE_ROLE_LABELS[r];

  filtered = computed(() => {
    const q = this.query().toLowerCase().trim();
    const status = this.statusFilter();
    return this.employeeService.employees().filter(e => {
      const matchesQuery = !q || e.fullName.toLowerCase().includes(q) || e.email.toLowerCase().includes(q);
      const matchesStatus = !status || e.accountStatus === status;
      return matchesQuery && matchesStatus;
    });
  });

  /** True when the user has an active search or status filter — in that case we show all matches instead of one server-paged slice. */
  isFiltering = computed(() => this.query().trim().length > 0 || this.statusFilter().length > 0);

  /** Filtered results when searching, otherwise the current server-fetched page. */
  displayedEmployees = computed(() => this.isFiltering() ? this.filtered() : this.employeeService.pagedEmployees());

  toggleForm() {
    this.justRegistered.set(null);
    this.showForm.set(!this.showForm());
  }

  toggleRole(r: EmployeeRole) {
    const idx = this.form.roles.indexOf(r);
    if (idx === -1) this.form.roles = [...this.form.roles, r];
    else this.form.roles = this.form.roles.filter(x => x !== r);
  }

  toggleExtraRole(name: string) {
    const idx = this.form.extraRoleLabels.indexOf(name);
    if (idx === -1) this.form.extraRoleLabels = [...this.form.extraRoleLabels, name];
    else this.form.extraRoleLabels = this.form.extraRoleLabels.filter(x => x !== name);
  }

  async submit() {
    if (!this.form.fullName || this.form.roles.length === 0) return;
    this.registering.set(true);
    this.registerError.set('');
    try {
      const allowedIpAddresses = this.form.allowedIpAddressesRaw
        .split(',').map(s => s.trim()).filter(Boolean);
      const result = await this.employeeService.registerEmployee({
        fullName: this.form.fullName,
        email: this.form.email,
        phoneNumber: this.form.phoneNumber,
        specialization: this.form.specialization,
        roles: this.form.roles,
        extraRoleLabels: this.form.extraRoleLabels,
        allowedIpAddresses,
      });
      this.justRegistered.set(result);
      this.form = { fullName: '', phoneNumber: '', email: '', specialization: '', allowedIpAddressesRaw: '', roles: [], extraRoleLabels: [] };
    } catch (err: any) {
      this.registerError.set(err?.error?.error ?? 'Registration failed — please check the details and try again.');
    } finally {
      this.registering.set(false);
    }
  }

  async retryEmail(employeeId: string) {
    this.resending.set(true);
    try {
      const result = await this.employeeService.resendCredentialEmail(employeeId);
      const current = this.justRegistered();
      if (current) {
        this.justRegistered.set({ ...current, emailSent: result.emailSent, emailError: result.emailError });
      }
    } finally {
      this.resending.set(false);
    }
  }

  closeCredentialPanel() {
    this.justRegistered.set(null);
    this.showForm.set(false);
  }

  async disable(id: string) {
    const reason = window.prompt('Reason for disabling this account?') ?? '';
    if (reason === '') return;
    this.disabling.set(id);
    try {
      await this.employeeService.disableEmployee(id, reason);
    } finally {
      this.disabling.set(null);
    }
  }

  async enable(id: string) {
    this.enabling.set(id);
    try {
      await this.employeeService.enableEmployee(id);
    } finally {
      this.enabling.set(null);
    }
  }

  startEdit(e: { id: string; fullName: string; email: string; phoneNumber: string; specialization: string }) {
    this.editingId.set(e.id);
    this.editError.set('');
    this.editForm = { fullName: e.fullName, email: e.email, phoneNumber: e.phoneNumber, specialization: e.specialization };
  }

  cancelEdit() {
    this.editingId.set(null);
    this.editError.set('');
  }

  async saveEdit(id: string) {
    this.savingEdit.set(true);
    this.editError.set('');
    try {
      await this.employeeService.updateEmployee(id, { ...this.editForm });
      this.editingId.set(null);
    } catch (err: any) {
      this.editError.set(err?.error?.error ?? err?.error ?? 'Could not save these changes — please try again.');
    } finally {
      this.savingEdit.set(false);
    }
  }

  async deleteEmployee(id: string, fullName: string) {
    if (!window.confirm(`Delete ${fullName}'s account? This removes them from the Employees list — their ticket and time-log history is kept.`)) return;
    this.deleting.set(id);
    try {
      await this.employeeService.deleteEmployee(id);
    } finally {
      this.deleting.set(null);
    }
  }
}
