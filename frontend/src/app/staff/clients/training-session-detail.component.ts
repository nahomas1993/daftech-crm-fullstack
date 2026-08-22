import { Component, effect, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AgreementService } from '../../core/services/agreement.service';
import { TrainingSession, TrainingAssignment, TrainerWorkload } from '../../core/models';

/**
 * The full training workflow record for one Training-type Agreement —
 * date/timeline, location, participants, attendance, topics, issues,
 * trainer comments, client representative confirmation, completion
 * status, and follow-up. Reachable from the Client detail page (via the
 * "Training Session" link next to a Training agreement), and equally
 * reachable by linking directly to /admin/clients/:clientId/training/:agreementId
 * from a System/Product or Agreement context — all three paths land here.
 *
 * The Trainer Assignments panel lists every TrainingAssignment on this
 * session (auto-assigned by workload when the agreement was created —
 * see Training.TrainersPerSession in Settings — plus any Admin added
 * manually). Each card shows that trainer's own submission (once they've
 * submitted) and lets the Admin approve or reject it; once every
 * assignment is approved, the session itself is marked Completed
 * automatically, which is what unlocks a Support agreement for the same
 * system/product. An extra trainer can still be added by hand below the
 * roster, using the same workload ranking used for auto-assignment.
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
      <p class="status-line">
        Status: <strong>{{ statusLabel(s.completionStatus) }}</strong>
        @if (s.completionStatus === 'Completed') {
          <span class="pill pill-recommend" style="margin-left:0.5rem;">Every trainer approved</span>
        }
      </p>

      <div class="panel panel-pad" style="margin-top:1rem;">
        <h3 style="margin:0 0 0.9rem;">Trainer Assignments</h3>

        @if (s.trainerAssignments.length === 0) {
          <p class="text-muted" style="font-size:0.82rem;">No trainers assigned yet — add one below.</p>
        } @else {
          <div class="assignment-list">
            @for (a of s.trainerAssignments; track a.id) {
              <div class="assignment-card" [class.approved]="a.status === 'Approved'">
                <div class="assignment-head">
                  <span class="trainer-name">{{ a.trainerEmployeeName }}</span>
                  <span class="pill" [class]="statusPillClass(a.status)">{{ statusLabel(a.status) }}</span>
                  @if (a.status !== 'Approved') {
                    <button class="btn btn-outline btn-sm" style="margin-left:auto;" (click)="removeAssignment(a)">Remove</button>
                  }
                </div>

                @if (a.workDescription) {
                  <p class="assignment-desc">{{ a.workDescription }}</p>
                }
                @if (a.fileName) {
                  <button class="btn btn-outline btn-sm" (click)="downloadAssignmentFile(a)">Download: {{ a.fileName }}</button>
                }
                @if (a.reviewNotes) {
                  <p class="review-notes"><strong>Review notes:</strong> {{ a.reviewNotes }} — {{ a.reviewedByName }}</p>
                }

                @if (a.status === 'Submitted') {
                  <div class="review-actions">
                    <button class="btn btn-primary btn-sm" [disabled]="reviewingId() === a.id" (click)="approve(a)">Approve</button>
                    <input type="text" placeholder="Notes (required to reject)" [(ngModel)]="rejectNotes[a.id]" class="reject-input" />
                    <button class="btn btn-outline btn-sm" [disabled]="reviewingId() === a.id" (click)="reject(a)">Reject</button>
                  </div>
                }
                @if (reviewError() && reviewingId() === a.id) {
                  <p class="upload-error" style="margin:0.4rem 0 0;">{{ reviewError() }}</p>
                }
              </div>
            }
          </div>
        }

        <div class="add-trainer" style="margin-top:1rem;">
          @if (workloadLoading()) {
            <p class="text-muted" style="font-size:0.82rem;">Loading trainer workload…</p>
          } @else if (availableTrainers().length === 0) {
            <p class="text-muted" style="font-size:0.82rem;">Every eligible Trainer is already assigned to this session.</p>
          } @else {
            <div class="field" style="max-width:360px;">
              <label>Add another Trainer</label>
              <select [(ngModel)]="selectedNewTrainerId">
                <option value="">Select…</option>
                @for (w of availableTrainers(); track w.employeeId) {
                  <option [value]="w.employeeId">
                    {{ w.employeeName }}{{ w.employeeId === recommendedTrainerId() ? ' (recommended)' : '' }}{{ w.isExcessiveWorkload ? ' — excessive workload' : '' }}
                  </option>
                }
              </select>
              <button class="btn btn-outline btn-sm" style="margin-top:0.5rem;" [disabled]="!selectedNewTrainerId() || addingTrainer()" (click)="addTrainer()">
                {{ addingTrainer() ? 'Adding…' : 'Add Trainer' }}
              </button>
              @if (addTrainerError()) { <p class="upload-error" style="margin-top:0.4rem;">{{ addTrainerError() }}</p> }
            </div>
          }
        </div>
      </div>

      <div class="panel panel-pad" style="margin-top:1rem;">
        <div class="form-grid">
          <div class="field">
            <label>Start Date</label>
            <input type="date" [ngModel]="form.startDate" (ngModelChange)="form.startDate = $event" />
          </div>
          <div class="field">
            <label>End Date</label>
            <input type="date" [ngModel]="form.endDate" (ngModelChange)="form.endDate = $event" [disabled]="s.completionStatus !== 'Completed'" />
            <span class="text-muted" style="font-size:0.72rem;">Set automatically once every trainer is approved — editable afterward if training ran long.</span>
          </div>
          <div class="field">
            <label>Location</label>
            <input type="text" [ngModel]="form.location" (ngModelChange)="form.location = $event" />
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
    .status-line { font-size: 0.85rem; color: var(--slate-600); }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.9rem; margin-top: 0.9rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .assignment-list { display: flex; flex-direction: column; gap: 0.6rem; }
    .assignment-card { padding: 0.7rem 0.85rem; border: 1px solid var(--slate-200); border-radius: 8px; }
    .assignment-card.approved { background: var(--slate-50, #f8fafc); }
    .assignment-head { display: flex; align-items: center; gap: 0.6rem; }
    .trainer-name { font-weight: 600; font-size: 0.88rem; }
    .assignment-desc { font-size: 0.82rem; margin: 0.5rem 0 0; color: var(--slate-700); }
    .review-notes { font-size: 0.78rem; margin: 0.5rem 0 0; color: var(--slate-600); }
    .review-actions { display: flex; align-items: center; gap: 0.5rem; margin-top: 0.6rem; }
    .reject-input { flex: 1; font-size: 0.8rem; padding: 0.3rem 0.5rem; border: 1px solid var(--slate-300); border-radius: 6px; }
    .pill { font-size: 0.68rem; font-weight: 600; padding: 0.15rem 0.5rem; border-radius: 999px; }
    .pill-recommend { background: #dcfce7; color: #166534; }
    .pill-warn { background: #fee2e2; color: #991b1b; }
    .pill-assigned { background: #e0e7ff; color: #3730a3; }
    .pill-submitted { background: #fef9c3; color: #854d0e; }
    .pill-approved { background: #dcfce7; color: #166534; }
    .pill-rejected { background: #fee2e2; color: #991b1b; }
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

  reviewingId = signal<string | null>(null);
  reviewError = signal<string | null>(null);
  rejectNotes: Record<string, string> = {};

  selectedNewTrainerId = signal('');
  addingTrainer = signal(false);
  addTrainerError = signal<string | null>(null);

  form = this.blankForm();

  constructor(private agreementsSvc: AgreementService) {
    effect(() => {
      const agreementId = this.agreementId();
      if (agreementId) void this.load(agreementId);
    });

    void this.loadWorkload();
  }

  /** Trainers not already on this session's roster — the only ones worth showing in "Add another Trainer". */
  availableTrainers(): TrainerWorkload[] {
    const assignedIds = new Set((this.session()?.trainerAssignments ?? []).map(a => a.trainerEmployeeId));
    return this.trainerWorkloads().filter(w => !assignedIds.has(w.employeeId));
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'NotStarted': return 'Not Started';
      case 'InProgress': return 'In Progress';
      case 'Completed': return 'Completed';
      case 'FollowUpRequired': return 'Follow-Up Required';
      case 'Assigned': return 'Assigned';
      case 'Submitted': return 'Submitted for review';
      case 'Approved': return 'Approved';
      case 'RejectedNeedsRework': return 'Needs rework';
      default: return status;
    }
  }

  statusPillClass(status: string): string {
    switch (status) {
      case 'Assigned': return 'pill-assigned';
      case 'Submitted': return 'pill-submitted';
      case 'Approved': return 'pill-approved';
      case 'RejectedNeedsRework': return 'pill-rejected';
      default: return 'pill-assigned';
    }
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
      startDate: '', endDate: '', location: '',
      participants: '', attendance: '', topicsCovered: '', issuesOrQuestions: '', trainerComments: '',
      clientRepresentativeConfirmation: '', clientRepresentativeComments: '',
      followUpRequired: false, followUpNotes: '',
    };
  }

  private async load(agreementId: string) {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const s = await this.agreementsSvc.getTrainingSession(agreementId);
      this.session.set(s);
      this.form = {
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

  async addTrainer() {
    const trainerEmployeeId = this.selectedNewTrainerId();
    if (!trainerEmployeeId) return;

    this.addingTrainer.set(true);
    this.addTrainerError.set(null);
    try {
      const updated = await this.agreementsSvc.addTrainingAssignment(this.agreementId(), trainerEmployeeId);
      this.session.set(updated);
      this.selectedNewTrainerId.set('');
    } catch (err: any) {
      this.addTrainerError.set(err?.error ?? 'Could not add this trainer — please try again.');
      console.error(err);
    } finally {
      this.addingTrainer.set(false);
    }
  }

  async removeAssignment(a: TrainingAssignment) {
    try {
      const updated = await this.agreementsSvc.removeTrainingAssignment(this.agreementId(), a.id);
      this.session.set(updated);
    } catch (err) {
      console.error('Failed to remove training assignment', err);
    }
  }

  async approve(a: TrainingAssignment) {
    this.reviewingId.set(a.id);
    this.reviewError.set(null);
    try {
      const updated = await this.agreementsSvc.reviewTrainingAssignment(a.id, true);
      this.session.set(updated);
    } catch (err: any) {
      this.reviewError.set(err?.error ?? 'Could not approve this assignment — please try again.');
      console.error(err);
    } finally {
      this.reviewingId.set(null);
    }
  }

  async reject(a: TrainingAssignment) {
    const notes = this.rejectNotes[a.id]?.trim();
    if (!notes) {
      this.reviewingId.set(a.id);
      this.reviewError.set('Notes are required when rejecting — the trainer needs to know what to revise.');
      return;
    }

    this.reviewingId.set(a.id);
    this.reviewError.set(null);
    try {
      const updated = await this.agreementsSvc.reviewTrainingAssignment(a.id, false, notes);
      this.session.set(updated);
      delete this.rejectNotes[a.id];
    } catch (err: any) {
      this.reviewError.set(err?.error ?? 'Could not reject this assignment — please try again.');
      console.error(err);
    } finally {
      this.reviewingId.set(null);
    }
  }

  async downloadAssignmentFile(a: TrainingAssignment) {
    try {
      const blob = await this.agreementsSvc.downloadTrainingAssignmentFile(a.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = a.fileName ?? '';
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Failed to download training assignment file', err);
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
