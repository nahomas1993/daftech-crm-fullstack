import { Component, computed, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { TicketService } from '../../core/services/ticket.service';
import { EmployeeService } from '../../core/services/employee.service';
import { AuthService } from '../../core/services/auth.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { TicketStatus, TICKET_CATEGORY_LABELS } from '../../core/models';

@Component({
  selector: 'app-tickets',
  standalone: true,
  imports: [BadgeComponent, PaginationComponent, SlicePipe, FilePreviewModalComponent],
  template: `
    <h1>Tickets</h1>
    <p class="text-muted" style="margin-top:0.3rem;">
      Client-submitted support issues. Assignment is automatic — the system picks the technician with the fewest open tickets the moment a ticket is submitted.
    </p>

    @if (isAdmin()) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <h3>Escalated — Needs Admin Review</h3>
        <p class="text-muted" style="font-size:0.8rem; margin: 0.2rem 0 0.9rem;">
          The client rated these below the 90/100 satisfaction threshold after resolution.
        </p>
        <div class="table-scroll"><table>
          <thead><tr><th>Ticket</th><th>Client</th><th>Assigned To</th><th>Rating</th><th></th></tr></thead>
          <tbody>
            @for (t of tickets.escalated(); track t.id) {
              <tr>
                <td class="mono">{{ t.id.slice(0,8) }}</td>
                <td>{{ t.clientName }}</td>
                <td class="text-muted">{{ t.assignedEmployeeName ?? '—' }}</td>
                <td><span class="badge badge-red">{{ t.satisfactionStars }}★ ({{ t.satisfactionScore }}/100)</span></td>
                <td class="text-muted" style="font-size:0.8rem;">{{ t.description }}</td>
              </tr>
            }
            @empty { <tr><td colspan="5" class="text-muted" style="text-align:center; padding:1rem;">No escalations right now.</td></tr> }
          </tbody>
        </table></div>
      </div>
    }

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <h3>{{ isAdmin() ? 'All Tickets' : 'My Tickets' }}</h3>
      <div class="table-scroll"><table style="margin-top:0.75rem;">
        <thead><tr><th>Ticket</th><th>Client</th><th>Category</th><th>Submitted</th><th>Assigned</th><th>Chargeable</th><th>Status</th><th>Satisfaction</th><th></th></tr></thead>
        <tbody>
          @for (t of tickets.pagedTickets(); track t.id) {
            <tr
              [class.row-faded]="isFinished(t.status) && t.status !== 'Resolved'"
              [class.row-resolved]="t.status === 'Resolved'"
              [class.row-in-progress]="t.status === 'InProgress'"
            >
              <td class="mono">{{ t.id.slice(0,8) }}</td>
              <td>{{ t.clientName }}</td>
              <td>{{ categoryLabel(t.category) }}</td>
              <td class="text-muted">{{ t.dateSubmitted | slice:0:10 }}</td>
              <td class="text-muted">{{ t.assignedEmployeeName ?? '—' }}</td>
              <td><app-badge [status]="t.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
              <td><app-badge [status]="t.status"></app-badge></td>
              <td class="text-muted">{{ t.satisfactionScore != null ? t.satisfactionScore + '/100' : '—' }}</td>
              <td>
                @if (t.attachmentFileName) {
                  <button class="btn btn-outline btn-sm" (click)="viewAttachment(t.id, t.attachmentFileName)">📎 Attachment</button>
                }
                @if (t.voiceNoteFileName) {
                  <button class="btn btn-outline btn-sm" (click)="viewVoiceNote(t.id, t.voiceNoteFileName)">🎤 Voice note</button>
                }
                @if (canUpdateStatus(t)) {
                  <select
                    style="margin-right:0.3rem;"
                    [value]="selectedStatusFor(t)"
                    (change)="onStatusSelect(t.id, $any($event.target).value)"
                  >
                    <option value="InProgress">In Progress</option>
                    <option value="Resolved">Resolved (sends to client for confirmation)</option>
                  </select>
                  <button class="btn btn-outline btn-sm" (click)="updateStatus(t.id, selectedStatusFor(t))" [disabled]="updatingTicketId() === t.id">
                    {{ updatingTicketId() === t.id ? 'Updating…' : 'Update' }}
                  </button>
                }
                @if (statusError(); as err) {
                  @if (err.ticketId === t.id) { <p class="status-error">{{ err.message }}</p> }
                }
                @if (statusSuccess(); as msg) {
                  @if (msg.ticketId === t.id) { <p class="status-success">{{ msg.message }}</p> }
                }
              </td>
            </tr>
          }
          @empty { <tr><td colspan="9" class="text-muted" style="text-align:center; padding:1rem;">No tickets yet.</td></tr> }
        </tbody>
      </table></div>
      <app-pagination
        [page]="tickets.page()"
        [totalPages]="tickets.totalPages()"
        [totalCount]="tickets.totalCount()"
        [pageSize]="tickets.pageSize()"
        (pageChange)="tickets.goToPage($event)">
      </app-pagination>
    </div>

    <app-file-preview-modal
      [open]="previewOpen()"
      [title]="previewTitle()"
      [fileName]="previewFileName()"
      [kind]="previewKind()"
      [load]="previewLoader"
      (closed)="closePreview()">
    </app-file-preview-modal>
  `,
  styles: [`
    .status-error { color: var(--red); font-size: 0.76rem; margin: 0.35rem 0 0; }
    .status-success { color: var(--blue); font-size: 0.76rem; margin: 0.35rem 0 0; }
    .row-faded { opacity: 0.45; filter: grayscale(35%); }
    .row-faded:hover { opacity: 0.75; filter: none; }
    /* Resolved tickets recede visually so a piled-up queue reads as "handled" at a glance — blur clears on hover/focus so the row is still fully readable when needed (e.g. re-checking details). */
    .row-resolved { opacity: 0.55; filter: blur(1.5px); transition: filter 0.15s ease, opacity 0.15s ease; }
    .row-resolved:hover, .row-resolved:focus-within { filter: none; opacity: 1; }
    /* In-progress tickets stand out as active work still needing attention. */
    .row-in-progress { font-weight: 700; }
    .row-in-progress td { color: var(--navy-900); }
  `],
})
export class TicketsComponent {
  constructor(
    public tickets: TicketService,
    public employees: EmployeeService,
    private auth: AuthService
  ) {}

  updatingTicketId = signal<string | null>(null);
  statusError = signal<{ ticketId: string; message: string } | null>(null);
  statusSuccess = signal<{ ticketId: string; message: string } | null>(null);

  // The status dropdown only offers InProgress/Resolved as choices, but a
  // ticket's real status can also be Assigned (nothing picked yet). This
  // map holds the technician's in-progress *selection* per ticket,
  // separate from the ticket's actual server-side status — so the <select>
  // stays a normal Angular-bound control instead of an uncontrolled DOM
  // element that could silently keep a stale value across a failed
  // request and retry.
  private selectedStatus = signal<Map<string, 'InProgress' | 'Resolved'>>(new Map());

  /** What the dropdown should show for this ticket: the technician's own pending pick if they've touched it, otherwise a sensible default from the ticket's real status (Assigned tickets default to InProgress, the first real step). */
  selectedStatusFor(t: { id: string; status: TicketStatus }): 'InProgress' | 'Resolved' {
    const picked = this.selectedStatus().get(t.id);
    if (picked) return picked;
    return t.status === 'Resolved' ? 'Resolved' : 'InProgress';
  }

  onStatusSelect(ticketId: string, value: string) {
    const next = new Map(this.selectedStatus());
    next.set(ticketId, value as 'InProgress' | 'Resolved');
    this.selectedStatus.set(next);
  }

  isAdmin = computed(() => this.auth.currentEmployee()?.roles.includes('Admin') ?? false);

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }

  private readonly finishedStatuses: TicketStatus[] = [
    'AwaitingClientConfirmation', 'Resolved', 'Closed', 'Escalated'
  ];

  isFinished(status: TicketStatus): boolean {
    return this.finishedStatuses.includes(status);
  }

  canUpdateStatus(t: { assignedEmployeeId?: string; status: TicketStatus }): boolean {
    const emp = this.auth.currentEmployee();
    if (!emp) return false;
    return emp.roles.includes('EmployeeTechnician') && t.assignedEmployeeId === emp.id && ['Assigned', 'InProgress'].includes(t.status);
  }

  async updateStatus(ticketId: string, status: string) {
    // Guards against a double-tap firing two PATCH requests for the same
    // ticket before the first response (and the [disabled] binding) lands —
    // the second would race the first and the server would reject it as a
    // lost concurrency update.
    if (this.updatingTicketId() === ticketId) return;

    const actor = this.auth.currentEmployee()?.fullName ?? 'Staff';
    this.statusError.set(null);
    this.statusSuccess.set(null);
    this.updatingTicketId.set(ticketId);
    try {
      await this.tickets.updateStatus(ticketId, status as TicketStatus, actor);

      const next = new Map(this.selectedStatus());
      next.delete(ticketId);
      this.selectedStatus.set(next);

      // Marking "Resolved" doesn't set the ticket to a Resolved status — the server
      // moves it to AwaitingClientConfirmation and starts the confirmation window.
      // Without this message the row just looks unchanged, so we spell out what happened.
      if (status === 'Resolved') {
        this.statusSuccess.set({
          ticketId,
          message: 'Marked resolved — waiting on client confirmation.',
        });
      }
    } catch (err: any) {
      // ExceptionHandlingMiddleware returns { error: string, traceId }.
      // Controllers' own BadRequest/NotFound(ex.Message) return a plain string.
      // Angular puts the parsed JSON body in err.error either way, so it can be
      // a string OR an object depending on which path produced it — handle both,
      // or we render the raw object as "[object Object]".
      const raw = err?.error;
      const message =
        typeof raw === 'string' ? raw
        : raw?.error ?? err?.message
        ?? 'Could not update this ticket — please try again.';
      this.statusError.set({ ticketId, message });
      console.error('Failed to update ticket status', err);
    } finally {
      this.updatingTicketId.set(null);
    }
  }

  // File preview modal state — attachments and voice notes open inline
  // (audio player / image / PDF viewer) instead of forcing a download, so
  // technicians can see and listen to them without leaving the system.
  previewOpen = signal(false);
  previewTitle = signal('');
  previewFileName = signal('');
  previewKind = signal<FilePreviewKind>('other');
  previewLoader: (() => Promise<Blob>) | undefined;

  viewAttachment(ticketId: string, fileName: string) {
    // Close first if a preview is already open for a different file — this
    // guarantees the [open] input goes false→true so the modal's
    // ngOnChanges reliably re-fires and fetches the new file.
    this.previewOpen.set(false);
    this.previewTitle.set('Attachment');
    this.previewFileName.set(fileName);
    this.previewKind.set(filePreviewKindFor(fileName));
    this.previewLoader = () => this.tickets.downloadAttachment(ticketId);
    setTimeout(() => this.previewOpen.set(true));
  }

  viewVoiceNote(ticketId: string, fileName: string) {
    this.previewOpen.set(false);
    this.previewTitle.set('Voice note');
    this.previewFileName.set(fileName);
    this.previewKind.set('audio');
    this.previewLoader = () => this.tickets.downloadVoiceNote(ticketId);
    setTimeout(() => this.previewOpen.set(true));
  }

  closePreview() {
    this.previewOpen.set(false);
  }
}