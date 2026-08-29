import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TrainingService } from '../../core/services/training.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { AgreementTypeService } from '../../core/services/agreement-type.service';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { MyTrainingAssignment, TrainingRecord } from '../../core/models';

/**
 * The Trainer's own workspace: pick one of the system/products Admin
 * assigned them to, then work through the admin-configured checklist of
 * training items (e.g. "Attendance" — see AgreementType) one at a time —
 * date, optional start/end time, description, optional file — saving
 * each as it's finished. Once every item the Trainer means to log is
 * saved, "Submit Training" tells Admin the checklist is done. Below that
 * sits a history of every session they've already logged.
 *
 * A Trainer never chooses which client they train — Admin decides that by
 * putting them on a system/product's training roster (see
 * SystemProduct.trainingAssignments). This page therefore shows no client
 * picker at all: it lists only the assignments handed to this trainer
 * (GET /api/training/my-assignments), and auto-selects when there's just
 * one. The server enforces the same roster check on save and on submit.
 *
 * Not every training item has a fixed start/end — some run a couple of
 * hours and finish the same day (record the time-of-day on both), some
 * span several days (different dates), and some have no real duration at
 * all (leave both blank). The "Same day" toggle is a convenience for the
 * common short-session case; the underlying fields are always just two
 * independent optional date/time values.
 */
@Component({
  selector: 'app-my-trainings',
  standalone: true,
  imports: [CommonModule, FormsModule, FilePreviewModalComponent],
  template: `
    <h1>My Trainings</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Log each training item you conducted, then submit once the checklist is done.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem; max-width:640px;">
      <h3 style="margin:0 0 0.9rem;">Add Training</h3>

      @if (loadingAssignments()) {
        <p class="text-muted" style="font-size:0.82rem;">Loading your assignments…</p>
      } @else if (assignments().length === 0) {
        <p class="text-muted" style="font-size:0.82rem;">You haven't been assigned to train any client yet — an Admin adds you to a client's system/product training roster.</p>
      } @else {
        <div class="field">
          <label>Assigned Training</label>
          @if (assignments().length === 1) {
            <p class="assignment-single">{{ assignments()[0].clientName }} — {{ assignments()[0].systemProductName }}</p>
          } @else {
            <select [(ngModel)]="form.systemProductId" (ngModelChange)="onAssignmentChange()">
              <option value="">Select an assigned training…</option>
              @for (a of assignments(); track a.systemProductId) {
                <option [value]="a.systemProductId">{{ a.clientName }} — {{ a.systemProductName }}</option>
              }
            </select>
          }
        </div>

        @if (form.systemProductId) {
          @if (submittedAt()) {
            <p class="submitted-note" style="margin-top:0.7rem;">Submitted to Admin on {{ submittedAt() | slice:0:10 }}. You can still log more items below if needed.</p>
          }

          <div class="field" style="margin-top:0.7rem;">
            <label>Agreement Item</label>
            @if (agreementTypes().length === 0) {
              <p class="text-muted" style="font-size:0.78rem;">No agreement items are configured yet — ask an Admin to add one (e.g. "Attendance") under Agreement Types.</p>
            } @else {
              <select [(ngModel)]="form.agreementTypeId">
                <option value="">Select an item…</option>
                @for (t of agreementTypes(); track t.id) {
                  <option [value]="t.id">{{ t.name }}</option>
                }
              </select>
            }
          </div>

          <div class="field" style="margin-top:0.7rem;">
            <label>Training Date</label>
            <input type="date" [(ngModel)]="form.trainingDate" (ngModelChange)="onDateChange()" />
          </div>

          <div class="field" style="margin-top:0.7rem;">
            <label class="checkbox-label">
              <input type="checkbox" [(ngModel)]="hasTimes" (ngModelChange)="onHasTimesChange()" />
              Record a start/end time (skip for items with no set duration)
            </label>
          </div>

          @if (hasTimes()) {
            <div class="field" style="margin-top:0.5rem;">
              <label class="checkbox-label">
                <input type="checkbox" [(ngModel)]="sameDay" (ngModelChange)="onSameDayChange()" />
                Finishes the same day (short session — just record start/end time)
              </label>
            </div>

            <div class="time-row" style="margin-top:0.5rem;">
              <div class="field">
                <label>Start</label>
                @if (sameDay()) {
                  <input type="time" [(ngModel)]="form.startTime" />
                } @else {
                  <input type="datetime-local" [(ngModel)]="form.startDateTime" />
                }
              </div>
              <div class="field">
                <label>End</label>
                @if (sameDay()) {
                  <input type="time" [(ngModel)]="form.endTime" />
                } @else {
                  <input type="datetime-local" [(ngModel)]="form.endDateTime" />
                }
              </div>
            </div>
          }

          <div class="field" style="margin-top:0.7rem;">
            <label>Description of what was taught/conducted</label>
            <textarea rows="4" [(ngModel)]="form.description" placeholder="What did you cover, who attended, anything worth noting…"></textarea>
          </div>

          <div class="field" style="margin-top:0.7rem;">
            <label>Supporting file (optional)</label>
            <input type="file" accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" (change)="onFileSelected($event)" />
            @if (selectedFile()) { <span class="text-muted" style="font-size:0.75rem;">{{ selectedFile()!.name }}</span> }
          </div>

          @if (submitError()) { <p class="upload-error" style="margin-top:0.7rem;">{{ submitError() }}</p> }

          <div style="margin-top:1rem; display:flex; gap:0.6rem; flex-wrap:wrap;">
            <button class="btn btn-primary" [disabled]="saving()" (click)="saveItem()">
              {{ saving() ? 'Saving…' : 'Save Training Item' }}
            </button>
            <button class="btn btn-outline" [disabled]="submittingAll() || itemsLoggedForSelected() === 0" (click)="submitAll()">
              {{ submittingAll() ? 'Submitting…' : 'Submit Training (' + itemsLoggedForSelected() + ' item' + (itemsLoggedForSelected() === 1 ? '' : 's') + ' logged)' }}
            </button>
          </div>
        }
      }
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <h3 style="margin:0 0 0.9rem;">Your Training History</h3>
      @if (loadingHistory()) {
        <p class="text-muted">Loading…</p>
      } @else if (history().length === 0) {
        <p class="text-muted">No training sessions logged yet.</p>
      } @else {
        <div class="table-scroll"><table>
          <thead><tr><th>Date</th><th>Item</th><th>Client</th><th>System/Product</th><th>Start</th><th>End</th><th>Description</th><th>File</th></tr></thead>
          <tbody>
            @for (r of history(); track r.id) {
              <tr>
                <td>{{ r.trainingDate }}</td>
                <td>{{ r.agreementTypeName }}</td>
                <td>{{ r.clientName }}</td>
                <td>{{ r.systemProductName }}</td>
                <td>{{ r.startDateTime ? (r.startDateTime | slice:0:16 | slice:11:16) : '—' }}</td>
                <td>{{ r.endDateTime ? (r.endDateTime | slice:0:16 | slice:11:16) : '—' }}</td>
                <td>{{ r.description }}</td>
                <td>
                  @if (r.fileName) {
                    <button class="btn btn-outline btn-sm" (click)="viewFile(r)">{{ r.fileName }}</button>
                  } @else { <span class="text-muted">None</span> }
                </td>
              </tr>
            }
          </tbody>
        </table></div>
      }
    </div>

    <app-file-preview-modal
      [open]="previewOpen"
      title="Training Record File"
      [fileName]="previewFileName"
      [kind]="previewKind"
      [load]="previewLoader"
      (closed)="closePreview()">
    </app-file-preview-modal>
  `,
  styles: [`
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .assignment-single { margin: 0; font-weight: 600; }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .submitted-note { font-size: 0.8rem; color: var(--green, #16a34a); font-weight: 600; }
    .checkbox-label { display: flex; flex-direction: row; align-items: center; gap: 0.4rem; font-weight: 500; }
    .time-row { display: flex; gap: 0.75rem; }
    .time-row .field { flex: 1; }
  `],
})
export class MyTrainingsComponent implements OnInit {
  loadingAssignments = signal(true);
  assignments = signal<MyTrainingAssignment[]>([]);
  agreementTypes = computed(() => this.agreementTypeSvc.types());

