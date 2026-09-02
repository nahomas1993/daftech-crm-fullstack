import { Component, computed, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';
import { LocationService } from '../../core/services/location.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { ProductCatalogService } from '../../core/services/product-catalog.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { ClientRegisteredResult } from '../../core/models';
import { requiredFieldsError } from '../../core/required-fields';
import { isValidRegistrationEmail } from '../../core/email-validation';
import { isValidEthiopianPhone, invalidEthiopianPhoneMessage } from '../../core/ethiopian-phone-validation';

@Component({
  selector: 'app-clients-list',
  standalone: true,
  imports: [RouterLink, FormsModule, BadgeComponent, PaginationComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Clients</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Customer profiles and their agreement / ticket history.</p>
      </div>
      <div style="display:flex; gap:0.5rem;">
        <a routerLink="/admin/clients/import" class="btn btn-outline">Import Clients (CSV)</a>
        <a routerLink="/admin/clients/import-attachments" class="btn btn-outline">Upload Attachments</a>
        <button class="btn btn-primary" (click)="toggleForm()">{{ showForm() ? 'Cancel' : '+ Register Client' }}</button>
      </div>
    </div>

    @if (showForm()) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        @if (!justRegistered()) {
          <div class="form-grid">
            <div class="field"><label>Name / Organization <span class="req">*</span></label><input type="text" [ngModel]="form.name" (ngModelChange)="form.name = $event" /></div>
            <div class="field">
              <label>Phone Number <span class="req">*</span></label>
              <input type="text" [ngModel]="form.phoneNumber" (ngModelChange)="form.phoneNumber = $event" placeholder="+2519XXXXXXXX or +2517XXXXXXXX" />
              @if (!isPhoneValid(form.phoneNumber)) { <div class="field-error">{{ phoneErrorMessage('Phone Number') }}</div> }
            </div>
            <div class="field"><label>Email <span class="req">*</span></label><input type="email" [ngModel]="form.email" (ngModelChange)="form.email = $event" placeholder="used to send login credentials" />
              @if (!isEmailValid(form.email)) { <div class="field-error">Invalid email</div> }
            </div>
            <div class="field"><label>Office <span class="req">*</span></label><input type="text" [ngModel]="form.office" (ngModelChange)="form.office = $event" /></div>
            <div class="field"><label>Location <span class="req">*</span></label><input type="text" [ngModel]="form.location" (ngModelChange)="form.location = $event" /></div>
            <div class="field"><label>Region <span class="req">*</span></label>
              <select [ngModel]="form.region" (ngModelChange)="onRegionChange($event, form)">
                <option value="">Select region…</option>
                @for (r of locations.options().regions; track r.id) {
                  <option [value]="r.name">{{ r.name }}</option>
                }
              </select>
            </div>
            <div class="field"><label>Zone <span class="req">*</span></label>
              <select [ngModel]="form.zone" (ngModelChange)="onZoneChange($event, form)" [disabled]="!form.region">
                <option value="">Select zone…</option>
                @for (z of zonesForForm(form); track z.id) {
                  <option [value]="z.name">{{ z.name }}</option>
                }
              </select>
            </div>
            <div class="field"><label>City <span class="req">*</span></label>
              <select [ngModel]="form.city" (ngModelChange)="form.city = $event">
                <option value="">Select city…</option>
                @for (c of locations.options().cities; track c.id) {
                  <option [value]="c.name">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="field"><label>Woreda <span class="req">*</span></label>
              <select [ngModel]="form.woreda" (ngModelChange)="form.woreda = $event" [disabled]="!form.zone">
                <option value="">Select woreda…</option>
                @for (w of woredasForForm(form); track w.id) {
                  <option [value]="w.name">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="field"><label>KYC Type <span class="req">*</span></label><input type="text" [ngModel]="form.kycType" (ngModelChange)="form.kycType = $event" placeholder="Business License…" /></div>
            <div class="field"><label>KYC Contact <span class="req">*</span></label><input type="text" [ngModel]="form.kycContact" (ngModelChange)="form.kycContact = $event" placeholder="Name — phone/email" /></div>
            <div class="field">
              <label>IT Support Contact <span class="req">*</span></label>
              <input type="text" [ngModel]="form.itSupportContact" (ngModelChange)="form.itSupportContact = $event" placeholder="+2519XXXXXXXX or +2517XXXXXXXX" />
              @if (!isItSupportContactValid(form.itSupportContact)) { <div class="field-error">{{ phoneErrorMessage('IT Support Contact') }}</div> }
            </div>
          </div>
          @if (registerError()) {
            <p class="register-error" style="margin-top:0.75rem;">{{ registerError() }}</p>
          }
          <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="registering()" (click)="submit()">
            {{ registering() ? 'Registering…' : 'Register Client' }}
          </button>
        } @else if (!systemProductCreated()) {
          <div class="credential-panel">
            <h4>✅ Account created — share these credentials now</h4>
            <p class="text-muted" style="font-size:0.82rem; margin: 0.3rem 0 0.9rem;">
              This one-time password will not be shown again.
              @if (justRegistered()!.emailSent) {
                An email with these details was also sent to {{ justRegistered()!.client.email }}.
              } @else {
                The credential email could not be sent{{ justRegistered()!.emailError ? ' (' + justRegistered()!.emailError + ')' : '' }} — relay these to {{ justRegistered()!.client.name }} directly, or retry below.
              }
            </p>
            <div class="cred-row"><span class="cred-label">Username</span><span class="mono cred-value">{{ justRegistered()!.username }}</span></div>
            <div class="cred-row"><span class="cred-label">One-time password</span><span class="mono cred-value">{{ justRegistered()!.oneTimePassword }}</span></div>
            @if (!justRegistered()!.emailSent) {
              <div style="margin-top:1rem;">
                <button class="btn btn-outline btn-sm" [disabled]="resending()" (click)="retryEmail(justRegistered()!.client.id)">
                  {{ resending() ? 'Retrying…' : 'Retry Email' }}
                </button>
              </div>
            }
          </div>

          <div class="panel panel-pad" style="margin-top:1rem; border:1px solid var(--slate-200);">
            <h4 style="margin:0 0 0.3rem;">Now add {{ justRegistered()!.client.name }}'s System/Product</h4>
            <p class="text-muted" style="font-size:0.82rem; margin:0 0 0.9rem;">
              Completed by DAFTECH — you'll assign trainers to it next.
            </p>
            <div class="form-grid">
              <div class="field">
                <label>System/Product <span class="req">*</span></label>
                @if (catalog.items().length > 0) {
                  <select [ngModel]="systemProductForm.catalogItemId" (ngModelChange)="onCatalogSelect($event)">
                    <option value="">Select a system/product…</option>
                    @for (c of catalog.items(); track c.id) {
                      <option [value]="c.id">{{ c.name }}</option>
                    }
                    <option value="__other__">Other (type manually)…</option>
                  </select>
                  @if (systemProductForm.catalogItemId === '__other__') {
                    <input type="text" style="margin-top:0.4rem;" [ngModel]="systemProductForm.name" (ngModelChange)="systemProductForm.name = $event" placeholder="e.g. Branch POS System" />
                  }
                } @else {
                  <input type="text" [ngModel]="systemProductForm.name" (ngModelChange)="systemProductForm.name = $event" placeholder="e.g. Branch POS System" />
                  <span class="text-muted" style="font-size:0.75rem;">No systems/products configured yet — an Admin can add some under Settings, or type a name here.</span>
                }
              </div>
              <div class="field"><label>Deployment Date (optional)</label><input type="date" [ngModel]="systemProductForm.deploymentDate" (ngModelChange)="systemProductForm.deploymentDate = $event" /></div>
              <div class="field"><label>Expiry Date (optional)</label><input type="date" [ngModel]="systemProductForm.expiryDate" (ngModelChange)="systemProductForm.expiryDate = $event" /></div>
              <div class="field" style="grid-column: 1 / -1;"><label>Description (optional)</label><textarea rows="2" [ngModel]="systemProductForm.description" (ngModelChange)="systemProductForm.description = $event"></textarea></div>
            </div>
            @if (systemProductError()) { <p class="register-error" style="margin-top:0.75rem;">{{ systemProductError() }}</p> }
            <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="savingSystemProduct()" (click)="submitSystemProduct()">
              {{ savingSystemProduct() ? 'Saving…' : 'Save & Continue to Training Assignment →' }}
            </button>
          </div>
        } @else {
          <div class="credential-panel">
            <h4>✅ System/Product added</h4>
            <p class="text-muted" style="font-size:0.82rem; margin: 0.3rem 0 0.9rem;">
              Taking you to Training Assignment for {{ systemProductCreated()!.name }}…
            </p>
          </div>
        }
      </div>
    }

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="filters">
        <input type="text" placeholder="Search by name or ID number…" [ngModel]="query()" (ngModelChange)="query.set($event)" />
        <select [ngModel]="statusFilter()" (ngModelChange)="statusFilter.set($event)">
          <option value="">All statuses</option>
          <option value="Approved">Approved</option>
          <option value="Pending">Pending</option>
          <option value="Rejected">Rejected</option>
        </select>
      </div>

      <div class="table-scroll"><table>
        <thead>
          <tr><th>Account ID</th><th>Name</th><th>ID Number</th><th>Office</th><th>Location</th><th>Status</th><th>Onboarded</th><th></th></tr>
        </thead>
        <tbody>
          @for (c of displayedClients(); track c.id) {
            <tr>
              <td class="mono">{{ c.accountRefId }}</td>
              <td>{{ c.name }}</td>
              <td class="mono text-muted">{{ c.idNumber }}</td>
              <td>{{ c.office }}</td>
              <td>{{ c.location }}</td>
              <td><app-badge [status]="c.accountStatus"></app-badge></td>
              <td class="text-muted">{{ c.onboardingDate }}</td>
              <td class="actions-cell">
                <a [routerLink]="['/admin/clients', c.id]" class="btn btn-outline btn-sm">View</a>
                <button class="btn btn-outline btn-sm" (click)="startEdit(c)">Edit</button>
                <button class="btn btn-outline btn-sm btn-danger" [disabled]="deleting() === c.id" (click)="deleteClient(c.id, c.name)">
                  {{ deleting() === c.id ? 'Deleting…' : 'Delete' }}
                </button>
              </td>
            </tr>
            @if (editingId() === c.id) {
              <tr class="edit-row">
                <td colspan="8">
                  <div class="edit-form">
                    <div class="field"><label>Name / Organization <span class="req">*</span></label><input type="text" [ngModel]="editForm.name" (ngModelChange)="editForm.name = $event" /></div>
                    <div class="field">
                      <label>Phone Number <span class="req">*</span></label>
                      <input type="text" [ngModel]="editForm.phoneNumber" (ngModelChange)="editForm.phoneNumber = $event" placeholder="+2519XXXXXXXX or +2517XXXXXXXX" />
                      @if (!isPhoneValid(editForm.phoneNumber)) { <div class="field-error">{{ phoneErrorMessage('Phone Number') }}</div> }
                    </div>
                    <div class="field"><label>Email <span class="req">*</span></label><input type="email" [ngModel]="editForm.email" (ngModelChange)="editForm.email = $event" />
                      @if (!isEmailValid(editForm.email)) { <div class="field-error">Invalid email</div> }
                    </div>
                    <div class="field"><label>Office <span class="req">*</span></label><input type="text" [ngModel]="editForm.office" (ngModelChange)="editForm.office = $event" /></div>
                    <div class="field"><label>Location <span class="req">*</span></label><input type="text" [ngModel]="editForm.location" (ngModelChange)="editForm.location = $event" /></div>
                    <div class="field"><label>Region <span class="req">*</span></label>
                      <select [ngModel]="editForm.region" (ngModelChange)="onRegionChange($event, editForm)">
                        <option value="">Select region…</option>
                        @for (r of locations.options().regions; track r.id) {
                          <option [value]="r.name">{{ r.name }}</option>
                        }
                      </select>
                    </div>
                    <div class="field"><label>Zone <span class="req">*</span></label>
                      <select [ngModel]="editForm.zone" (ngModelChange)="onZoneChange($event, editForm)" [disabled]="!editForm.region">
                        <option value="">Select zone…</option>
                        @for (z of zonesForForm(editForm); track z.id) {
                          <option [value]="z.name">{{ z.name }}</option>
                        }
                      </select>
                    </div>
                    <div class="field"><label>City <span class="req">*</span></label>
                      <select [ngModel]="editForm.city" (ngModelChange)="editForm.city = $event">
                        <option value="">Select city…</option>
                        @for (ct of locations.options().cities; track ct.id) {
                          <option [value]="ct.name">{{ ct.name }}</option>
                        }
                      </select>
                    </div>
                    <div class="field"><label>Woreda <span class="req">*</span></label>
                      <select [ngModel]="editForm.woreda" (ngModelChange)="editForm.woreda = $event" [disabled]="!editForm.zone">
                        <option value="">Select woreda…</option>
                        @for (w of woredasForForm(editForm); track w.id) {
                          <option [value]="w.name">{{ w.name }}</option>
                        }
                      </select>
                    </div>
                    <div class="field"><label>KYC Type <span class="req">*</span></label><input type="text" [ngModel]="editForm.kycType" (ngModelChange)="editForm.kycType = $event" /></div>
                    <div class="field"><label>KYC Contact <span class="req">*</span></label><input type="text" [ngModel]="editForm.kycContact" (ngModelChange)="editForm.kycContact = $event" /></div>
                    <div class="field">
                      <label>IT Support Contact <span class="req">*</span></label>
                      <input type="text" [ngModel]="editForm.itSupportContact" (ngModelChange)="editForm.itSupportContact = $event" placeholder="+2519XXXXXXXX or +2517XXXXXXXX" />
                      @if (!isItSupportContactValid(editForm.itSupportContact)) { <div class="field-error">{{ phoneErrorMessage('IT Support Contact') }}</div> }
                    </div>
                    <div class="edit-actions">
                      <button class="btn btn-primary btn-sm" [disabled]="savingEdit()" (click)="saveEdit(c.id)">{{ savingEdit() ? 'Saving…' : 'Save' }}</button>
                      <button class="btn btn-secondary btn-sm" (click)="cancelEdit()">Cancel</button>
                    </div>
                    @if (editError()) { <p class="register-error" style="margin-top:0.5rem;">{{ editError() }}</p> }
                  </div>
                </td>
              </tr>
            }
          }
          @empty {
            <tr><td colspan="8" class="text-muted" style="text-align:center; padding: 1.5rem;">No clients match your filters.</td></tr>
          }
        </tbody>
      </table></div>

      @if (!isFiltering()) {
        <app-pagination
          [page]="clients.page()"
          [totalPages]="clients.totalPages()"
          [totalCount]="clients.totalCount()"
          [pageSize]="clients.pageSize()"
          (pageChange)="clients.goToPage($event)">
        </app-pagination>
      } @else {
        <p class="text-muted" style="font-size:0.78rem; margin-top:0.75rem;">
          Showing all matches for your search/filter across every client. Clear the filters to page through the full list.
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
    .field label .req { color: var(--red, #b3261e); }
    .field-error { background: var(--red-bg, #fde8e8); color: var(--red, #b3261e); font-size: 0.72rem; font-weight: 600; padding: 0.2rem 0.5rem; border-radius: 5px; margin-top: 0.3rem; display: inline-block; }
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
  `],
})
export class ClientsListComponent {
  query = signal('');
  statusFilter = signal('');
  showForm = signal(false);
  registering = signal(false);
  registerError = signal('');
  resending = signal(false);
  justRegistered = signal<ClientRegisteredResult | null>(null);

  // Step 2 of registration (per the Client Registration & Training flow):
  // once the client account exists, immediately prompt for their first
  // System/Product before auto-redirecting to Training Assignment — see
  // submitSystemProduct(). systemProductCreated is only set briefly, to
  // show a short confirmation before the redirect actually happens.
  systemProductForm = { name: '', description: '', deploymentDate: '', catalogItemId: '', expiryDate: '' };
  savingSystemProduct = signal(false);
  systemProductError = signal('');
  systemProductCreated = signal<{ id: string; name: string } | null>(null);

  editingId = signal<string | null>(null);
  savingEdit = signal(false);
  editError = signal('');
  deleting = signal<string | null>(null);
  editForm = { name: '', phoneNumber: '', email: '', office: '', location: '', region: '', zone: '', city: '', woreda: '', kycType: '', kycContact: '', itSupportContact: '' };

  form = { name: '', phoneNumber: '', email: '', office: '', location: '', region: '', zone: '', city: '', woreda: '', kycType: '', kycContact: '', itSupportContact: '' };

  constructor(
    public clients: ClientService,
    public locations: LocationService,
    private systemProductsSvc: SystemProductService,
    public catalog: ProductCatalogService,
    private router: Router,
  ) {}

  filtered = computed(() => {
    const q = this.query().toLowerCase().trim();
    const status = this.statusFilter();
    return this.clients.clients().filter(c => {
      const matchesQuery = !q || c.name.toLowerCase().includes(q) || c.idNumber.toLowerCase().includes(q);
      const matchesStatus = !status || c.accountStatus === status;
      return matchesQuery && matchesStatus;
    });
  });

  /** True when the user has an active search or status filter — in that case we show all matches instead of one server-paged slice. */
  isFiltering = computed(() => this.query().trim().length > 0 || this.statusFilter().length > 0);

  /** Filtered results when searching, otherwise the current server-fetched page. */
  displayedClients = computed(() => this.isFiltering() ? this.filtered() : this.clients.pagedClients());

  isEmailValid(email: string | null | undefined): boolean {
    return isValidRegistrationEmail(email);
  }

  isPhoneValid(value: string | null | undefined): boolean {
    return isValidEthiopianPhone(value);
  }

  isItSupportContactValid(value: string | null | undefined): boolean {
    return isValidEthiopianPhone(value);
  }

  phoneErrorMessage(label: string): string {
    return invalidEthiopianPhoneMessage(label);
  }

  /**
   * Location cascade helpers — form.region/zone/woreda are stored by NAME
   * (not id), matching the client DTO fields, so we resolve the region's/
   * zone's id from its name before delegating to LocationService's
   * id-based zonesFor()/woredasFor() helpers.
   */
  private regionIdByName(name: string | null | undefined): string | undefined {
    if (!name) return undefined;
    return this.locations.options().regions.find(r => r.name === name)?.id;
  }

  private zoneIdByName(name: string | null | undefined): string | undefined {
    if (!name) return undefined;
    return this.locations.options().zones.find(z => z.name === name)?.id;
  }

  zonesForForm(form: { region: string }) {
    return this.locations.zonesFor(this.regionIdByName(form.region));
  }

  woredasForForm(form: { zone: string }) {
    return this.locations.woredasFor(this.zoneIdByName(form.zone));
  }

  /** Region changed (or cleared) — always drop the now-stale Zone and Woreda. */
  onRegionChange(value: string, form: { region: string; zone: string; woreda: string }) {
    form.region = value;
    form.zone = '';
    form.woreda = '';
  }

  /** Zone changed (or cleared) — always drop the now-stale Woreda. */
  onZoneChange(value: string, form: { zone: string; woreda: string }) {
    form.zone = value;
    form.woreda = '';
  }

  toggleForm() {
    this.justRegistered.set(null);
    this.systemProductCreated.set(null);
    this.systemProductForm = { name: '', description: '', deploymentDate: '', catalogItemId: '', expiryDate: '' };
    this.systemProductError.set('');
    this.showForm.set(!this.showForm());
  }

  /** Selecting a catalog entry sets its name as the system/product's Name (source of truth for display); "Other" clears it so the free-text input takes over. */
  onCatalogSelect(value: string) {
    this.systemProductForm.catalogItemId = value;
    if (value && value !== '__other__') {
      const match = this.catalog.items().find(c => c.id === value);
      this.systemProductForm.name = match?.name ?? '';
    } else {
      this.systemProductForm.name = '';
    }
  }

  async submit() {
    const validationError = requiredFieldsError([
      { label: 'Name / Organization', value: this.form.name },
      { label: 'Phone Number', value: this.form.phoneNumber },
      { label: 'Email', value: this.form.email },
      { label: 'Office', value: this.form.office },
      { label: 'Location', value: this.form.location },
      { label: 'Region', value: this.form.region },
      { label: 'Zone', value: this.form.zone },
      { label: 'City', value: this.form.city },
      { label: 'Woreda', value: this.form.woreda },
      { label: 'KYC Type', value: this.form.kycType },
      { label: 'KYC Contact', value: this.form.kycContact },
      { label: 'IT Support Contact', value: this.form.itSupportContact },
    ]);
    if (validationError) {
      this.registerError.set(validationError);
      return;
    }
    if (!this.isEmailValid(this.form.email)) {
      this.registerError.set('Invalid email — please use a @gmail.com address.');
      return;
    }
    if (!this.isPhoneValid(this.form.phoneNumber)) {
      this.registerError.set(this.phoneErrorMessage('Phone Number'));
      return;
    }
    if (!this.isItSupportContactValid(this.form.itSupportContact)) {
      this.registerError.set(this.phoneErrorMessage('IT Support Contact'));
      return;
    }

    this.registering.set(true);
    this.registerError.set('');
    try {
      const result = await this.clients.registerClient({
        ...this.form,
        itSupportContact: this.form.itSupportContact || undefined,
      });
      this.justRegistered.set(result);
      this.form = { name: '', phoneNumber: '', email: '', office: '', location: '', region: '', zone: '', city: '', woreda: '', kycType: '', kycContact: '', itSupportContact: '' };
    } catch (err: any) {
      this.registerError.set(err?.error?.error ?? err?.error ?? 'Registration failed — please check the details and try again.');
    } finally {
      this.registering.set(false);
    }
  }

  async retryEmail(clientId: string) {
    this.resending.set(true);
    try {
      const result = await this.clients.resendCredentialEmail(clientId);
      const current = this.justRegistered();
      if (current) {
        this.justRegistered.set({ ...current, emailSent: result.emailSent, emailError: result.emailError });
      }
    } finally {
      this.resending.set(false);
    }
  }

  /**
   * Step 2 of the Client Registration & Training flow: right after the
   * client account is created, the Admin enters the client's first
   * System/Product (completed by DAFTECH) here — never overwrites or
   * replaces anything, this is always a fresh SystemProduct. On success,
   * automatically redirects to Client Detail with that system/product's
   * Training panel focused, so Manual/Automatic Assignment is the very
   * next thing the Admin sees — see ClientDetailComponent's
   * ?focusSystemProduct handling.
   */
  async submitSystemProduct() {
    const clientId = this.justRegistered()?.client.id;
    if (!clientId || !this.systemProductForm.name.trim()) {
      this.systemProductError.set('System/Product is required — select one from the list, or choose "Other" and type a name.');
      return;
    }

    this.savingSystemProduct.set(true);
    this.systemProductError.set('');
    try {
      const catalogItemId = this.systemProductForm.catalogItemId && this.systemProductForm.catalogItemId !== '__other__'
        ? this.systemProductForm.catalogItemId
        : undefined;
      const created = await this.systemProductsSvc.create({
        clientId,
        name: this.systemProductForm.name,
        description: this.systemProductForm.description || undefined,
        deploymentDate: this.systemProductForm.deploymentDate || undefined,
        catalogItemId,
        expiryDate: this.systemProductForm.expiryDate || undefined,
      });
      this.systemProductCreated.set({ id: created.id, name: created.name });

      // Brief confirmation, then the redirect — matches the credential
      // panel's own "just registered" pause, so the Admin sees each step
      // land before being moved on to the next.
      setTimeout(() => {
        this.justRegistered.set(null);
        this.systemProductCreated.set(null);
        this.showForm.set(false);
        void this.router.navigate(['/admin/clients', clientId], {
          queryParams: { tab: 'agreements', focusSystemProduct: created.id },
        });
      }, 900);
    } catch (err: any) {
      this.systemProductError.set(err?.error?.error ?? 'Could not add this system/product — please try again.');
    } finally {
      this.savingSystemProduct.set(false);
    }
  }

  startEdit(c: {
    id: string; name: string; phoneNumber: string; email: string; office: string; location: string;
    region?: string; zone?: string; city?: string; woreda?: string; kycType: string; kycContact: string; itSupportContact?: string;
  }) {
    this.editingId.set(c.id);
    this.editError.set('');
    this.editForm = {
      name: c.name, phoneNumber: c.phoneNumber, email: c.email, office: c.office, location: c.location,
      region: c.region ?? '', zone: c.zone ?? '', city: c.city ?? '', woreda: c.woreda ?? '',
      kycType: c.kycType, kycContact: c.kycContact, itSupportContact: c.itSupportContact ?? '',
    };
  }

  cancelEdit() {
    this.editingId.set(null);
    this.editError.set('');
  }

  async saveEdit(id: string) {
    const validationError = requiredFieldsError([
      { label: 'Name / Organization', value: this.editForm.name },
      { label: 'Phone Number', value: this.editForm.phoneNumber },
      { label: 'Email', value: this.editForm.email },
      { label: 'Office', value: this.editForm.office },
      { label: 'Location', value: this.editForm.location },
      { label: 'Region', value: this.editForm.region },
      { label: 'Zone', value: this.editForm.zone },
      { label: 'City', value: this.editForm.city },
      { label: 'Woreda', value: this.editForm.woreda },
      { label: 'KYC Type', value: this.editForm.kycType },
      { label: 'KYC Contact', value: this.editForm.kycContact },
      { label: 'IT Support Contact', value: this.editForm.itSupportContact },
    ]);
    if (validationError) {
      this.editError.set(validationError);
      return;
    }
    if (!this.isEmailValid(this.editForm.email)) {
      this.editError.set('Invalid email — please use a @gmail.com address.');
      return;
    }
    if (!this.isPhoneValid(this.editForm.phoneNumber)) {
      this.editError.set(this.phoneErrorMessage('Phone Number'));
      return;
    }
    if (!this.isItSupportContactValid(this.editForm.itSupportContact)) {
      this.editError.set(this.phoneErrorMessage('IT Support Contact'));
      return;
    }

    this.savingEdit.set(true);
    this.editError.set('');
    try {
      await this.clients.updateClient(id, {
        ...this.editForm,
        itSupportContact: this.editForm.itSupportContact || undefined,
      });
      this.editingId.set(null);
    } catch (err: any) {
      this.editError.set(err?.error?.error ?? err?.error ?? 'Could not save these changes — please try again.');
    } finally {
      this.savingEdit.set(false);
    }
  }

  async deleteClient(id: string, name: string) {
    if (!window.confirm(`Delete ${name}'s account? This removes them from the Clients list — their agreement and ticket history is kept.`)) return;
    this.deleting.set(id);
    try {
      await this.clients.deleteClient(id);
    } finally {
      this.deleting.set(null);
    }
  }
}
