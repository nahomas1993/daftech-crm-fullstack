import { Component, computed, effect, input, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { AgreementService } from '../../core/services/agreement.service';
import { AgreementTypeService } from '../../core/services/agreement-type.service';
import { TicketService } from '../../core/services/ticket.service';
import { BadgeComponent } from '../../shared/badge.component';
import { TICKET_CATEGORY_LABELS, BillingTier, Agreement } from '../../core/models';

/**
 * Client -> System/Product -> Agreement -> Agreement Type. Each
 * System/Product is its own expandable panel; agreements for it are
 * fetched and shown per-panel. Signing a Support agreement for a
 * System/Product is blocked until that SAME System/Product has a Training
 * agreement with an End Date set (see AgreementService.CreateAsync on the
 * backend — this UI mirrors that gate but the server is the source of
 * truth). Creating a new System/Product or Agreement never overwrites an
 * existing one — everything here is additive.
 *
 * Supports a `?tab=agreements` query param so the "Continue to Agreement"
 * redirect after Admin registers a new client (see
 * ClientsListComponent.continueToAgreement) can land the Admin straight on
 * this section — see the effect() in the constructor.
 */
@Component({
  selector: 'app-client-detail',
  standalone: true,
  imports: [RouterLink, BadgeComponent, SlicePipe, FormsModule],
  template: `
    @if (client(); as c) {
      <a routerLink="/admin/clients" class="back">← Back to Clients</a>
      <div class="header-row">
        <div>
          <h1>{{ c.name }}</h1>
          <p class="text-muted" style="margin-top:0.3rem;">ID {{ c.idNumber }} · {{ c.office }}, {{ c.location }}</p>
          <p class="text-muted mono" style="margin-top:0.2rem; font-size:0.78rem;">Account: {{ c.accountRefId }}</p>
        </div>
        <app-badge [status]="c.accountStatus"></app-badge>
      </div>

      <div class="panel panel-pad">
        <h3>Profile</h3>
        <dl>
          <dt>Phone</dt><dd>{{ c.phoneNumber }}</dd>
          <dt>KYC Type</dt><dd>{{ c.kycType }}</dd>
          <dt>KYC Contact</dt><dd>{{ c.kycContact }}</dd>
          @if (c.itSupportContact) { <dt>IT Support Contact</dt><dd>{{ c.itSupportContact }}</dd> }
          <dt>Onboarded</dt><dd>{{ c.onboardingDate }}</dd>
          @if (c.rejectionReason) { <dt>Rejection Reason</dt><dd class="text-muted">{{ c.rejectionReason }}</dd> }
        </dl>
      </div>

      <div class="panel panel-pad" id="agreements-section" style="margin-top:1.25rem;">
        <div class="header-row">
          <h3 style="margin:0;">Systems / Products & Agreements</h3>
          <button class="btn btn-primary btn-sm" (click)="toggleNewSystemForm()">
            {{ showNewSystemForm() ? 'Cancel' : '+ Add System/Product' }}
          </button>
        </div>
        <p class="text-muted" style="font-size:0.78rem; margin: 0.3rem 0 0;">
          A client may have multiple systems/products, and each may carry multiple agreements — nothing shown here is ever overwritten by adding another.
        </p>

        @if (showNewSystemForm()) {
          <div class="add-form">
            <div class="form-grid">
              <div class="field">
                <label>Name</label>
                <input type="text" [ngModel]="newSystemForm.name" (ngModelChange)="newSystemForm.name = $event" placeholder="e.g. Branch POS System" />
              </div>
              <div class="field">
                <label>Deployment Date (optional)</label>
                <input type="date" [ngModel]="newSystemForm.deploymentDate" (ngModelChange)="newSystemForm.deploymentDate = $event" />
              </div>
              <div class="field" style="grid-column: 1 / -1;">
                <label>Description (optional)</label>
                <textarea rows="2" [ngModel]="newSystemForm.description" (ngModelChange)="newSystemForm.description = $event"></textarea>
              </div>
            </div>
            @if (systemError()) { <p class="upload-error" style="margin-top:0.75rem;">{{ systemError() }}</p> }
            <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="savingSystem()" (click)="submitNewSystem(c.id)">
              {{ savingSystem() ? 'Saving…' : 'Add System/Product' }}
            </button>
          </div>
        }

        @for (sp of systemProducts(); track sp.id) {
          <div class="system-product-panel">
            <div class="header-row">
              <div>
                <h4 style="margin:0;">{{ sp.name }}</h4>
                <p class="text-muted mono" style="margin: 0.15rem 0 0; font-size:0.75rem;">{{ sp.referenceNumber }}</p>
                @if (sp.description) { <p class="text-muted" style="font-size:0.8rem; margin:0.3rem 0 0;">{{ sp.description }}</p> }
              </div>
              <button class="btn btn-outline btn-sm" (click)="toggleAgreementForm(sp.id)">
                {{ agreementFormOpenFor() === sp.id ? 'Cancel' : '+ New Agreement' }}
              </button>
            </div>

            @if (agreementFormOpenFor() === sp.id) {
              <div class="add-form">
                <div class="form-grid">
                  <div class="field">
                    <label>Agreement Type</label>
                    <select [ngModel]="agreementForm.agreementTypeId" (ngModelChange)="onAgreementTypeChange(sp.id, $event)">
                      <option value="">Select type…</option>
                      @for (t of agreementTypesSvc.types(); track t.id) {
                        <option [value]="t.id">{{ t.name }}</option>
                      }
                    </select>
                  </div>
                  <div class="field">
                    <label>Agreement Place</label>
                    <input type="text" [ngModel]="agreementForm.agreementPlace" (ngModelChange)="agreementForm.agreementPlace = $event" placeholder="Addis Ababa" />
                  </div>
                  <div class="field">
                    <label>Signed Date</label>
                    <input type="date" [ngModel]="agreementForm.signDate" (ngModelChange)="agreementForm.signDate = $event" />
                  </div>
                  <div class="field">
                    <label>Support Window (months)</label>
                    <input type="number" [ngModel]="agreementForm.supportWindowMonths" (ngModelChange)="agreementForm.supportWindowMonths = $event" />
                  </div>
                  <div class="field">
                    <label>Billing Tier</label>
                    <select [ngModel]="agreementForm.billingTier" (ngModelChange)="agreementForm.billingTier = $event">
                      <option value="Basic">Basic</option>
                      <option value="Intermediate">Intermediate</option>
                      <option value="Advanced">Advanced</option>
                    </select>
                  </div>
                  <div class="field">
                    <label>Scanned Document</label>
                    <input type="file" accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" (change)="onFileSelected($event)" />
                    @if (selectedFile()) { <span class="text-muted" style="font-size:0.75rem;">{{ selectedFile()!.name }}</span> }
                  </div>
                  <div class="field" style="grid-column: 1 / -1;">
                    <label>Details (optional)</label>
                    <textarea rows="2" [ngModel]="agreementForm.details" (ngModelChange)="agreementForm.details = $event"></textarea>
                  </div>
                </div>

                @if (isSelectedTypeSupport()) {
                  @if (trainingCheckPending()) {
                    <p class="text-muted" style="font-size:0.76rem; margin: 0.8rem 0 0;">Checking training status for this system/product…</p>
                  } @else if (canSignSupport() === false) {
                    <p class="upload-error" style="margin: 0.8rem 0 0;">
                      This system/product has no completed training yet. A Training agreement must be signed and every assigned trainer's work approved before a Support agreement can be signed for it.
                    </p>
                  }
                }

                @if (uploadError()) { <p class="upload-error" style="margin-top:0.75rem;">{{ uploadError() }}</p> }
                <button class="btn btn-primary" style="margin-top:1rem;" (click)="submitAgreement(sp.id)"
                        [disabled]="submitting() || !agreementForm.agreementTypeId || (isSelectedTypeSupport() && canSignSupport() === false)">
                  {{ submitting() ? 'Saving…' : 'Sign Agreement' }}
                </button>
              </div>
            }

            <div class="table-scroll" style="margin-top:0.85rem;"><table>
              <thead><tr><th>Type</th><th>Doc #</th><th>Sign Date</th><th>Expiry</th><th>Tier</th><th>Status</th><th>Document</th><th></th></tr></thead>
              <tbody>
                @for (a of agreementsFor(sp.id); track a.id) {
                  <tr>
                    <td>{{ a.agreementTypeName }}</td>
                    <td class="mono">{{ a.documentNumber }}</td>
                    <td>{{ a.signDate }}</td>
                    <td>{{ a.expiryDate }}</td>
                    <td>{{ a.billingTier }}</td>
                    <td><app-badge [status]="a.status"></app-badge></td>
                    <td>
                      @if (a.scannedFileUrl) {
                        <button class="btn btn-outline btn-sm" (click)="download(a.id)">Download</button>
                      } @else { <span class="text-muted">None</span> }
                    </td>
                    <td>
                      @if (a.agreementTypeName === 'Training') {
                        <a [routerLink]="['/admin/clients', c.id, 'training', a.id]" class="btn btn-outline btn-sm">Training Session</a>
                      }
                    </td>
                  </tr>
                }
                @empty { <tr><td colspan="8" class="text-muted">No agreements yet for this system/product.</td></tr> }
              </tbody>
            </table></div>
          </div>
        }
        @empty {
          <p class="text-muted" style="margin-top:0.9rem;">No systems/products yet — click "+ Add System/Product" to add this client's first one.</p>
        }
      </div>

      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <h3>Full Ticket History with DAFTECH</h3>
        <p class="text-muted" style="font-size:0.8rem; margin: 0.2rem 0 0.9rem;">Used by Admin when assigning new tickets.</p>
        <div class="table-scroll"><table>
          <thead><tr><th>Ticket</th><th>Category</th><th>Submitted</th><th>Chargeable</th><th>Status</th></tr></thead>
          <tbody>
            @for (t of tickets(); track t.id) {
              <tr>
                <td class="mono">{{ t.id }}</td>
                <td>{{ categoryLabel(t.category) }}</td>
                <td class="text-muted">{{ t.dateSubmitted | slice:0:10 }}</td>
                <td><app-badge [status]="t.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
                <td><app-badge [status]="t.status"></app-badge></td>
              </tr>
            }
            @empty { <tr><td colspan="5" class="text-muted">No tickets submitted yet.</td></tr> }
          </tbody>
        </table></div>
      </div>
    } @else {
      <p class="text-muted">Client not found.</p>
    }
  `,
  styles: [`
    .back { display: inline-block; margin-bottom: 1rem; font-size: 0.82rem; color: var(--slate-500); }
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1.25rem; }
    dl { display: grid; grid-template-columns: auto 1fr; gap: 0.4rem 1rem; margin-top: 0.75rem; font-size: 0.85rem; }
    dt { color: var(--slate-500); font-weight: 600; }
    dd { margin: 0; }
    .add-form { margin-top: 0.9rem; padding: 0.9rem; border: 1px solid var(--slate-200); border-radius: 10px; }
    .system-product-panel { margin-top: 1rem; padding: 0.9rem; border: 1px solid var(--slate-200); border-radius: 10px; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 0.85rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
  `],
})
export class ClientDetailComponent {
  id = input.required<string>();

  showNewSystemForm = signal(false);
  savingSystem = signal(false);
  systemError = signal<string | null>(null);
  newSystemForm = { name: '', description: '', deploymentDate: '' };

  agreementFormOpenFor = signal<string | null>(null);
  submitting = signal(false);
  uploadError = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  canSignSupport = signal<boolean | null>(null);
  trainingCheckPending = signal(false);

  // Agreements fetched per system/product, keyed by systemProductId — a
  // client may have several system/products, each with its own agreement
  // list, so this isn't a single flat cache like the old client-level
  // agreements list was.
  private agreementsBySystemProduct = signal<Record<string, Agreement[]>>({});

  agreementForm: {
    agreementTypeId: string; agreementPlace: string; signDate: string;
    supportWindowMonths: number; billingTier: BillingTier; details: string;
  } = this.blankAgreementForm();

  constructor(
    private clientsSvc: ClientService,
    private systemProductsSvc: SystemProductService,
    public agreementsSvc: AgreementService,
    public agreementTypesSvc: AgreementTypeService,
    private ticketsSvc: TicketService,
    private route: ActivatedRoute,
  ) {
    // Load this client's systems/products as soon as the client id is
    // known, then each one's agreements.
    effect(() => {
      const clientId = this.id();
      if (clientId) void this.refreshSystemProducts(clientId);
    });

    // Supports the ?tab=agreements redirect from
    // ClientsListComponent.continueToAgreement — scrolls the Agreements
    // section into view once the page has rendered.
    effect(() => {
      const clientId = this.id();
      const tab = this.route.snapshot.queryParamMap.get('tab');
      if (clientId && tab === 'agreements') {
        setTimeout(() => document.getElementById('agreements-section')?.scrollIntoView({ behavior: 'smooth' }), 150);
      }
    });
  }

  client = computed(() => this.clientsSvc.getById(this.id()));
  systemProducts = computed(() => this.systemProductsSvc.systemProductsFor(this.id()));
  tickets = computed(() => this.ticketsSvc.forClient(this.id()));

  agreementsFor(systemProductId: string): Agreement[] {
    return this.agreementsBySystemProduct()[systemProductId] ?? [];
  }

  isSelectedTypeSupport = computed(() => {
    const type = this.agreementTypesSvc.types().find(t => t.id === this.agreementForm.agreementTypeId);
    return type?.name === 'Support';
  });

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }

  private blankAgreementForm() {
    return {
      agreementTypeId: '', agreementPlace: '', signDate: new Date().toISOString().slice(0, 10),
      supportWindowMonths: 12, billingTier: 'Basic' as BillingTier, details: '',
    };
  }

  private async refreshSystemProducts(clientId: string) {
    const list = await this.systemProductsSvc.refreshForClient(clientId);
    await Promise.all(list.map(sp => this.refreshAgreementsFor(sp.id)));
  }

  private async refreshAgreementsFor(systemProductId: string) {
    try {
      const list = await this.agreementsSvc.fetchForSystemProduct(systemProductId);
      this.agreementsBySystemProduct.update(map => ({ ...map, [systemProductId]: list }));
    } catch (err) {
      console.error('Failed to load agreements for system/product', err);
    }
  }

  toggleNewSystemForm() {
    this.systemError.set(null);
    this.showNewSystemForm.set(!this.showNewSystemForm());
  }

  /** Always creates a brand-new SystemProduct — never overwrites or replaces one the client already has. */
  async submitNewSystem(clientId: string) {
    if (!this.newSystemForm.name.trim()) {
      this.systemError.set('Name is required.');
      return;
    }
    this.savingSystem.set(true);
    this.systemError.set(null);
    try {
      await this.systemProductsSvc.create({
        clientId,
        name: this.newSystemForm.name,
        description: this.newSystemForm.description || undefined,
        deploymentDate: this.newSystemForm.deploymentDate || undefined,
      });
      this.newSystemForm = { name: '', description: '', deploymentDate: '' };
      this.showNewSystemForm.set(false);
    } catch (err: any) {
      this.systemError.set(err?.error?.error ?? 'Could not add this system/product — please try again.');
    } finally {
      this.savingSystem.set(false);
    }
  }

  toggleAgreementForm(systemProductId: string) {
    if (this.agreementFormOpenFor() === systemProductId) {
      this.agreementFormOpenFor.set(null);
      return;
    }
    this.agreementFormOpenFor.set(systemProductId);
    this.agreementForm = this.blankAgreementForm();
    this.selectedFile.set(null);
    this.uploadError.set(null);
    this.canSignSupport.set(null);
  }

  async onAgreementTypeChange(systemProductId: string, agreementTypeId: string) {
    this.agreementForm.agreementTypeId = agreementTypeId;
    const type = this.agreementTypesSvc.types().find(t => t.id === agreementTypeId);
    if (type?.name === 'Support') {
      await this.refreshTrainingCheck(systemProductId);
    } else {
      this.canSignSupport.set(null);
    }
  }

  private async refreshTrainingCheck(systemProductId: string) {
    this.trainingCheckPending.set(true);
    try {
      this.canSignSupport.set(await this.agreementsSvc.systemProductHasCompletedTraining(systemProductId));
    } catch (err) {
      console.error('Failed to check training status', err);
      this.canSignSupport.set(null);
    } finally {
      this.trainingCheckPending.set(false);
    }
  }

  onFileSelected(evt: Event) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    this.selectedFile.set(file ?? null);
    this.uploadError.set(null);
  }

  /**
   * Always creates a brand-new Agreement scoped to this system/product —
   * never touches or overwrites any existing agreement. Rejected with 409
   * by the server if a Support agreement is requested but training isn't
   * complete yet for this SAME system/product.
   */
  async submitAgreement(systemProductId: string) {
    if (this.isSelectedTypeSupport() && this.canSignSupport() === false) return;

    this.submitting.set(true);
    this.uploadError.set(null);
    try {
      const created = await this.agreementsSvc.createAgreement({
        systemProductId,
        agreementTypeId: this.agreementForm.agreementTypeId,
        agreementPlace: this.agreementForm.agreementPlace,
        signDate: this.agreementForm.signDate,
        supportWindowMonths: this.agreementForm.supportWindowMonths,
        billingTier: this.agreementForm.billingTier,
        details: this.agreementForm.details || undefined,
      });

      const file = this.selectedFile();
      if (file) {
        await this.agreementsSvc.uploadScannedFile(created.id, file);
      }

      await this.refreshAgreementsFor(systemProductId);
      this.agreementFormOpenFor.set(null);
      this.selectedFile.set(null);
      this.agreementForm = this.blankAgreementForm();
    } catch (err: any) {
      if (err?.status === 409) {
        this.uploadError.set(err?.error ?? 'This system/product has no completed training yet — the Support agreement cannot be signed.');
      } else {
        this.uploadError.set('The agreement was saved, but a later step failed. You can retry uploads from the agreements list above.');
      }
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  async download(agreementId: string) {
    try {
      const blob = await this.agreementsSvc.downloadScannedFile(agreementId);
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
