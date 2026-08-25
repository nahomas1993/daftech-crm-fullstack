import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TrainingService } from '../../core/services/training.service';
import { MyTrainingAssignment, TrainingRecord } from '../../core/models';

/**
 * The Trainer's own workspace: "Add Training" (pick one of the
 * system/products Admin assigned them to -> date/description/optional
 * file -> submit), plus a history of every session they've already
 * logged (see TrainingRecord).
 *
 * A Trainer never chooses which client they train — Admin decides that by
 * putting them on a system/product's training roster (see
 * SystemProduct.trainingAssignments). This page therefore shows no client
 * picker at all: it lists only the assignments handed to this trainer
 * (GET /api/training/my-assignments), and auto-selects when there's just
 * one. The server enforces the same roster check on submit.
 */
@Component({
  selector: 'app-my-trainings',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h1>My Trainings</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Log a training session you conducted, or review what you've already logged.</p>

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
          <div class="field" style="margin-top:0.7rem;">
            <label>Training Date</label>
            <input type="date" [(ngModel)]="form.trainingDate" />
          </div>

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
          <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="submitting()" (click)="submit()">
            {{ submitting() ? 'Submitting…' : 'Submit Training' }}
          </button>
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
          <thead><tr><th>Date</th><th>Client</th><th>System/Product</th><th>Description</th><th>File</th></tr></thead>
          <tbody>
            @for (r of history(); track r.id) {
              <tr>
                <td>{{ r.trainingDate }}</td>
                <td>{{ r.clientName }}</td>
                <td>{{ r.systemProductName }}</td>
                <td>{{ r.description }}</td>
                <td>
                  @if (r.fileName) {
                    <button class="btn btn-outline btn-sm" (click)="downloadFile(r)">{{ r.fileName }}</button>
                  } @else { <span class="text-muted">None</span> }
                </td>
              </tr>
            }
          </tbody>
        </table></div>
      }
    </div>
  `,
  styles: [`
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .assignment-single { margin: 0; font-weight: 600; }
    .upload-error { color: var(--red); font-size: 0.85rem; }
  `],
})
export class MyTrainingsComponent implements OnInit {
  loadingAssignments = signal(true);
  assignments = signal<MyTrainingAssignment[]>([]);

  form = { systemProductId: '', trainingDate: new Date().toISOString().slice(0, 10), description: '' };
  selectedFile = signal<File | null>(null);
  submitting = signal(false);
  submitError = signal<string | null>(null);

  loadingHistory = signal(true);
  history = signal<TrainingRecord[]>([]);

  constructor(private trainingSvc: TrainingService) {}

  selectedAssignment = computed(() =>
    this.assignments().find(a => a.systemProductId === this.form.systemProductId) ?? null);

  async ngOnInit() {
    await Promise.all([this.loadAssignments(), this.loadHistory()]);
  }

  private async loadAssignments() {
    this.loadingAssignments.set(true);
    try {
      const list = await this.trainingSvc.getMyAssignments();
      this.assignments.set(list);
      // Nothing to choose when Admin assigned exactly one — just use it.
      if (list.length === 1) this.form.systemProductId = list[0].systemProductId;
    } catch (err) {
      console.error('Failed to load training assignments', err);
      this.assignments.set([]);
    } finally {
      this.loadingAssignments.set(false);
    }
  }

  onAssignmentChange() {
    this.submitError.set(null);
    this.selectedFile.set(null);
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

  /** Always inserts a new TrainingRecord — repeat "Add Training" for as many sessions as needed against the same assignment. */
  async submit() {
    if (!this.form.systemProductId || !this.form.description.trim()) {
      this.submitError.set('Select an assigned training and describe what was conducted.');
      return;
    }

    this.submitting.set(true);
    this.submitError.set(null);
    try {
      const created = await this.trainingSvc.create({
        systemProductId: this.form.systemProductId,
        trainingDate: this.form.trainingDate,
        description: this.form.description.trim(),
      });

      const file = this.selectedFile();
      if (file) {
        await this.trainingSvc.uploadFile(created.id, file);
      }

      this.form = { systemProductId: this.form.systemProductId, trainingDate: new Date().toISOString().slice(0, 10), description: '' };
      this.selectedFile.set(null);
      await this.loadHistory();
    } catch (err: any) {
      this.submitError.set(typeof err?.error === 'string' ? err.error : (err?.error?.detail ?? err?.message ?? 'Could not submit — please try again.'));
      console.error(err);
    } finally {
      this.submitting.set(false);
    }
  }

  async downloadFile(r: TrainingRecord) {
    try {
      const blob = await this.trainingSvc.downloadFile(r.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = r.fileName ?? '';
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Failed to download training record file', err);
    }
  }
}
