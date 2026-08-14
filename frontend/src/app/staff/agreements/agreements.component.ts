import { Component, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AgreementService } from '../../core/services/agreement.service';
import { ClientService } from '../../core/services/client.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { AgreementTraining, BillingTier } from '../../core/models';

@Component({
  selector: 'app-agreements',
  standalone: true,
  imports: [FormsModule, BadgeComponent, PaginationComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Agreements</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Scanned agreement documents, billing tiers, and support windows.</p>
      </div>
      <button class="btn btn-primary" (click)="toggleForm()">{{ showForm() ? 'Cancel' : '+ New Agreement' }}</button>
    </div>

    @if (showForm()) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <div class="form-grid">
          <div class="field">
            <label>Client</label>
            <select [ngModel]="form.clientId" (ngModelChange)="onClientChange($event)">
              @for (c of clients.approvedClients(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
            </select>
          </div>
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
          <p class="text-muted" style="font-size:0.78rem; margin: 1rem 0 0;">Checking training status…</p>
        } @else if (canSignForSelectedClient() === false) {
          <p class="upload-error" style="margin: 1rem 0 0;">
            This client has no completed training yet. Training must finish (an End Date set) before the support agreement can be signed —
            <button class="btn btn-outline btn-sm" style="margin-left:0.25rem;" (click)="viewTraining(form.clientId)">manage training</button>.
          </p>
        } @else {
          <p class="text-muted" style="font-size:0.78rem; margin: 1rem 0 0;">
            Sign Date is set to today when you save — creating this agreement is the act of signing it.
          </p>
        }

        @if (uploadError()) { <p class="upload-error" style="margin-top:0.75rem;">{{ uploadError() }}</p> }
        <button class="btn btn-primary" style="margin-top:1rem;" (click)="submit()" [disabled]="submitting() || canSignForSelectedClient() === false">
          {{ submitting() ? 'Saving…' : 'Sign Agreement' }}
        </button>
      </div>
    }

    @if (viewingTrainingClientId(); as clientId) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <div class="header-row">
          <h3 style="margin:0;">Trainings — {{ clientName(clientId) }}</h3>
          <div style="display:flex; gap:0.5rem;">
            <button class="btn btn-outline btn-sm" (click)="addTrainingRow(clientId)" [disabled]="submitting()">+ Add Training</button>
            <button class="btn btn-outline btn-sm" (click)="closeTrainingView()">Close</button>
          </div>
        </div>
        <p class="text-muted" style="font-size:0.78rem; margin: 0.6rem 0 0;">
          A client may have multiple trainings (e.g. separate sessions for different staff groups), recorded here before any agreement exists.
          Once at least one has an End Date, the support agreement can be signed above. End Date stays editable afterward if training runs long.
        </p>

        @if (uploadError()) { <p class="upload-error" style="margin-top:0.75rem;">{{ uploadError() }}</p> }

        @for (row of trainingRows(); track row.training.id) {
          <div class="training-row">
            <div class="header-row">
              <h4 style="margin:0;">Training {{ $index + 1 }}</h4>
              <button class="btn btn-outline btn-sm" (click)="deleteTrainingRow(clientId, row.training.id)" [disabled]="submitting()">Delete</button>
            </div>
            <div class="form-grid" style="margin-top:0.75rem;">
              <div class="field">
                <label>Start Date</label>
                <input type="date" [ngModel]="row.startDate" (ngModelChange)="row.startDate = $event" />
              </div>
              <div class="field">
                <label>End Date</label>
                <input type="date" [ngModel]="row.endDate" (ngModelChange)="row.endDate = $event" />
              </div>
              <div class="field">
                <label>Scan</label>
                <input type="file" accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" (change)="onTrainingFileSelected($event, row)" />
                @if (row.selectedFile) {
                  <span class="text-muted" style="font-size:0.75rem;">{{ row.selectedFile.name }}</span>
                } @else if (row.training.scanFileName) {
                  <span class="text-muted" style="font-size:0.75rem;">
                    Current: {{ row.training.scanFileName }}
                    <button class="btn btn-outline btn-sm" style="margin-left:0.5rem;" (click)="downloadTrainingScan(clientId, row.training.id)">Download</button>
                  </span>
                }
              </div>
              <div class="field" style="grid-column: 1 / -1;">
                <label>Description</label>
                <textarea rows="3" maxlength="1000" [ngModel]="row.description" (ngModelChange)="row.description = $event" placeholder="What was covered, who attended…"></textarea>
                <span class="text-muted" style="font-size:0.72rem; align-self:flex-end;">{{ row.description.length }}/1000</span>
              </div>
            </div>
            <button class="btn btn-primary btn-sm" style="margin-top:0.75rem;" (click)="saveTrainingRow(clientId, row)" [disabled]="submitting()">
              {{ submitting() ? 'Saving…' : 'Save Training ' + ($index + 1) }}
            </button>
          </div>
        }
        @empty {
          <p class="text-muted" style="margin-top:0.9rem;">No trainings yet — click "+ Add Training" to add one.</p>
        }
      </div>
    }

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="table-scroll"><table>
        <thead><tr><th>Client</th><th>Doc #</th><th>Sign Date</th><th>Expiry</th><th>Support Window</th><th>Tier</th><th>Status</th><th>Document</th><th>Trainings</th></tr></thead>
        <tbody>
          @for (a of agreements.pagedAgreements(); track a.id) {
            <tr>
              <td>{{ clientName(a.clientId) }}</td>
              <td class="mono">{{ a.documentNumber }}</td>
              <td>{{ a.signDate }}</td>
              <td>{{ a.expiryDate }}</td>
              <td class="text-muted">{{ a.supportWindowMonths }} mo</td>
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
                <button class="btn btn-outline btn-sm" (click)="viewTraining(a.clientId)">
                  {{ a.trainings.length > 0 ? a.trainings.length + ' training' + (a.trainings.length > 1 ? 's' : '') : 'View training' }}
                </button>
              </td>
            </tr>
          }
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
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .training-row { margin-top: 1rem; padding: 0.9rem; border: 1px solid var(--slate-200); border-radius: 10px; }
    .training-row:first-of-type { margin-top: 1.1rem; }
  `],
})
export class AgreementsComponent {
  showForm = signal(false);
  submitting = signal(false);
  uploadError = signal<string | null>(null);
  selectedFile = signal<File | null>(null);

  // Whether the currently-selected client in the New Agreement form has a
  // completed training — null while unchecked/checking, so the button
  // doesn't flash enabled before the check resolves.
  canSignForSelectedClient = signal<boolean | null>(null);
  trainingCheckPending = signal(false);

  // Trainings panel — keyed by CLIENT now, not agreement, since training
  // exists independently of (and before) any agreement.
  viewingTrainingClientId = signal<string | null>(null);
  trainingRows = signal<TrainingRowState[]>([]);

  form: {
    clientId: string; agreementPlace: string;
    supportWindowMonths: number; billingTier: BillingTier;
  } = {
    clientId: '', agreementPlace: '',
    supportWindowMonths: 12, billingTier: 'Basic',
  };

  constructor(public agreements: AgreementService, public clients: ClientService) {
    effect(() => {
      const list = clients.approvedClients();
      if (list.length > 0 && !this.form.clientId) {
        this.form.clientId = list[0].id;
        void this.refreshTrainingCheck();
      }
    });
  }

  clientName(id: string): string {
    return this.clients.getById(id)?.name ?? id;
  }

  toggleForm() {
    this.showForm.set(!this.showForm());
    if (this.showForm()) void this.refreshTrainingCheck();
  }

  onClientChange(clientId: string) {
    this.form.clientId = clientId;
    void this.refreshTrainingCheck();
  }

  private async refreshTrainingCheck() {
    if (!this.form.clientId) { this.canSignForSelectedClient.set(null); return; }
    this.trainingCheckPending.set(true);
    try {
      const complete = await this.agreements.clientHasCompletedTraining(this.form.clientId);
      this.canSignForSelectedClient.set(complete);
    } catch (err) {
      console.error('Failed to check training status', err);
      this.canSignForSelectedClient.set(null);
    } finally {
      this.trainingCheckPending.set(false);
    }
  }

  onFileSelected(evt: Event) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    this.selectedFile.set(file ?? null);
    this.uploadError.set(null);
  }

  async submit() {
    if (!this.form.clientId || this.canSignForSelectedClient() === false) return;

    this.submitting.set(true);
    this.uploadError.set(null);
    try {
      const created = await this.agreements.createAgreement({ ...this.form });

      const file = this.selectedFile();
      if (file) {
        await this.agreements.uploadScannedFile(created.id, file);
      }

      this.showForm.set(false);
      this.selectedFile.set(null);
      this.form = {
        clientId: this.clients.approvedClients()[0]?.id ?? '', agreementPlace: '',
        supportWindowMonths: 12, billingTier: 'Basic',
      };
    } catch (err: any) {
      // 409 = server-side hard-block: training isn't complete for this client.
      if (err?.status === 409) {
        this.uploadError.set(err?.error ?? 'This client has no completed training yet — the agreement cannot be signed.');
      } else {
        this.uploadError.set('The agreement was saved, but a later step failed. You can retry uploads from the agreements list.');
      }
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

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

  async viewTraining(clientId: string) {
    this.viewingTrainingClientId.set(clientId);
    this.uploadError.set(null);
    try {
      const trainings = await this.agreements.getTrainingsForClient(clientId);
      this.trainingRows.set(trainings.map(t => this.toRowState(t)));
    } catch (err) {
      this.uploadError.set('Could not load trainings for this client.');
      console.error(err);
    }
  }

  closeTrainingView() {
    this.viewingTrainingClientId.set(null);
    this.trainingRows.set([]);
    this.uploadError.set(null);
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

  /** Adds a new, empty training row for the client immediately (so it has an id to save/delete against), then refreshes the panel. */
  async addTrainingRow(clientId: string) {
    this.submitting.set(true);
    this.uploadError.set(null);
    try {
      await this.agreements.addTraining(clientId);
      const trainings = await this.agreements.getTrainingsForClient(clientId);
      this.trainingRows.set(trainings.map(t => this.toRowState(t)));
    } catch (err) {
      this.uploadError.set('Could not add a new training row — please try again.');
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  onTrainingFileSelected(evt: Event, row: TrainingRowState) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    row.selectedFile = file ?? null;
    this.uploadError.set(null);
  }

  /** Saves one training row independently of any other — its own Save button, its own request. */
  async saveTrainingRow(clientId: string, row: TrainingRowState) {
    this.submitting.set(true);
    this.uploadError.set(null);
    try {
      await this.agreements.saveTraining(clientId, row.training.id, {
        description: row.description || undefined,
        startDate: row.startDate || undefined,
        endDate: row.endDate || undefined,
      });

      if (row.selectedFile) {
        await this.agreements.uploadTrainingScan(clientId, row.training.id, row.selectedFile);
      }

      const trainings = await this.agreements.getTrainingsForClient(clientId);
      this.trainingRows.set(trainings.map(t => this.toRowState(t)));

      // The New Agreement form's button may now unlock if this client's
      // training just became complete — refresh the check if it's open.
      if (this.form.clientId === clientId) void this.refreshTrainingCheck();
    } catch (err) {
      this.uploadError.set('Could not save this training — please try again.');
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  async deleteTrainingRow(clientId: string, trainingId: string) {
    this.submitting.set(true);
    this.uploadError.set(null);
    try {
      await this.agreements.deleteTraining(clientId, trainingId);
      const trainings = await this.agreements.getTrainingsForClient(clientId);
      this.trainingRows.set(trainings.map(t => this.toRowState(t)));
      if (this.form.clientId === clientId) void this.refreshTrainingCheck();
    } catch (err) {
      this.uploadError.set('Could not delete this training — please try again.');
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  async downloadTrainingScan(clientId: string, trainingId: string) {
    try {
      const blob = await this.agreements.downloadTrainingScan(clientId, trainingId);
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
