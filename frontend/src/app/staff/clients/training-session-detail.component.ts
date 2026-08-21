import { Component, effect, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AgreementService } from '../../core/services/agreement.service';
import { TrainingSession, TrainingCompletionStatus, TrainerWorkload } from '../../core/models';

/**
 * The full training workflow record for one Training-type Agreement —
 * date/timeline, location, participants, attendance, topics, issues,
 * trainer comments, client representative confirmation, completion
 * status, and follow-up. Reachable from the Client detail page (via the
 * "Training Session" link next to a Training agreement), and equally
 * reachable by linking directly to /admin/clients/:clientId/training/:agreementId
 * from a System/Product or Agreement context — all three paths land here.
 *
 * The Trainer Assignment panel shows every employee with the Trainer
 * responsibility alongside their current workload (active/pending/high-
 * priority/overdue tickets, active training assignments) and recommends
 * the least-loaded eligible Trainer — see ITrainerWorkloadService on the
 * backend. Admin can still select anyone regardless of the recommendation
 * or an excessive-workload warning; the server also validates the chosen
 * TrainerEmployeeId actually holds the Trainer responsibility on save.
 */
@Component({
  selector: 'app-training-session-detail',
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
    <a [routerLink]="['/admin/clients', clientId()]" class="back">← Back to Client</a>

    @if (session(); as s) {
      <h1>Training Session</h1>
      <p class="text-muted mono" style="margin-top:0.3rem; font-size:0.8rem;">Agreement {{ agreementId() }}</p>

      <div class="panel panel-pad" style="margin-top:1rem;">
        <h3 style="margin:0 0 0.9rem;">Trainer Assignment</h3>
        <p class="text-muted" style="font-size:0.78rem; margin: -0.5rem 0 0.9rem;">
          Workload shown per Trainer — active/pending/high-priority/overdue tickets and current training assignments. The recommendation is a suggestion; you can assign anyone regardless.
        </p>

        @if (workloadLoading()) {
          <p class="text-muted" style="font-size:0.82rem;">Loading trainer workload…</p>
        } @else if (trainerWorkloads().length === 0) {
          <p class="text-muted" style="font-size:0.82rem;">No employees currently have the Trainer responsibility assigned.</p>
        } @else {
          <div class="trainer-list">
            @for (w of trainerWorkloads(); track w.employeeId) {
              <label class="trainer-row" [class.selected]="form.trainerEmployeeId === w.employeeId">
                <input type="radio" name="trainer" [value]="w.employeeId"
                       [checked]="form.trainerEmployeeId === w.employeeId"
                       (change)="form.trainerEmployeeId = w.employeeId" />
                <div class="trainer-info">
                  <div class="trainer-name-row">
                    <span class="trainer-name">{{ w.employeeName }}</span>
                    @if (w.employeeId === recommendedTrainerId()) {
                      <span class="pill pill-recommend">Recommended</span>
                    }
                    @if (w.isExcessiveWorkload) {
                      <span class="pill pill-warn">Excessive workload</span>
                    }
                  </div>
                  <div class="trainer-stats text-muted">
                    {{ w.activeTicketCount }} active · {{ w.pendingTicketCount }} pending ·
                    {{ w.highPriorityTicketCount }} high-priority · {{ w.overdueTicketCount }} overdue ·
                    {{ w.activeTrainingAssignmentCount }} active training(s)
                  </div>
                </div>
              </label>
            }
            <label class="trainer-row" [class.selected]="!form.trainerEmployeeId">
              <input type="radio" name="trainer" value="" [checked]="!form.trainerEmployeeId" (change)="form.trainerEmployeeId = ''" />
              <div class="trainer-info"><span class="trainer-name text-muted">Unassigned</span></div>
            </label>
          </div>
        }
      </div>

      <div class="panel panel-pad" style="margin-top:1rem;">
        <div class="form-grid">
          <div class="field">
            <label>Start Date</label>
            <input type="date" [ngModel]="form.startDate" (ngModelChange)="form.startDate = $event" />
          </div>
          <div class="field">
            <label>End Date</label>
            <input type="date" [ngModel]="form.endDate" (ngModelChange)="form.endDate = $event" />
            <span class="text-muted" style="font-size:0.72rem;">Setting this is what unlocks a Support agreement for this system/product.</span>
          </div>
          <div class="field">
            <label>Location</label>
            <input type="text" [ngModel]="form.location" (ngModelChange)="form.location = $event" />
          </div>
          <div class="field">
            <label>Completion Status</label>
            <select [ngModel]="form.completionStatus" (ngModelChange)="form.completionStatus = $event">
              <option value="NotStarted">Not Started</option>
              <option value="InProgress">In Progress</option>
              <option value="Completed">Completed</option>
              <option value="FollowUpRequired">Follow-Up Required</option>
            </select>
          </div>
          <div class="field">
            <label>Follow-Up Required</label>
            <select [ngModel]="form.followUpRequired" (ngModelChange)="form.followUpRequired = $event">
              <option [ngValue]="false">No</option>
              <option [ngValue]="true">Yes</option>
            </select>
          </div>
          <div class="field" style="grid-column: 1 / -1;">
            <label>Participants</label>
            <textarea rows="2" [ngModel]="form.participants" (ngModelChange)="form.participants = $event" placeholder="Who was invited/expected"></textarea>
          </div>
          <div class="field" style="grid-column: 1 / -1;">
            <label>Attendance</label>
            <textarea rows="2" [ngModel]="form.attendance" (ngModelChange)="form.attendance = $event" placeholder="Who actually attended"></textarea>
          </div>
          <div class="field" style="grid-column: 1 / -1;">
            <label>Topics Covered</label>
            <textarea rows="3" [ngModel]="form.topicsCovered" (ngModelChange)="form.topicsCovered = $event"></textarea>
          </div>
          <div class="field" style="grid-column: 1 / -1;">
            <label>Issues / Questions Raised</label>
            <textarea rows="3" [ngModel]="form.issuesOrQuestions" (ngModelChange)="form.issuesOrQuestions = $event"></textarea>
          </div>
          <div class="field" style="grid-column: 1 / -1;">
            <label>Trainer Comments</label>
            <textarea rows="3" [ngModel]="form.trainerComments" (ngModelChange)="form.trainerComments = $event"></textarea>
          </div>
          <div class="field">
            <label>Client Representative Confirmation</label>
            <input type="text" [ngModel]="form.clientRepresentativeConfirmation" (ngModelChange)="form.clientRepresentativeConfirmation = $event" placeholder="Name / role confirming" />
          </div>
          <div class="field" style="grid-column: 1 / -1;">
            <label>Client Representative Comments</label>
            <textarea rows="2" [ngModel]="form.clientRepresentativeComments" (ngModelChange)="form.clientRepresentativeComments = $event"></textarea>
          </div>
          @if (form.followUpRequired) {
            <div class="field" style="grid-column: 1 / -1;">
              <label>Follow-Up Notes</label>
              <textarea rows="2" [ngModel]="form.followUpNotes" (ngModelChange)="form.followUpNotes = $event"></textarea>
            </div>
          }
          <div class="field">
            <label>Scan / Sign-In Sheet</label>
            <input type="file" accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" (change)="onScanSelected($event)" />
            @if (selectedScan()) {
              <span class="text-muted" style="font-size:0.75rem;">{{ selectedScan()!.name }}</span>
            } @else if (s.scanFileName) {
              <span class="text-muted" style="font-size:0.75rem;">
                Current: {{ s.scanFileName }}
                <button class="btn btn-outline btn-sm" style="margin-left:0.5rem;" (click)="downloadScan()">Download</button>
              </span>
            }
          </div>
        </div>

        @if (saveError()) { <p class="upload-error" style="margin-top:0.9rem;">{{ saveError() }}</p> }
        <button class="btn btn-primary" style="margin-top:1.1rem;" [disabled]="saving()" (click)="save()">
          {{ saving() ? 'Saving…' : 'Save Training Session' }}
        </button>
      </div>
    } @else if (loadError()) {
      <p class="upload-error">{{ loadError() }}</p>
    } @else {
      <p class="text-muted">Loading training session…</p>
    }
  `,
  styles: [`
    .back { display: inline-block; margin-bottom: 1rem; font-size: 0.82rem; color: var(--slate-500); }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.9rem; margin-top: 0.9rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .trainer-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .trainer-row { display: flex; align-items: center; gap: 0.7rem; padding: 0.6rem 0.75rem; border: 1px solid var(--slate-200); border-radius: 8px; cursor: pointer; }
    .trainer-row.selected { border-color: var(--accent, #1d4ed8); background: var(--slate-50, #f8fafc); }
    .trainer-info { display: flex; flex-direction: column; gap: 0.2rem; }
    .trainer-name-row { display: flex; align-items: center; gap: 0.5rem; }
    .trainer-name { font-weight: 600; font-size: 0.88rem; }
    .trainer-stats { font-size: 0.75rem; }
    .pill { font-size: 0.68rem; font-weight: 600; padding: 0.15rem 0.5rem; border-radius: 999px; }
    .pill-recommend { background: #dcfce7; color: #166534; }
    .pill-warn { background: #fee2e2; color: #991b1b; }
  `],
})
export class TrainingSessionDetailComponent {
  clientId = input.required<string>();
  agreementId = input.required<string>();

  trainerWorkloads = signal<TrainerWorkload[]>([]);
  recommendedTrainerId = signal<string | undefined>(undefined);
  workloadLoading = signal(true);

  loading = signal(true);
  loadError = signal<string | null>(null);
  session = signal<TrainingSession | null>(null);

  saving = signal(false);
  saveError = signal<string | null>(null);
  selectedScan = signal<File | null>(null);

  form = this.blankForm();

  constructor(private agreementsSvc: AgreementService) {
    effect(() => {
      const agreementId = this.agreementId();
      if (agreementId) void this.load(agreementId);
    });

    void this.loadWorkload();
  }

  private async loadWorkload() {
    this.workloadLoading.set(true);
    try {
      const rec = await this.agreementsSvc.getTrainerWorkload();
      this.trainerWorkloads.set(rec.eligibleTrainers);
      this.recommendedTrainerId.set(rec.recommendedTrainerEmployeeId);
    } catch (err) {
      console.error('Failed to load trainer workload', err);
    } finally {
      this.workloadLoading.set(false);
    }
  }

  private blankForm() {
    return {
      trainerEmployeeId: '', startDate: '', endDate: '', location: '',
      participants: '', attendance: '', topicsCovered: '', issuesOrQuestions: '', trainerComments: '',
      clientRepresentativeConfirmation: '', clientRepresentativeComments: '',
      completionStatus: 'NotStarted' as TrainingCompletionStatus, followUpRequired: false, followUpNotes: '',
    };
  }

  private async load(agreementId: string) {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const s = await this.agreementsSvc.getTrainingSession(agreementId);
      this.session.set(s);
      this.form = {
        trainerEmployeeId: s.trainerEmployeeId ?? '',
        startDate: s.startDate?.slice(0, 10) ?? '',
        endDate: s.endDate?.slice(0, 10) ?? '',
        location: s.location ?? '',
        participants: s.participants ?? '',
        attendance: s.attendance ?? '',
        topicsCovered: s.topicsCovered ?? '',
        issuesOrQuestions: s.issuesOrQuestions ?? '',
        trainerComments: s.trainerComments ?? '',
        clientRepresentativeConfirmation: s.clientRepresentativeConfirmation ?? '',
        clientRepresentativeComments: s.clientRepresentativeComments ?? '',
        completionStatus: s.completionStatus,
        followUpRequired: s.followUpRequired,
        followUpNotes: s.followUpNotes ?? '',
      };
    } catch (err) {
      this.loadError.set('This agreement has no training session — it may not be a Training-type agreement.');
      console.error(err);
    } finally {
      this.loading.set(false);
    }
  }

  onScanSelected(evt: Event) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    this.selectedScan.set(file ?? null);
  }

  async save() {
    this.saving.set(true);
    this.saveError.set(null);
    try {
      const updated = await this.agreementsSvc.saveTrainingSession(this.agreementId(), {
        trainerEmployeeId: this.form.trainerEmployeeId || undefined,
        startDate: this.form.startDate || undefined,
        endDate: this.form.endDate || undefined,
        location: this.form.location || undefined,
        participants: this.form.participants || undefined,
        attendance: this.form.attendance || undefined,
        topicsCovered: this.form.topicsCovered || undefined,
        issuesOrQuestions: this.form.issuesOrQuestions || undefined,
        trainerComments: this.form.trainerComments || undefined,
        clientRepresentativeConfirmation: this.form.clientRepresentativeConfirmation || undefined,
        clientRepresentativeComments: this.form.clientRepresentativeComments || undefined,
        completionStatus: this.form.completionStatus,
        followUpRequired: this.form.followUpRequired,
        followUpNotes: this.form.followUpNotes || undefined,
      });
      this.session.set(updated);

      const scan = this.selectedScan();
      if (scan) {
        const withScan = await this.agreementsSvc.uploadTrainingScan(this.agreementId(), scan);
        this.session.set(withScan);
        this.selectedScan.set(null);
      }
    } catch (err: any) {
      this.saveError.set(err?.error ?? 'Could not save the training session — please try again.');
      console.error(err);
    } finally {
      this.saving.set(false);
    }
  }

  async downloadScan() {
    try {
      const blob = await this.agreementsSvc.downloadTrainingScan(this.agreementId());
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