  hasTimes = signal(false);
  sameDay = signal(true);

  form = {
    systemProductId: '',
    agreementTypeId: '',
    trainingDate: new Date().toISOString().slice(0, 10),
    startTime: '',
    endTime: '',
    startDateTime: '',
    endDateTime: '',
    description: '',
  };
  selectedFile = signal<File | null>(null);
  saving = signal(false);
  submittingAll = signal(false);
  submitError = signal<string | null>(null);

  loadingHistory = signal(true);
  history = signal<TrainingRecord[]>([]);

  // The currently-selected assignment's own SystemProduct row (for its trainingSubmittedAt), refreshed on demand.
  private selectedSystemProduct = signal<{ id: string; trainingSubmittedAt?: string } | null>(null);
  submittedAt = computed(() => this.selectedSystemProduct()?.trainingSubmittedAt);

  constructor(
    private trainingSvc: TrainingService,
    private systemProductSvc: SystemProductService,
    private agreementTypeSvc: AgreementTypeService,
  ) {}

  selectedAssignment = computed(() =>
    this.assignments().find(a => a.systemProductId === this.form.systemProductId) ?? null);

  itemsLoggedForSelected = computed(() =>
    this.history().filter(r => r.systemProductId === this.form.systemProductId).length);

  async ngOnInit() {
    await Promise.all([this.loadAssignments(), this.loadHistory(), this.agreementTypeSvc.refresh()]);
  }

  private async loadAssignments() {
    this.loadingAssignments.set(true);
    try {
      const list = await this.trainingSvc.getMyAssignments();
      this.assignments.set(list);
      // Nothing to choose when Admin assigned exactly one — just use it.
      if (list.length === 1) {
        this.form.systemProductId = list[0].systemProductId;
        await this.refreshSelectedSystemProduct();
      }
    } catch (err) {
      console.error('Failed to load training assignments', err);
      this.assignments.set([]);
    } finally {
      this.loadingAssignments.set(false);
    }
  }

