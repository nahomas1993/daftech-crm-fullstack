import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { ClientService } from '../../core/services/client.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { TrainingService } from '../../core/services/training.service';
import { SystemProduct, TrainingRecord } from '../../core/models';

/**
 * The Trainer's own workspace: "Add Training" (client -> their
 * system/product -> date/description/optional file -> submit), plus a
 * history of every session they've already logged (see TrainingRecord).
 * There is no submit/approve lifecycle per record — each "Add Training"
 * click inserts a new, independent record; a client's system/product can
 * accumulate any number of these over time, even after Admin has already
 * marked its training Completed (a refresher). Only route reachable by
 * an employee holding the Trainer responsibility (see
 * staff-shell.component.ts's 'Trainer'-gated nav item).
 *
 * Only shows system/products the logged-in Trainer is actually on the
 * training roster for (see SystemProduct.trainingAssignments) — the
 * server enforces this too (see TrainingController.Create), but filtering
 * here keeps the client picker from listing clients/system-products this
 * trainer has no reason to touch.
 */
@Component({
  selector: 'app-my-trainings',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h1>My Trainings</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Log a training session you conducted, or review what you've already logged.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem; max-width:640px;">
      <h3 style="margin:0 0 0.9rem;">Add Training</h3>

      <div class="field">
        <label>Client</label>
        <select [(ngModel)]="selectedClientId" (ngModelChange)="onClientChange($event)">
          <option value="">Select a client…</option>
          @for (c of clientsSvc.clients(); track c.id) {
            <option [value]="c.id">{{ c.name }}</option>
          }
        </select>
      </div>

      @if (selectedClientId()) {
        <div class="field" style="margin-top:0.7rem;">
          <label>System / Product</label>
          @if (loadingSystemProducts()) {
            <p class="text-muted" style="font-size:0.82rem;">Loading…</p>
          } @else if (assignedSystemProducts().length === 0) {
            <p class="text-muted" style="font-size:0.82rem;">You aren't assigned to train this client on any system/product yet — ask an Admin to add you to the training roster first.</p>
          } @else {
            <select [(ngModel)]="form.systemProductId">
              <option value="">Select a system/product…</option>
              @for (sp of assignedSystemProducts(); track sp.id) {
                <option [value]="sp.id">{{ sp.name }}</option>
              }
            </select>
          }
        </div>
      }

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
    .upload-error { color: var(--red); font-size: 0.85rem; }
  `],
})
export class MyTrainingsComponent implements OnInit {
  selectedClientId = signal('');
  loadingSystemProducts = signal(false);
  private clientSystemProducts = signal<SystemProduct[]>([]);

  form = { systemProductId: '', trainingDate: new Date().toISOString().slice(0, 10), description: '' };
  selectedFile = signal<File | null>(null);
  submitting = signal(false);
  submitError = signal<string | null>(null);

  loadingHistory = signal(true);
  history = signal<TrainingRecord[]>([]);

  constructor(
    private auth: AuthService,
    public clientsSvc: ClientService,
    private systemProductsSvc: SystemProductService,
    private trainingSvc: TrainingService,
  ) {}

  async ngOnInit() {
    await this.loadHistory();
  }

  /** This trainer's own system/products for the selected client — the server enforces the same roster check, this just avoids offering a system/product the submit would be rejected for. */
  assignedSystemProducts = computed(() => {
    const myId = this.auth.currentEmployee()?.id;
    if (!myId) return [];
    return this.clientSystemProducts().filter(sp => sp.trainingAssignments.some(a => a.trainerEmployeeId === myId));
  });

  async onClientChange(clientId: string) {
    this.form = { systemProductId: '', trainingDate: new Date().toISOString().slice(0, 10), description: '' };
    this.selectedFile.set(null);
    this.submitError.set(null);
    if (!clientId) {
      this.clientSystemProducts.set([]);
      return;
    }

    this.loadingSystemProducts.set(true);
    try {
      const list = await this.systemProductsSvc.refreshForClient(clientId);
      this.clientSystemProducts.set(list);
    } catch (err) {
      console.error('Failed to load system/products for client', err);
      this.clientSystemProducts.set([]);
    } finally {
      this.loadingSystemProducts.set(false);
    }
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

  /** Always inserts a new TrainingRecord — repeat "Add Training" for as many sessions as needed against the same client/system-product. */
  async submit() {
    if (!this.form.systemProductId || !this.form.description.trim()) {
      this.submitError.set('System/Product and a description are required.');
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
      this.submitError.set(err?.error ?? 'Could not submit — please try again.');
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
