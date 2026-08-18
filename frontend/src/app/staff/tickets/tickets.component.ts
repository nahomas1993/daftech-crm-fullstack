import { Component, computed, signal, OnInit, OnDestroy } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { TicketService } from '../../core/services/ticket.service';
import { EmployeeService } from '../../core/services/employee.service';
import { AuthService } from '../../core/services/auth.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { TicketStatus, TICKET_CATEGORY_LABELS } from '../../core/models';
import { formatRemaining } from '../../portal/maintenance-history/maintenance-history.component';

/** How often to re-pull the ticket list while this page is open, so a
 * change made elsewhere (another technician updating a ticket, an Admin
 * reassigning one, the same technician in a second tab) shows up here
 * before the next manual action — without this, a stale row's "Update"
 * button can send a PATCH for a ticket that's already moved on, which
 * the server correctly (but confusingly) reports as 404. Matches the
 * same polling approach already used on the client portal's
 * MaintenanceHistoryComponent — there's no SignalR push wired up yet. */
const REFRESH_INTERVAL_MS = 20_000;

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
        <thead><tr><th>Ticket</th><th>Client</th><th>Category</th><th>Failure Type</th><th>Submitted</th><th>Assigned</th><th>Chargeable</th><th>Status</th><th>Expected Resolution</th><th>Time Remaining</th><th>Satisfaction</th><th></th></tr></thead>
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
              <td class="text-muted">{{ t.failureTypeName ?? '—' }}</td>
              <td class="text-muted">{{ t.dateSubmitted | slice:0:10 }}</td>
              <td class="text-muted">{{ t.assignedEmployeeName ?? '—' }}</td>
              <td><app-badge [status]="t.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
              <td><app-badge [status]="t.status"></app-badge></td>
              <td class="text-muted" style="font-size:0.8rem;">
                @if (t.expectedResolutionBy) {
                  {{ resolutionDeadlineLabel(t.expectedResolutionBy) }}
                } @else if (!t.assignedAt) {
                  Awaiting assignment
                } @else {
                  —
                }
              </td>
              <td style="font-size:0.8rem;" [class.sla-overdue]="isOverdue(t)" [class.sla-soon]="isDueSoon(t)">
                {{ countdownLabel(t) }}
              </td>
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
          @empty { <tr><td colspan="12" class="text-muted" style="text-align:center; padding:1rem;">No tickets yet.</td></tr> }
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
    .status-success { color: #0f7b3d; background: rgba(16, 145, 74, 0.1); border: 1px solid rgba(16, 145, 74, 0.28); border-radius: 6px; font-size: 0.78rem; line-height: 1.35; font-weight: 600; margin: 0.4rem 0 0; padding: 0.35rem 0.5rem; }
    .row-faded { opacity: 0.45; filter: grayscale(35%); }
    .row-faded:hover { opacity: 0.75; filter: none; }
    /* Resolved tickets recede visually so a piled-up queue reads as "handled" at a glance — blur clears on hover/focus so the row is still fully readable when needed (e.g. re-checking details). */
    .row-resolved { opacity: 0.55; filter: blur(1.5px); transition: filter 0.15s ease, opacity 0.15s ease; }
    .row-resolved:hover, .row-resolved:focus-within { filter: none; opacity: 1; }
    /* In-progress tickets stand out as active work still needing attention. */
    .row-in-progress { font-weight: 700; }
    .row-in-progress td { color: var(--navy-900); }
    /* SLA countdown emphasis: red once the expected resolution time has run out, amber in the final hour. */
    .sla-overdue { color: var(--red, #b3261e); font-weight: 700; }
    .sla-soon { color: #b06a00; font-weight: 700; }
  `],
})
export class TicketsComponent implements OnInit, OnDestroy {
  constructor(
    public tickets: TicketService,
    public employees: EmployeeService,
    private auth: AuthService
  ) {}

  private pollHandle: ReturnType<typeof setInterval> | undefined;

  ngOnInit() {
    this.pollHandle = setInterval(() => this.refreshTickets(), REFRESH_INTERVAL_MS);
    this.tickHandle = setInterval(() => this.nowTick.set(Date.now()), 1000);
  }

  ngOnDestroy() {
    if (this.pollHandle) clearInterval(this.pollHandle);
    if (this.tickHandle) clearInterval(this.tickHandle);
  }

  private refreshTickets() {
    // Skip a poll tick while an update is in flight — updateStatus()
    // already refreshes the list itself on success, and racing that with
    // a concurrent poll-triggered refresh could otherwise briefly show
    // stale data right after a successful update.
    if (this.updatingTicketId()) return;
    void this.tickets.refreshPaged();
  }

  updatingTicketId = signal<string | null>(null);
  statusError = signal<{ ticketId: string; message: string } | null>(null);
  statusSuccess = signal<{ ticketId: string; message: string } | null>(null);

  // The status dropdown only offers InProgress/Resolved as choices, but a
  // ticket's real status can also be Assigned (nothing picked yet). This
  // map holds the technician's in-progress *selection* per ticket,
  // separate from the ticket's actual server-side status — so the <select>
  // stays a normal Angular-bound control instead of an uncontrolled DOM
  // element that could silently keep a stale value across a failed
  // request and retry. Selections here survive a background poll refresh
  // since selectedStatusFor() always checks this map first.
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

  /**
   * Formats the server-computed expected-resolution deadline
   * (AssignedAt + the ticket's failure type duration — see
   * TicketService.ProjectAsync on the backend) for display. Purely
   * presentational: the deadline itself is always the value the API
   * returned, never recalculated here.
   */
  /** Ticks every second so the countdown below visibly runs down. */
  nowTick = signal(Date.now());
  private tickHandle: ReturnType<typeof setInterval> | undefined;

  private remainingMs(t: { expectedResolutionBy?: string }): number | null {
    this.nowTick();
    if (!t.expectedResolutionBy) return null;
    return new Date(t.expectedResolutionBy).getTime() - Date.now();
  }

  private isRunning(t: { status: TicketStatus }): boolean {
    return ['Submitted', 'Assigned', 'InProgress'].includes(t.status);
  }

  /**
   * Live SLA countdown for the assigned technician: starts the moment the
   * ticket is assigned and runs against the deadline the server computed
   * from the failure type the client chose (AssignedAt + the admin's
   * expected resolution time). Stops once the work is handed back.
   */
  countdownLabel(t: { status: TicketStatus; expectedResolutionBy?: string; assignedAt?: string }): string {
    const remaining = this.remainingMs(t);
    if (remaining === null) return t.assignedAt ? '—' : 'Not started';
    if (!this.isRunning(t)) return 'Stopped';
    return remaining <= 0 ? 'Overdue' : formatRemaining(remaining);
  }

  isOverdue(t: { status: TicketStatus; expectedResolutionBy?: string }): boolean {
    const remaining = this.remainingMs(t);
    return remaining !== null && this.isRunning(t) && remaining <= 0;
  }

  /** Within the last hour before the deadline — visual warning for the technician. */
  isDueSoon(t: { status: TicketStatus; expectedResolutionBy?: string }): boolean {
    const remaining = this.remainingMs(t);
    return remaining !== null && this.isRunning(t) && remaining > 0 && remaining <= 3_600_000;
  }

  resolutionDeadlineLabel(expectedResolutionBy: string): string {
    const deadline = new Date(expectedResolutionBy);
    const now = new Date();
    const label = deadline.toLocaleString(undefined, {
      month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit',
    });
    return deadline.getTime() < now.getTime()
      ? `${label} (overdue)`
      : label;
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
      // moves it to AwaitingClientConfirmation, notifies the client and starts the
      // confirmation window. Spell that out so the technician knows what happened next.
      this.statusSuccess.set({
        ticketId,
        message:
          status === 'Resolved'
            ? '✓ Sent to the client for confirmation — the client has been notified and must confirm the work is done.'
            : `✓ Ticket updated to ${status}.`,
      });

    } catch (err: any) {
      // TicketService.updateStatus() already maps the failure to a
      // user-facing message by HTTP status code (409/404/500) and throws
      // a plain Error with that message — just display it.
      const message = err?.message ?? 'Could not update this ticket — please try again.';
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