  async onAssignmentChange() {
    this.submitError.set(null);
    this.selectedFile.set(null);
    await this.refreshSelectedSystemProduct();
  }

  private async refreshSelectedSystemProduct() {
    if (!this.form.systemProductId) { this.selectedSystemProduct.set(null); return; }
    try {
      const sp = await this.systemProductSvc.getById(this.form.systemProductId);
      this.selectedSystemProduct.set({ id: sp.id, trainingSubmittedAt: sp.trainingSubmittedAt });
    } catch (err) {
      console.error('Failed to load system/product', err);
    }
  }

  onDateChange() {
    // Same-day time inputs assume the training date; nothing else to sync here — kept for clarity/extension.
  }

  onHasTimesChange() {
    if (!this.hasTimes()) {
      this.form.startTime = this.form.endTime = this.form.startDateTime = this.form.endDateTime = '';
    }
  }

  onSameDayChange() {
    this.form.startTime = this.form.endTime = this.form.startDateTime = this.form.endDateTime = '';
  }

  onFileSelected(evt: Event) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    this.selectedFile.set(file ?? null);
  }

  private async loadHistory() {
    this.loadingHistory.set(true);
    try {
      this.history.set(await this.trainingSvc.getMyRecords());
    } catch (err) {
      console.error('Failed to load training history', err);
    } finally {
      this.loadingHistory.set(false);
    }
  }

  /** Builds ISO start/end datetimes from the same-day time fields or the multi-day datetime-local fields, or undefined if hasTimes is off. */
  private resolveStartEnd(): { start?: string; end?: string } {
    if (!this.hasTimes()) return {};
    if (this.sameDay()) {
      const date = this.form.trainingDate;
      return {
        start: this.form.startTime ? `${date}T${this.form.startTime}` : undefined,
        end: this.form.endTime ? `${date}T${this.form.endTime}` : undefined,
      };
    }
    return {
      start: this.form.startDateTime || undefined,
      end: this.form.endDateTime || undefined,
    };
  }

  /** Saves the currently-filled-in item as one TrainingRecord, then clears the description/time/file fields so the Trainer can move on to the next agreement item for the same assignment. */
  async saveItem() {
    if (!this.form.systemProductId || !this.form.agreementTypeId || !this.form.description.trim()) {
      this.submitError.set('Select an assigned training, an agreement item, and describe what was conducted.');
      return;
    }

    const { start, end } = this.resolveStartEnd();
    if (start && end && end < start) {
      this.submitError.set('End time cannot be before the start time.');
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);
    try {
      const created = await this.trainingSvc.create({
        systemProductId: this.form.systemProductId,
        agreementTypeId: this.form.agreementTypeId,
        trainingDate: this.form.trainingDate,
        startDateTime: start,
        endDateTime: end,
        description: this.form.description.trim(),
      });

      const file = this.selectedFile();
      if (file) {
        await this.trainingSvc.uploadFile(created.id, file);
      }

      // Keep the assignment selected; clear the rest so the Trainer moves straight to the next item.
      this.form.agreementTypeId = '';
      this.form.description = '';
      this.form.startTime = this.form.endTime = this.form.startDateTime = this.form.endDateTime = '';
      this.selectedFile.set(null);
      await this.loadHistory();
    } catch (err: any) {
      this.submitError.set(typeof err?.error === 'string' ? err.error : (err?.error?.detail ?? err?.message ?? 'Could not save — please try again.'));
      console.error(err);
    } finally {
      this.saving.set(false);
    }
  }

  /** Once every agreement item the Trainer means to log is saved, tells Admin the checklist is done for this system/product. */
  async submitAll() {
    if (!this.form.systemProductId) return;

    this.submittingAll.set(true);
    this.submitError.set(null);
    try {
      const updated = await this.systemProductSvc.submitTraining(this.form.systemProductId);
      this.selectedSystemProduct.set({ id: updated.id, trainingSubmittedAt: updated.trainingSubmittedAt });
    } catch (err: any) {
      this.submitError.set(typeof err?.error === 'string' ? err.error : (err?.error?.detail ?? err?.message ?? 'Could not submit — please try again.'));
      console.error(err);
    } finally {
      this.submittingAll.set(false);
    }
  }

  // Shown inline in the shared preview modal (audio player / image / PDF
  // viewer) rather than forcing a download — see FilePreviewModalComponent.
  previewOpen = false;
  previewFileName = '';
  previewKind: FilePreviewKind = 'other';
  previewLoader?: () => Promise<Blob>;

  viewFile(r: TrainingRecord) {
    this.previewFileName = r.fileName ?? '';
    this.previewKind = filePreviewKindFor(r.fileName);
    this.previewLoader = () => this.trainingSvc.downloadFile(r.id);
    this.previewOpen = true;
  }

  closePreview() {
    this.previewOpen = false;
  }
}
