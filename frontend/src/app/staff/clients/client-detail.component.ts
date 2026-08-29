import { Component, computed, effect, input, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { ProductCatalogService } from '../../core/services/product-catalog.service';
import { AgreementService } from '../../core/services/agreement.service';
import { AgreementTypeService } from '../../core/services/agreement-type.service';
import { TicketService } from '../../core/services/ticket.service';
import { TrainingService } from '../../core/services/training.service';
import { BadgeComponent } from '../../shared/badge.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { TICKET_CATEGORY_LABELS, BillingTier, Agreement, TrainingRecord, TrainerWorkload } from '../../core/models';

/**
 * Client -> System/Product -> Agreement -> Agreement Type. Each
 * System/Product is its own expandable panel; agreements for it are
 * fetched and shown per-panel, alongside its training roster and
 * open-ended training record log (see SystemProduct.trainingAssignments/
 * trainingCompletionStatus and TrainingRecord). Signing a Support
 * agreement for a System/Product is blocked until that SAME
 * System/Product's training has been marked Completed (see
 * SystemProductService.markTrainingCompleted on the frontend,
 * AgreementService.CreateAsync on the backend — this UI mirrors that gate
 * but the server is the source of truth). Creating a new System/Product,
 * Agreement, or TrainingRecord never overwrites an existing one —
 * everything here is additive.
 *
 * Supports a `?tab=agreements` query param so the redirect after Admin
 * registers a new client and adds its first System/Product (see
 * ClientsListComponent) can land the Admin straight on this section, and
 * a `?focusSystemProduct=<id>` param to auto-open that system/product's
 * Training Assignment panel — see the effect() in the constructor.
 */
@Component({
  selector: 'app-client-detail',
  standalone: true,
  imports: [RouterLink, BadgeComponent, SlicePipe, FormsModule, FilePreviewModalComponent],
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

      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <h3>Login Credentials</h3>
        @if (c.username) {
          <dl>
            <dt>Username</dt><dd class="mono">{{ c.username }}</dd>
            <dt>Password status</dt><dd>{{ c.mustChangePassword ? 'Awaiting first login / change' : 'Set by client' }}</dd>
          </dl>
        } @else {
          <p class="text-muted" style="font-size:0.85rem;">No credentials issued yet.</p>
        }

        <p class="text-muted" style="font-size:0.82rem; margin-top:0.5rem;">
          The one-time password itself is never stored or shown again after it's issued —
          resending generates a brand-new one and emails it to {{ c.email }}.
        </p>

        <button class="btn btn-outline btn-sm" style="margin-top:0.5rem;" [disabled]="resendingCredential()" (click)="resendCredentialEmail(c.id)">
          {{ resendingCredential() ? 'Sending…' : 'Resend credential email' }}
        </button>

        @if (resendResult(); as r) {
          @if (r.emailSent) {
            <p class="text-muted" style="margin-top:0.5rem; font-size:0.85rem;">A new one-time password was emailed to {{ c.email }}.</p>
          } @else {
            <p class="err" style="margin-top:0.5rem;">
              Could not send the email{{ r.emailError ? ' (' + r.emailError + ')' : '' }} — a new password was still generated; you may need to relay it another way, or try resending again.
            </p>
          }
        }
      </div>

      <div class="panel panel-pad" id="agreements-section" style="margin-top:1.25rem;">
        <div class="header-row">
          <h3 style="margin:0;">Systems / Products & Agreements</h3>
          <button class="btn btn-primary btn-sm" (click)="toggleNewSystemForm()">
            {{ showNewSystemForm() ? 'Cancel' : '+ Add System/Product' }}
          </button>
        </div>
        <p class="text-muted" style="font-size:0.78rem; margin: 0.3rem 0 0;">
          A client may have multiple systems/products, and each may carry multiple agreements and training records — nothing shown here is ever overwritten by adding another.
        </p>

        @if (showNewSystemForm()) {
          <div class="add-form">
            <div class="form-grid">
              <div class="field">
                <label>System/Product <span class="req">*</span></label>
                @if (catalog.items().length > 0) {
                  <select [ngModel]="newSystemForm.catalogItemId" (ngModelChange)="onNewSystemCatalogSelect($event)">
                    <option value="">Select a system/product…</option>
                    @for (c of catalog.items(); track c.id) {
                      <option [value]="c.id">{{ c.name }}</option>
                    }
                    <option value="__other__">Other (type manually)…</option>
                  </select>
                  @if (newSystemForm.catalogItemId === '__other__') {
                    <input type="text" style="margin-top:0.4rem;" [ngModel]="newSystemForm.name" (ngModelChange)="newSystemForm.name = $event" placeholder="e.g. Branch POS System" />
                  }
                } @else {
                  <input type="text" [ngModel]="newSystemForm.name" (ngModelChange)="newSystemForm.name = $event" placeholder="e.g. Branch POS System" />
                  <span class="text-muted" style="font-size:0.75rem;">No systems/products configured yet — add some under Settings, or type a name here.</span>
                }
              </div>
              <div class="field">
                <label>Deployment Date (optional)</label>
                <input type="date" [ngModel]="newSystemForm.deploymentDate" (ngModelChange)="newSystemForm.deploymentDate = $event" />
              </div>
              <div class="field">
                <label>Expiry Date (optional)</label>
                <input type="date" [ngModel]="newSystemForm.expiryDate" (ngModelChange)="newSystemForm.expiryDate = $event" />
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
          <div class="system-product-panel" [id]="'system-product-' + sp.id">
            <div class="header-row">
              <div>
                <h4 style="margin:0;">{{ sp.name }}</h4>
                <p class="text-muted mono" style="margin: 0.15rem 0 0; font-size:0.75rem;">{{ sp.referenceNumber }}</p>
                @if (sp.expiryDate) {
                  <p class="text-muted" style="font-size:0.78rem; margin:0.2rem 0 0;">
                    Expires {{ sp.expiryDate }}
                  </p>
                }
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
                      This system/product's training hasn't been marked Completed yet — see the Training panel below.
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
              <thead><tr><th>Type</th><th>Doc #</th><th>Sign Date</th><th>Expiry</th><th>Tier</th><th>Status</th><th>Document</th></tr></thead>
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
                        <button class="btn btn-outline btn-sm" (click)="viewAgreementFile(a.id, a.scannedFileUrl)">View</button>
                      } @else { <span class="text-muted">None</span> }
                    </td>
                  </tr>
                }
                @empty { <tr><td colspan="7" class="text-muted">No agreements yet for this system/product.</td></tr> }
              </tbody>
            </table></div>

            <div class="training-panel">
              <div class="header-row">
                <h5 style="margin:0;">Training</h5>
                <app-badge [status]="sp.trainingCompletionStatus"></app-badge>
              </div>

              <div class="roster">
                <p class="text-muted" style="font-size:0.76rem; margin: 0.6rem 0 0.4rem;">Assigned Trainers</p>
                @if (sp.trainingAssignments.length === 0) {
                  <p class="text-muted" style="font-size:0.82rem;">No trainers assigned yet.</p>
                } @else {
                  <ul class="roster-list">
                    @for (ta of sp.trainingAssignments; track ta.id) {
                      <li>
                        {{ ta.trainerEmployeeName }}
                        <button class="btn btn-outline btn-sm" (click)="removeTrainer(sp.id, ta.id)">Remove</button>
                      </li>
                    }
                  </ul>
                }

                <div class="assign-row">
                  <select [(ngModel)]="manualTrainerSelection[sp.id]" style="max-width:260px;">
                    <option value="">Select a trainer…</option>
                    @for (w of availableTrainers(sp); track w.employeeId) {
                      <option [value]="w.employeeId">
                        {{ w.employeeName }}{{ w.employeeId === recommendedTrainerId() ? ' (recommended)' : '' }}{{ w.isExcessiveWorkload ? ' — excessive workload' : '' }}
                      </option>
                    }
                  </select>
                  <button class="btn btn-outline btn-sm" [disabled]="!manualTrainerSelection[sp.id] || rosterBusy() === sp.id" (click)="addTrainerManually(sp.id)">
                    Manual Assign
                  </button>
                  <button class="btn btn-outline btn-sm" [disabled]="rosterBusy() === sp.id" (click)="autoAssign(sp.id)">
                    Automatic Assignment
                  </button>
                </div>
                @if (rosterError()[sp.id]) { <p class="upload-error" style="margin-top:0.4rem;">{{ rosterError()[sp.id] }}</p> }
              </div>

              <div class="records">
                <p class="text-muted" style="font-size:0.76rem; margin: 0.9rem 0 0.4rem;">Training Records</p>
                @if (recordsFor(sp.id).length === 0) {
                  <p class="text-muted" style="font-size:0.82rem;">No training sessions logged yet.</p>
                } @else {
                  <div class="table-scroll"><table>
                    <thead><tr><th>Date</th><th>Item</th><th>Trainer</th><th>Start</th><th>End</th><th>Description</th><th>File</th></tr></thead>
                    <tbody>
                      @for (r of recordsFor(sp.id); track r.id) {
                        <tr>
                          <td>{{ r.trainingDate }}</td>
                          <td>{{ r.agreementTypeName }}</td>
                          <td>{{ r.trainerEmployeeName }}</td>
                          <td>{{ r.startDateTime ? (r.startDateTime | slice:11:16) : '—' }}</td>
                          <td>{{ r.endDateTime ? (r.endDateTime | slice:11:16) : '—' }}</td>
                          <td>{{ r.description }}</td>
                          <td>
                            @if (r.fileName) {
                              <button class="btn btn-outline btn-sm" (click)="viewRecordFile(r)">{{ r.fileName }}</button>
                            } @else { <span class="text-muted">None</span> }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table></div>
                }

                @if (sp.trainingSubmittedAt) {
                  <p class="text-muted" style="font-size:0.76rem; margin: 0.5rem 0 0;">Trainer submitted this checklist on {{ sp.trainingSubmittedAt | slice:0:10 }}.</p>
                }

                @if (sp.trainingCompletionStatus !== 'Completed') {
                  <button class="btn btn-primary btn-sm" style="margin-top:0.8rem;" [disabled]="markingComplete() === sp.id" (click)="markTrainingCompleted(sp.id)">
                    {{ markingComplete() === sp.id ? 'Saving…' : 'Mark Training Completed' }}
                  </button>
                } @else {
                  <p class="text-muted" style="font-size:0.78rem; margin-top:0.6rem;">Training marked Completed — a trainer can still log a refresher session above if needed.</p>
                }
              </div>
            </div>
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

    <app-file-preview-modal
      [open]="previewOpen"
      [title]="previewTitle"
      [fileName]="previewFileName"
      [kind]="previewKind"
      [load]="previewLoader"
      (closed)="closePreview()">
    </app-file-preview-modal>
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
    .field label .req { color: var(--red, #b3261e); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .training-panel { margin-top: 1rem; padding-top: 0.9rem; border-top: 1px dashed var(--slate-200); }
    .roster-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.35rem; }
    .roster-list li { display: flex; align-items: center; gap: 0.6rem; font-size: 0.85rem; }
    .assign-row { display: flex; align-items: center; gap: 0.5rem; margin-top: 0.6rem; flex-wrap: wrap; }
  `],
})
export class ClientDetailComponent {
  id = input.required<string>();

  showNewSystemForm = signal(false);
  savingSystem = signal(false);
  systemError = signal<string | null>(null);
  newSystemForm = { name: '', description: '', deploymentDate: '', catalogItemId: '', expiryDate: '' };

  agreementFormOpenFor = signal<string | null>(null);
  submitting = signal(false);
  uploadError = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  resendingCredential = signal(false);
  resendResult = signal<{ emailSent: boolean; emailError?: string } | null>(null);

  async resendCredentialEmail(clientId: string) {
    this.resendingCredential.set(true);
    this.resendResult.set(null);
    try {
      const result = await this.clientsSvc.resendCredentialEmail(clientId);
      this.resendResult.set(result);
      await this.clientsSvc.refresh();
    } catch (err) {
      this.resendResult.set({ emailSent: false });
      console.error('Failed to resend credential email', err);
    } finally {
      this.resendingCredential.set(false);
    }
  }

  canSignSupport = signal<boolean | null>(null);
  trainingCheckPending = signal(false);

  // Agreements fetched per system/product, keyed by systemProductId — a
  // client may have several system/products, each with its own agreement
  // list, so this isn't a single flat cache like the old client-level
  // agreements list was.
  private agreementsBySystemProduct = signal<Record<string, Agreement[]>>({});

  // Training records fetched per system/product, same keying reasoning.
  private recordsBySystemProduct = signal<Record<string, TrainingRecord[]>>({});

  trainerWorkloads = signal<TrainerWorkload[]>([]);
  recommendedTrainerId = signal<string | undefined>(undefined);
  manualTrainerSelection: Record<string, string> = {};
  rosterBusy = signal<string | null>(null);
  rosterError = signal<Record<string, string>>({});
  markingComplete = signal<string | null>(null);

  agreementForm: {
    agreementTypeId: string; agreementPlace: string; signDate: string;
    supportWindowMonths: number; billingTier: BillingTier; details: string;
  } = this.blankAgreementForm();

  constructor(
    private clientsSvc: ClientService,
    public systemProductsSvc: SystemProductService,
    public catalog: ProductCatalogService,
    public agreementsSvc: AgreementService,
    public agreementTypesSvc: AgreementTypeService,
    private ticketsSvc: TicketService,
    private trainingSvc: TrainingService,
    private route: ActivatedRoute,
  ) {
    // Load this client's systems/products as soon as the client id is
    // known, then each one's agreements and training records.
    effect(() => {
      const clientId = this.id();
      if (clientId) void this.refreshSystemProducts(clientId);
    });

    void this.loadTrainerWorkload();

    // Supports the ?tab=agreements redirect from ClientsListComponent
    // (after registering a client and adding its first system/product) —
    // scrolls the Agreements section, and if ?focusSystemProduct=<id> is
    // also present, scrolls straight to that system/product's panel so
    // Training Assignment is immediately visible.
    effect(() => {
      const clientId = this.id();
      const params = this.route.snapshot.queryParamMap;
      const tab = params.get('tab');
      const focusSystemProductId = params.get('focusSystemProduct');
      if (clientId && tab === 'agreements') {
        setTimeout(() => {
          const targetId = focusSystemProductId ? `system-product-${focusSystemProductId}` : 'agreements-section';
          document.getElementById(targetId)?.scrollIntoView({ behavior: 'smooth' });
        }, 150);
      }
    });
  }

  client = computed(() => this.clientsSvc.getById(this.id()));
  systemProducts = computed(() => this.systemProductsSvc.systemProductsFor(this.id()));
  tickets = computed(() => this.ticketsSvc.forClient(this.id()));

  agreementsFor(systemProductId: string): Agreement[] {
    return this.agreementsBySystemProduct()[systemProductId] ?? [];
  }

  recordsFor(systemProductId: string): TrainingRecord[] {
    return this.recordsBySystemProduct()[systemProductId] ?? [];
  }

  /** Trainers not already on this system/product's roster — the only ones worth showing in the Manual Assign dropdown. */
  availableTrainers(sp: { trainingAssignments: { trainerEmployeeId: string }[] }): TrainerWorkload[] {
    const assignedIds = new Set(sp.trainingAssignments.map(a => a.trainerEmployeeId));
    return this.trainerWorkloads().filter(w => !assignedIds.has(w.employeeId));
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
    await Promise.all(list.map(sp => Promise.all([this.refreshAgreementsFor(sp.id), this.refreshRecordsFor(sp.id)])));
  }

  private async refreshAgreementsFor(systemProductId: string) {
    try {
      const list = await this.agreementsSvc.fetchForSystemProduct(systemProductId);
      this.agreementsBySystemProduct.update(map => ({ ...map, [systemProductId]: list }));
    } catch (err) {
      console.error('Failed to load agreements for system/product', err);
    }
  }

  private async refreshRecordsFor(systemProductId: string) {
    try {
      const list = await this.trainingSvc.getForSystemProduct(systemProductId);
      this.recordsBySystemProduct.update(map => ({ ...map, [systemProductId]: list }));
    } catch (err) {
      console.error('Failed to load training records for system/product', err);
    }
  }

  private async loadTrainerWorkload() {
    try {
      const rec = await this.systemProductsSvc.getTrainerWorkload();
      this.trainerWorkloads.set(rec.eligibleTrainers);
      this.recommendedTrainerId.set(rec.recommendedTrainerEmployeeId);
    } catch (err) {
      console.error('Failed to load trainer workload', err);
    }
  }

  toggleNewSystemForm() {
    this.systemError.set(null);
    this.newSystemForm = { name: '', description: '', deploymentDate: '', catalogItemId: '', expiryDate: '' };
    this.showNewSystemForm.set(!this.showNewSystemForm());
  }

  /** Selecting a catalog entry sets its name as the system/product's Name (source of truth for display); "Other" clears it so the free-text input takes over. */
  onNewSystemCatalogSelect(value: string) {
    this.newSystemForm.catalogItemId = value;
    if (value && value !== '__other__') {
      const match = this.catalog.items().find(c => c.id === value);
      this.newSystemForm.name = match?.name ?? '';
    } else {
      this.newSystemForm.name = '';
    }
  }

  /** Always creates a brand-new SystemProduct — never overwrites or replaces one the client already has. Starts with an empty training roster. */
  async submitNewSystem(clientId: string) {
    if (!this.newSystemForm.name.trim()) {
      this.systemError.set('System/Product is required — select one from the list, or choose "Other" and type a name.');
      return;
    }
    this.savingSystem.set(true);
    this.systemError.set(null);
    try {
      const catalogItemId = this.newSystemForm.catalogItemId && this.newSystemForm.catalogItemId !== '__other__'
        ? this.newSystemForm.catalogItemId
        : undefined;
      await this.systemProductsSvc.create({
        clientId,
        name: this.newSystemForm.name,
        description: this.newSystemForm.description || undefined,
        deploymentDate: this.newSystemForm.deploymentDate || undefined,
        catalogItemId,
        expiryDate: this.newSystemForm.expiryDate || undefined,
      });
      this.newSystemForm = { name: '', description: '', deploymentDate: '', catalogItemId: '', expiryDate: '' };
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
      this.canSignSupport.set(await this.systemProductsSvc.hasCompletedTraining(systemProductId));
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
   * marked Completed yet for this SAME system/product.
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
        this.uploadError.set(err?.error ?? "This system/product's training hasn't been marked Completed yet — the Support agreement cannot be signed.");
      } else {
        this.uploadError.set('The agreement was saved, but a later step failed. You can retry uploads from the agreements list above.');
      }
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  // Shown inline in the shared preview modal (audio player / image / PDF
  // viewer) rather than forcing a download — see FilePreviewModalComponent.
  previewOpen = false;
  previewTitle = 'Preview';
  previewFileName = '';
  previewKind: FilePreviewKind = 'other';
  previewLoader?: () => Promise<Blob>;

  viewAgreementFile(agreementId: string, scannedFileUrl: string) {
    this.previewTitle = 'Scanned Agreement';
    this.previewFileName = scannedFileUrl;
    this.previewKind = filePreviewKindFor(scannedFileUrl);
    this.previewLoader = () => this.agreementsSvc.downloadScannedFile(agreementId);
    this.previewOpen = true;
  }

  viewRecordFile(r: TrainingRecord) {
    this.previewTitle = 'Training Record File';
    this.previewFileName = r.fileName ?? '';
    this.previewKind = filePreviewKindFor(r.fileName);
    this.previewLoader = () => this.trainingSvc.downloadFile(r.id);
    this.previewOpen = true;
  }

  closePreview() {
    this.previewOpen = false;
  }

  private async refreshSystemProductOnly(systemProductId: string, clientId: string) {
    // The roster/status changed on one system/product — cheapest correct
    // refresh is re-fetching the whole client list (it's already cached
    // and small), rather than patching just one entry's nested arrays by
    // hand and risking it drifting from the server.
    await this.systemProductsSvc.refreshForClient(clientId);
  }

  async addTrainerManually(systemProductId: string) {
    const trainerEmployeeId = this.manualTrainerSelection[systemProductId];
    if (!trainerEmployeeId) return;

    this.rosterBusy.set(systemProductId);
    this.rosterError.update(m => ({ ...m, [systemProductId]: '' }));
    try {
      await this.systemProductsSvc.addTrainingAssignment(systemProductId, trainerEmployeeId);
      await this.refreshSystemProductOnly(systemProductId, this.id());
      this.manualTrainerSelection[systemProductId] = '';
    } catch (err: any) {
      this.rosterError.update(m => ({ ...m, [systemProductId]: err?.error ?? 'Could not assign this trainer — please try again.' }));
      console.error(err);
    } finally {
      this.rosterBusy.set(null);
    }
  }

  async autoAssign(systemProductId: string) {
    this.rosterBusy.set(systemProductId);
    this.rosterError.update(m => ({ ...m, [systemProductId]: '' }));
    try {
      await this.systemProductsSvc.autoAssignTrainers(systemProductId);
      await this.refreshSystemProductOnly(systemProductId, this.id());
    } catch (err: any) {
      this.rosterError.update(m => ({ ...m, [systemProductId]: err?.error ?? 'Could not auto-assign trainers — please try again.' }));
      console.error(err);
    } finally {
      this.rosterBusy.set(null);
    }
  }

  async removeTrainer(systemProductId: string, assignmentId: string) {
    try {
      await this.systemProductsSvc.removeTrainingAssignment(systemProductId, assignmentId);
      await this.refreshSystemProductOnly(systemProductId, this.id());
    } catch (err) {
      console.error('Failed to remove training assignment', err);
    }
  }

  async markTrainingCompleted(systemProductId: string) {
    this.markingComplete.set(systemProductId);
    try {
      await this.systemProductsSvc.markTrainingCompleted(systemProductId);
      await this.refreshSystemProductOnly(systemProductId, this.id());
    } catch (err) {
      console.error('Failed to mark training completed', err);
    } finally {
      this.markingComplete.set(null);
    }
  }
}
