import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { AgreementService } from '../../core/services/agreement.service';
import { TrainingAssignment } from '../../core/models';

/**
 * The logged-in Trainer's own view of every TrainingAssignment they hold
 * (see TrainingAssignment) — the counterpart to the Admin-only Trainer
 * Assignments panel on TrainingSessionDetailComponent. A Trainer lands
 * here (see staff-shell.component.ts's 'Trainer'-gated nav item) to see
 * what they've been assigned, write up the work they completed, attach a
 * file as evidence, and submit it for Admin review. Only route reachable
 * by an employee holding just the Trainer responsibility with no other
 * role — everything else under /admin is Admin-only or open to any
 * employee, so this is the one dedicated "my work" page for Trainers.
 */
@Component({
  selector: 'app-my-trainings',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h1>My Trainings</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Training sessions assigned to you — write up your work and submit it for review.</p>

    @if (loading()) {
      <p class="text-muted" style="margin-top:1rem;">Loading your trainings…</p>
    } @else if (loadError()) {
      <p class="upload-error" style="margin-top:1rem;">{{ loadError() }}</p>
    } @else if (assignments().length === 0) {
      <p class="text-muted" style="margin-top:1rem;">No trainings are currently assigned to you.</p>
    } @else {
      <div class="assignment-list" style="margin-top:1.25rem;">
        @for (a of assignments(); track a.id) {
          <div class="panel panel-pad assignment-card">
            <div class="assignment-head">
              <span class="pill" [class]="statusPillClass(a.status)">{{ statusLabel(a.status) }}</span>
              <span class="text-muted assigned-at">Assigned {{ a.assignedAt | date:'medium' }}</span>
            </div>

            @if (a.reviewNotes) {
              <p class="review-notes"><strong>Admin feedback:</strong> {{ a.reviewNotes }}</p>
            }

            @if (a.status === 'Approved') {
              <p class="assignment-desc">{{ a.workDescription }}</p>
              @if (a.fileName) {
                <button class="btn btn-outline btn-sm" (click)="downloadFile(a)">Download: {{ a.fileName }}</button>
              }
            } @else {
              <div class="field" style="margin-top:0.7rem;">
                <label>Description of completed work</label>
                <textarea rows="4" [ngModel]="draftDescription(a.id)" (ngModelChange)="setDraftDescription(a.id, $event)" placeholder="What did you cover, who attended, anything the Admin should know…"></textarea>
              </div>

              <div class="field" style="margin-top:0.6rem;">
                <label>Evidence file (optional)</label>
                <input type="file" accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" (change)="onFileSelected(a.id, $event)" />
                @if (selectedFiles()[a.id]) {
                  <span class="text-muted" style="font-size:0.75rem;">{{ selectedFiles()[a.id]!.name }}</span>
                } @else if (a.fileName) {
                  <span class="text-muted" style="font-size:0.75rem;">
                    Current: {{ a.fileName }}
                    <button class="btn btn-outline btn-sm" style="margin-left:0.5rem;" (click)="downloadFile(a)">Download</button>
                  </span>
                }
              </div>

              @if (submitError()[a.id]) { <p class="upload-error" style="margin-top:0.6rem;">{{ submitError()[a.id] }}</p> }
              <button class="btn btn-primary" style="margin-top:0.9rem;" [disabled]="submitting() === a.id" (click)="submit(a)">
                {{ submitting() === a.id ? 'Submitting…' : 'Submit for Review' }}
              </button>
            }
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .assignment-list { display: flex; flex-direction: column; gap: 1rem; max-width: 720px; }
    .assignment-card { }
    .assignment-head { display: flex; align-items: center; gap: 0.7rem; }
    .assigned-at { font-size: 0.78rem; }
    .assignment-desc { font-size: 0.85rem; margin: 0.6rem 0 0; color: var(--slate-700); }
    .review-notes { font-size: 0.8rem; margin: 0.6rem 0 0; color: var(--slate-600); }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
    .pill { font-size: 0.68rem; font-weight: 600; padding: 0.15rem 0.5rem; border-radius: 999px; }
    .pill-assigned { background: #e0e7ff; color: #3730a3; }
    .pill-submitted { background: #fef9c3; color: #854d0e; }
    .pill-approved { background: #dcfce7; color: #166534; }
    .pill-rejected { background: #fee2e2; color: #991b1b; }
  `],
})
export class MyTrainingsComponent implements OnInit {
  loading = signal(true);
  loadError = signal<string | null>(null);
  assignments = signal<TrainingAssignment[]>([]);

  private draftDescriptions = signal<Record<string, string>>({});
  selectedFiles = signal<Record<string, File | undefined>>({});
  submitting = signal<string | null>(null);
  submitError = signal<Record<string, string>>({});

  constructor(private agreementsSvc: AgreementService) {}

  async ngOnInit() {
    await this.load();
  }

  private async load() {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const list = await this.agreementsSvc.getMyTrainingAssignments();
      this.assignments.set(list);
      // Seed the draft textarea with whatever was last saved (e.g. after a
      // rejection, so the trainer can revise it rather than starting blank).
      const drafts: Record<string, string> = {};
      for (const a of list) drafts[a.id] = a.workDescription ?? '';
      this.draftDescriptions.set(drafts);
    } catch (err) {
      this.loadError.set('Could not load your trainings — please try again.');
      console.error(err);
    } finally {
      this.loading.set(false);
    }
  }

  draftDescription(assignmentId: string): string {
    return this.draftDescriptions()[assignmentId] ?? '';
  }

  setDraftDescription(assignmentId: string, value: string) {
    this.draftDescriptions.update(m => ({ ...m, [assignmentId]: value }));
  }

  onFileSelected(assignmentId: string, evt: Event) {
    const file = (evt.target as HTMLInputElement).files?.[0];
    this.selectedFiles.update(m => ({ ...m, [assignmentId]: file ?? undefined }));
  }

  statusLabel(status: string): string {
    switch (status) {
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

  async submit(a: TrainingAssignment) {
    const description = (this.draftDescriptions()[a.id] ?? '').trim();
    if (!description) {
      this.submitError.update(m => ({ ...m, [a.id]: 'A description of the completed work is required before submitting.' }));
      return;
    }

    this.submitting.set(a.id);
    this.submitError.update(m => ({ ...m, [a.id]: '' }));
    try {
      const file = this.selectedFiles()[a.id];
      if (file) {
        await this.agreementsSvc.uploadTrainingAssignmentFile(a.id, file);
      }
      const updated = await this.agreementsSvc.submitTrainingAssignment(a.id, description);
      this.assignments.update(list => list.map(x => (x.id === updated.id ? updated : x)));
      this.selectedFiles.update(m => ({ ...m, [a.id]: undefined }));
    } catch (err: any) {
      this.submitError.update(m => ({ ...m, [a.id]: err?.error ?? 'Could not submit — please try again.' }));
      console.error(err);
    } finally {
      this.submitting.set(null);
    }
  }

  async downloadFile(a: TrainingAssignment) {
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
}
