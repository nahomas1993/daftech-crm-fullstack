import { Component, computed, effect, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';
import { AgreementService } from '../../core/services/agreement.service';
import { TicketService } from '../../core/services/ticket.service';
import { BadgeComponent } from '../../shared/badge.component';
import { TICKET_CATEGORY_LABELS, BillingTier, AgreementTraining } from '../../core/models';

/**
 * Client → Training → Agreements. Reads agreements from
 * forClientStaffView() (the staff-only full agreements list, already
 * loaded for every Admin/Employee session) — see AgreementService for why
 * that's the correct source for a staff session.
 *
 * Training now belongs to the client directly and exists independently of
 * any agreement — a client can (and must) have a completed training
 * (an End Date set) before an agreement can be signed at all. The
 * training panel is keyed by client, always visible, and the "Add
 * Agreement" action is blocked with a clear message until training is
 * complete — matching the server-side hard-block in
 * AgreementService.CreateAsync.
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

      <div class="grid">
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

        <div class="panel panel-pad">
          <div class="header-row">
            <h3 style="margin:0;">Training</h3>
            <button class="btn btn-outline btn-sm" (click)="addTrainingRow(c.id)" [disabled]="submitting()">+ Add Training</button>
          </div>
          <p class="text-muted" style="font-size:0.78rem; margin: 0.3rem 0 0;">
            Recorded before any agreement — the support agreement can be signed below once at least one training has an End Date.
            End Date stays editable afterward if training runs long.
          </p>

          @if (trainingError()) { <p class="upload-error" style="margin-top:0.75rem;">{{ trainingError() }}</p> }

          @for (row of trainingRows(); track row.training.id) {
            <div class="training-row">
              <div class="header-row">
                <h5 style="margin:0;">Training {{ $index + 1 }}</h5>
                <button class="btn btn-outline btn-sm" (click)="deleteTrainingRow(c.id, row.training.id)" [disabled]="submitting()">Delete</button>
              </div>
              <div class="form-grid" style="margin-top:0.6rem;">
                <div class="field">
                  <label>Start Date</label>
                  <input type="date" [ngModel]="row.startDate" (ngModelChange)="row.startDate = $event" />
                </div>
                <div class="field">
                  <label>End Date</label>
                  <input type="date" [ngModel]="row.endDate" (ngModelChange)="row.endDate = $event" />
                </div>
                <div class="field" style="grid-column: 1 / -1;">
                  <label>Description</label>
                  <textarea rows="3" maxlength="1000" [ngModel]="row.description" (ngModelChange)="row.description = $event"></textarea>
                  <span class="text-muted" style="font-size:0.72rem; align-self:flex-end;">{{ row.description.length }}/1000</span>
                </div>
                <div class="field">
                  <label>Scan</label>
                  <input type="file" accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" (change)="onTrainingFileSelected($event, row)" />
                  @if (row.selectedFile) {
                    <span class="text-muted" style="font-size:0.75rem;">{{ row.selectedFile.name }}</span>
                  } @else if (row.training.scanFileName) {
                    <span class="text-muted" style="font-size:0.75rem;">
                      Current: {{ row.training.scanFileName }}
                      <button class="btn btn-outline btn-sm" style="margin-left:0.5rem;" (click)="downloadTrainingScan(c.id, row.training.id)">Download</button>
                    </span>
                  }
                </div>
              </div>
              <button class="btn btn-primary btn-sm" style="margin-top:0.7rem;" (click)="saveTrainingRow(c.id, row)" [disabled]="submitting()">
                {{ submitting() ? 'Saving…' : 'Save Training ' + ($index + 1) }}
              </button>
            </div>
          }
          @empty {
            <p class="text-muted" style="margin-top:0.75rem;">No trainings yet — click "+ Add Training" to add one.</p>
          }
        </div>
      </div>

      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <div class="header-row">
          <h3 style="margin:0;">Agreements</h3>
          <button class="btn btn-primary btn-sm" (click)="toggleForm(c.id)">
            {{ showForm() ? 'Cancel' : '+ Add Agreement' }}
          </button>
        </div>
        <p class="text-muted" style="font-size:0.78rem; margin: 0.3rem 0 0;">
          A client may have multiple agreements — each new one shown below is independent.
        </p>

        @if (showForm()) {
          <div class="add-form">
            <div class="form-grid">
              <div class="field">
                <label>Agreement Place</label>
                <input type="text" [ngModel]="form.agreementPlace" (ngModelChange)="form.agreementPlace = $event" placeholder="Addis Ababa" />
              </div>
              <div class="field">
                <label>Support Window (months)</label>
                <input type="number" [ngModel]="form.supportWindowMonths" (ngModelChange)="form.supportWindowMonths = $event" />
              </div>
              <div class="field">
                <label>Billing Tier</label>
                <select [ngModel]="form.billingTier" (ngModelChange)="form.billingTier = $event">
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
            </div>

            @if (trainingCheckPending()) {
              <p class="text-muted" style="font-size:0.76rem; margin: 0.8rem 0 0;">Checking training status…</p>
            } @else if (canSign() === false) {
              <p class="upload-error" style="margin: 0.8rem 0 0;">
                This client has no completed training yet (see Training panel above). Training must finish before the support agreement can be signed.
              </p>
            } @else {
              <p class="text-muted" style="font-size:0.76rem; margin: 0.8rem 0 0;">
                Sign Date is set to today when you save — creating this agreement is the act of signing it.
              </p>
            }

            @if (uploadError()) { <p class="upload-error" style="margin-top:0.75rem;">{{ uploadError() }}</p> }
            <button class="btn btn-primary" style="margin-top:1rem;" (click)="submit(c.id)" [disabled]="submitting() || canSign() === false">
              {{ submitting() ? 'Saving…' : 'Sign Agreement' }}
            </button>
          </div>
        }

        <div class="table-scroll" style="margin-top:1rem;"><table>
          <thead><tr><th>Doc #</th><th>Sign Date</th><th>Expiry</th><th>Tier</th><th>Status</th><th>Document</th></tr></thead>
          <tbody>
            @for (a of agreements(); track a.id) {
              <tr>
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
              </tr>
            }
            @empty { <tr><td colspan="6" class="text-muted">No agreements on file.</td></tr> }
          </tbody>
        </table></div>
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
    .grid { display: grid; grid-template-columns: 1fr 1.4fr; gap: 1.25rem; align-items: start; }
    dl { display: grid; grid-template-columns: auto 1fr; gap: 0.4rem 1rem; margin-top: 0.75rem; font-size: 0.85rem; }
    dt { color: var(--slate-500); font-weight: 600; }
    dd { margin: 0; }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
    .add-form { margin-top: 0.9rem; padding: 0.9rem; border: 1px solid var(--slate-200); border-radius: 10px; }
    .training-row { margin-top: 0.85rem; padding: 0.75rem; border: 1px solid var(--slate-200); border-radius: 8px; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 0.85rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
  `],
})
export class ClientDetailComponent {
  id = input.required<string>();

  showForm = signal(false);
  submitting = signal(false);
  uploadError = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  trainingRows = signal<TrainingRowState[]>([]);
  trainingError = signal<string | null>(null);

  canSign = signal<boolean | null>(null);
  trainingCheckPending = signal(false);

  form: {
    agreementPlace: string;
    supportWindowMonths: number; billingTier: BillingTier;
  } = this.blankForm();

  constructor(
    private clientsSvc: ClientService,
    public agreementsSvc: AgreementService,
    private ticketsSvc: TicketService
  ) {
    // Load this client's trainings as soon as the client id is known —
    // the panel is always visible now, not gated behind an agreement.
    effect(() => {
      const clientId = this.id();
      if (clientId) void this.refreshTrainings(clientId);
    });
  }

  client = computed(() => this.clientsSvc.getById(this.id()));
  // Reads the staff-only full agreements list (already loaded for every
  // Admin/Employee session) instead of the client-portal-only myAgreements
  // list — see the class-level comment above for why the old forClient()
  // call here never showed anything for a staff user.
  agreements = computed(() => this.agreementsSvc.forClientStaffView(this.id()));
  tickets = computed(() => this.ticketsSvc.forClient(this.id()));

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }

  private blankForm() {
    return {
      agreementPlace: '',
      supportWindowMonths: 12, billingTier: 'Basic' as BillingTier,
    };
  }

  toggleForm(clientId: string) {
    this.showForm.set(!this.showForm());
    if (this.showForm()) void this.refreshTrainingCheck(clientId);
  }

  private async refreshTrainingCheck(clientId: string) {
    this.trainingCheckPending.set(true);
    try {
      this.canSign.set(await this.agreementsSvc.clientHasCompletedTraining(clientId));
    } catch (err) {
      console.error('Failed to check training status', err);
      this.canSign.set(null);
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
   * Always creates a brand-new Agreement record scoped to this client —
   * never touches or overwrites any existing agreement, so a client can
   * accumulate several over time, all listed in the table below. Rejected
   * with 409 by the server if training isn't complete yet.
   */
  async submit(clientId: string) {
    if (this.canSign() === false) return;

    this.submitting.set(true);
    this.uploadError.set(null);
    try {
      const created = await this.agreementsSvc.createAgreement({ clientId, ...this.form });

      const file = this.selectedFile();
      if (file) {
        await this.agreementsSvc.uploadScannedFile(created.id, file);
      }

      this.showForm.set(false);
      this.selectedFile.set(null);
      this.form = this.blankForm();
    } catch (err: any) {
      if (err?.status === 409) {
        this.uploadError.set(err?.error ?? 'This client has no completed training yet — the agreement cannot be signed.');
      } else {
        this.uploadError.set('The agreement was saved, but a later step failed. You can retry uploads from the agreements list below.');
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

  private toRowState(t: AgreementTraining): TrainingRowState {
    return {
      training: t,
      description: t.description ?? '',
      startDate: t.startDate?.slice(0, 10) ?? '',
      endDate: t.endDate?.slice(0, 10) ?? '',
      selectedFile: null,
    };
  }

  private async refreshTrainings(clientId: string) {
    try {
      const trainings = await this.agreementsSvc.getTrainingsForClient(clientId);
      this.trainingRows.set(trainings.map(t => this.toRowState(t)));
    } catch (err) {
      this.trainingError.set('Could not load trainings for this client.');
      console.error(err);
    }
  }

  async addTrainingRow(clientId: string) {
    this.submitting.set(true);
    this.trainingError.set(null);
    try {
      await this.agreementsSvc.addTraining(clientId);
      await this.refreshTrainings(clientId);
    } catch (err) {
      this.trainingError.set('Could not add a new training row — please try again.');
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  onTrainingFileSelected(evt: Event, row: TrainingRowState) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    row.selectedFile = file ?? null;
    this.trainingError.set(null);
  }

  async saveTrainingRow(clientId: string, row: TrainingRowState) {
    this.submitting.set(true);
    this.trainingError.set(null);
    try {
      await this.agreementsSvc.saveTraining(clientId, row.training.id, {
        description: row.description || undefined,
        startDate: row.startDate || undefined,
        endDate: row.endDate || undefined,
      });

      if (row.selectedFile) {
        await this.agreementsSvc.uploadTrainingScan(clientId, row.training.id, row.selectedFile);
      }

      await this.refreshTrainings(clientId);
      // The agreement form's Sign button may now unlock if training just
      // became complete — refresh the check if the form is open.
      if (this.showForm()) void this.refreshTrainingCheck(clientId);
    } catch (err) {
      this.trainingError.set('Could not save this training — please try again.');
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  async deleteTrainingRow(clientId: string, trainingId: string) {
    this.submitting.set(true);
    this.trainingError.set(null);
    try {
      await this.agreementsSvc.deleteTraining(clientId, trainingId);
      await this.refreshTrainings(clientId);
      if (this.showForm()) void this.refreshTrainingCheck(clientId);
    } catch (err) {
      this.trainingError.set('Could not delete this training — please try again.');
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  async downloadTrainingScan(clientId: string, trainingId: string) {
    try {
      const blob = await this.agreementsSvc.downloadTrainingScan(clientId, trainingId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = '';
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Failed to download training scan', err);
    }
  }
}

interface TrainingRowState {
  training: AgreementTraining;
  description: string;
  startDate: string;
  endDate: string;
  selectedFile: File | null;
}
