import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { AgreementService } from '../../core/services/agreement.service';
import { TicketService } from '../../core/services/ticket.service';
import { FailureTypeService } from '../../core/services/failure-type.service';
import { TicketCategory } from '../../core/models';

@Component({
  selector: 'app-submit-issue',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <h1>Submit an Issue</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Describe the problem — our team will review and follow up.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem; max-width:520px;">
      @if (!agreement()) {
        <p class="text-muted">No active agreement found on your account — please contact DAFTECH directly.</p>
      } @else if (!submittedId()) {
        <div class="field">
          <label>Category</label>
          <select [ngModel]="category()" (ngModelChange)="category.set($event)">
            <option value="SqlDatabaseError">SQL/Database error</option>
            <option value="Bug">Bug</option>
            <option value="Other">Other</option>
          </select>
        </div>
        @if (failureTypes.types().length > 0) {
          <div class="field" style="margin-top:0.8rem;">
            <label>What kind of failure is this? (optional)</label>
            <select [ngModel]="failureTypeId()" (ngModelChange)="failureTypeId.set($event)">
              <option value="">Not sure / other…</option>
              @for (f of failureTypes.types(); track f.id) {
                <option [value]="f.id">{{ f.name }}</option>
              }
            </select>
          </div>
        }
        <div class="field" style="margin-top:0.8rem;">
          <label>Description</label>
          <textarea rows="5" maxlength="1000" [ngModel]="description()" (ngModelChange)="description.set($event)" placeholder="Describe what happened, when, and any error messages…"></textarea>
          <span class="text-muted" style="font-size:0.75rem; align-self:flex-end;">{{ description().length }}/1000</span>
        </div>
        <div class="field" style="margin-top:0.8rem;">
          <label>Attach a screenshot (optional)</label>
          <input type="file" accept=".png,.jpg,.jpeg,.pdf,.doc,.docx" (change)="onFileSelected($event)" />
          @if (selectedFile(); as f) {
            <span class="text-muted" style="font-size:0.78rem;">{{ f.name }} ({{ (f.size / 1024) | number:'1.0-0' }} KB)</span>
          }
          <span class="text-muted" style="font-size:0.75rem;">A screenshot of an error message or console can help us diagnose faster. Max 10 MB.</span>
        </div>
        <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="submitting()" (click)="submit()">
          {{ submitting() ? 'Submitting…' : 'Submit Issue' }}
        </button>
        @if (errorMessage(); as err) {
          <div class="error">{{ err }}</div>
        }
      } @else {
        <div class="success">Submitted — ticket <span class="mono">{{ submittedId() }}</span>. You can track it under My Tickets.</div>
      }
    </div>
  `,
  styles: [`
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    textarea { resize: vertical; width: 100%; }
    select { width: 100%; }
    .success { margin-top: 1rem; padding: 0.7rem 0.9rem; border-radius: 8px; background: var(--green-bg); color: var(--green); font-size: 0.85rem; }
    .error { margin-top: 1rem; padding: 0.7rem 0.9rem; border-radius: 8px; background: var(--red-bg, #fdecea); color: var(--red, #b3261e); font-size: 0.85rem; }
  `],
})
export class SubmitIssueComponent {
  category = signal<TicketCategory>('Bug');
  failureTypeId = signal<string>('');
  description = signal('');
  selectedFile = signal<File | null>(null);
  submittedId = signal<string | null>(null);
  submitting = signal(false);
  errorMessage = signal<string | null>(null);

  private readonly maxFileSizeBytes = 10 * 1024 * 1024;

  constructor(private auth: AuthService, private agreements: AgreementService, private tickets: TicketService, public failureTypes: FailureTypeService) {}

  agreement = computed(() => {
    const client = this.auth.currentClient();
    if (!client) return undefined;
    return this.agreements.forClient(client.id).find(a => a.status === 'Active') ?? this.agreements.forClient(client.id)[0];
  });

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.errorMessage.set(null);

    if (file && file.size > this.maxFileSizeBytes) {
      this.errorMessage.set('That file is larger than 10 MB — please choose a smaller one.');
      this.selectedFile.set(null);
      input.value = '';
      return;
    }

    this.selectedFile.set(file);
  }

  async submit() {
    const client = this.auth.currentClient();
    const agreement = this.agreement();
    if (!client || !agreement || !this.description().trim()) return;

    this.submitting.set(true);
    this.errorMessage.set(null);

    try {
      const ticket = await this.tickets.submitFromClient(
        client.id, agreement.id, this.description().trim(), this.category(),
        this.failureTypeId() || undefined
      );

      const file = this.selectedFile();
      if (file) {
        try {
          await this.tickets.uploadAttachment(ticket.id, file);
        } catch {
          // The ticket itself was submitted successfully — don't block
          // that on the attachment. Let the person know the attachment
          // specifically failed so they can retry it separately later
          // (e.g. re-upload from the ticket's detail view) rather than
          // assume the whole submission was lost.
          this.errorMessage.set('Your issue was submitted, but the attachment failed to upload. You can try attaching it again from My Tickets.');
        }
      }

      this.submittedId.set(ticket.id);
      this.description.set('');
      this.failureTypeId.set('');
      this.selectedFile.set(null);
    } finally {
      this.submitting.set(false);
    }
  }
}
