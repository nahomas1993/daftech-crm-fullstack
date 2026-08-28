import { Component, computed, signal, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TicketService } from '../../core/services/ticket.service';
import { AgreementService } from '../../core/services/agreement.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { FailureTypeService } from '../../core/services/failure-type.service';
import { SupportTypeService } from '../../core/services/support-type.service';
import { BadgeComponent } from '../../shared/badge.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { TICKET_CATEGORY_LABELS, TicketCategory, TicketStatus, TicketQuote } from '../../core/models';

type FilterKey = 'all' | 'pending' | 'accomplished' | 'escalated';

const PENDING_STATUSES: TicketStatus[] = ['Submitted', 'Assigned', 'InProgress', 'Resolved', 'AwaitingClientConfirmation'];

/** How often to re-pull ticket status while this page is open, so a
 * technician's status change shows up without a manual refresh. There's
 * no SignalR push wired up on the client portal yet, so poll instead. */
const REFRESH_INTERVAL_MS = 20_000;

@Component({
  selector: 'app-maintenance-history',
  standalone: true,
  imports: [FormsModule, SlicePipe, RouterLink, BadgeComponent, FilePreviewModalComponent],
  template: `
    <div class="head-row">
      <div>
        <h1>Maintenance History</h1>
        <p class="text-muted" style="margin-top:0.3rem;">Every issue you've submitted, who's working on it, and where it stands.</p>
      </div>
      <button class="btn btn-primary" (click)="toggleSubmitPanel()">
        {{ showSubmitPanel() ? 'Cancel' : '+ Submit New Issue' }}
      </button>
    </div>

    @if (showSubmitPanel()) {
      <div class="panel panel-pad" style="margin-top:1.1rem; max-width:520px;">
        @if (myProducts().length === 0) {
          <p class="text-muted">No systems/products found on your account — please contact DAFTECH directly.</p>
        } @else {
          <div class="field">
            <label>System/Product <span class="req">*</span></label>
            <select [ngModel]="systemProductId()" (ngModelChange)="onSystemProductChange($event)">
              <option value="">Select which system/product this is about…</option>
              @for (p of myProducts(); track p.id) {
                <option [value]="p.id">{{ p.name }}</option>
              }
            </select>
          </div>
          @if (systemProductId() && !agreement()) {
            <p class="text-muted" style="margin-top:0.6rem;">No agreement found for this system/product — please contact DAFTECH directly.</p>
          }
          @if (agreement()) {
          <div class="field" style="margin-top:0.8rem;">
            <label>Category</label>
            <select [ngModel]="category()" (ngModelChange)="onCategoryChange($event)">
              <option value="Frontend">Frontend</option>
              <option value="Backend">Backend</option>
              <option value="Database">Database</option>
            </select>
          </div>
          <div class="field" style="margin-top:0.8rem;">
            <label>Failure Type</label>
            @if (failureTypes.types().length > 0) {
              <select [ngModel]="failureTypeId()" (ngModelChange)="onFailureTypeChange($event)">
                <option value="">Not sure / other…</option>
                @for (f of failureTypes.types(); track f.id) {
                  @if (f.category === category()) { <option [value]="f.id">{{ f.name }}</option> }
                }
              </select>
              @if (selectedFailureType(); as ft) {
                <span class="text-muted" style="font-size:0.75rem;">
                  Expected resolution time: {{ durationLabel(ft.durationValue, ft.durationUnit) }} once a technician is assigned.@if (ft.description) { <span> — {{ ft.description }}</span> }
                </span>
              }
            } @else if (failureTypes.loading()) {
              <span class="text-muted" style="font-size:0.78rem;">Loading failure types…</span>
            } @else {
              <span class="text-muted" style="font-size:0.78rem;">
                {{ failureTypes.error() ?? 'No failure types have been configured yet by DAFTECH.' }}
              </span>
              <button type="button" class="btn btn-outline btn-sm" style="margin-top:0.4rem; align-self:flex-start;" (click)="reloadFailureTypes()">Retry</button>
            }
          </div>
          @if (supportTypes.types().length > 0) {
            <div class="field" style="margin-top:0.8rem;">
              <label>Support Type</label>
              <select [ngModel]="supportTypeId()" (ngModelChange)="onSupportTypeChange($event)">
                <option value="">No preference</option>
                @for (s of supportTypes.types(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
              @if (selectedSupportType(); as st) {
                @if (st.description) {
                  <span class="text-muted" style="font-size:0.75rem;">{{ st.description }}</span>
                }
              }
            </div>
          }

          @if (quote(); as q) {
            <div class="quote-box" [class.chargeable]="q.chargeable" style="margin-top:0.9rem;">
              @if (!q.chargeable) {
                <strong>Free Support — 0 ETB</strong>
                <p class="text-muted" style="font-size:0.78rem; margin:0.3rem 0 0;">
                  Your free support period is still running@if (q.freeSupportEndsOn) { <span> until {{ q.freeSupportEndsOn }}</span> }, so there's nothing to pay for this issue.
                </p>
              } @else {
                <strong>Chargeable Support — {{ q.total }} ETB</strong>
                <p class="text-muted" style="font-size:0.78rem; margin:0.3rem 0 0;">
                  Your free support period ended@if (q.freeSupportEndsOn) { <span> on {{ q.freeSupportEndsOn }}</span> }.
                  Failure type {{ q.basePrice }} ETB + support type {{ q.supportFee }} ETB.
                </p>
                <label class="ack">
                  <input type="checkbox" [ngModel]="acknowledged()" (ngModelChange)="acknowledged.set($event)" />
                  <span>I acknowledge that this request is chargeable.</span>
                </label>
              }
            </div>
          } @else if (quoteLoading()) {
            <p class="text-muted" style="font-size:0.78rem; margin-top:0.9rem;">Checking whether this issue is covered by your free support…</p>
          }

          <div class="field" style="margin-top:0.8rem;">
            <label>Description</label>
            <textarea rows="4" [ngModel]="description()" (ngModelChange)="description.set($event)" placeholder="Describe what happened, when, and any error messages…"></textarea>
          </div>
          <div class="field" style="margin-top:0.8rem;">
            <label>Attach a screenshot (optional)</label>
            <input type="file" accept=".png,.jpg,.jpeg,.pdf,.doc,.docx" (change)="onFileSelected($event)" />
            @if (selectedFile(); as f) {
              <span class="text-muted" style="font-size:0.78rem;">{{ f.name }}</span>
            }
          </div>
          <div class="field" style="margin-top:0.8rem;">
            <label>Record a voice note (optional)</label>
            <p class="text-muted" style="font-size:0.76rem; margin:0 0 0.4rem;">
              Describe the error out loud — it's attached alongside your written description for extra context.
            </p>
            @if (recordingUnsupported()) {
              <span class="text-muted" style="font-size:0.78rem;">Voice recording isn't supported in this browser.</span>
            } @else if (!recordedNote()) {
              <button
                type="button"
                class="btn btn-outline btn-sm"
                [class.recording]="isRecording()"
                (click)="isRecording() ? stopRecording() : startRecording()"
              >
                {{ isRecording() ? '⏹ Stop (' + recordingSeconds() + 's)' : '🎤 Start Recording' }}
              </button>
            } @else {
              <div style="display:flex; align-items:center; gap:0.6rem;">
                <audio [src]="recordedNoteUrl()" controls style="height:32px;"></audio>
                <button type="button" class="btn btn-outline btn-sm" (click)="discardRecording()">Remove</button>
              </div>
            }
            @if (recordingError()) {
              <div class="error" style="margin-top:0.5rem;">{{ recordingError() }}</div>
            }
          </div>
          <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="!systemProductId() || !agreement() || !description().trim() || submitting() || !acknowledgementSatisfied()" (click)="submit()">
            {{ submitting() ? 'Submitting…' : 'Submit Issue' }}
          </button>
          @if (submittedId(); as id) {
            <div class="success">Submitted — ticket <span class="mono">{{ id.slice(0,8).toUpperCase() }}</span>.</div>
          }
          @if (uploadError()) {
            <div class="error">{{ uploadError() }}</div>
          }
          }
        }
      </div>
    }

    <div class="filter-row">
      <button class="chip" [class.active]="filter() === 'all'" (click)="filter.set('all')">All ({{ counts().all }})</button>
      <button class="chip" [class.active]="filter() === 'pending'" (click)="filter.set('pending')">Pending ({{ counts().pending }})</button>
      <button class="chip" [class.active]="filter() === 'accomplished'" (click)="filter.set('accomplished')">Accomplished ({{ counts().accomplished }})</button>
      <button class="chip" [class.active]="filter() === 'escalated'" (click)="filter.set('escalated')">Escalated ({{ counts().escalated }})</button>
    </div>

    <div class="panel panel-pad" style="margin-top:1rem;">
      <div class="table-scroll"><table>
        <thead><tr><th>Ticket #</th><th>System/Product</th><th>Category</th><th>Failure Type</th><th>Submitted</th><th>Assigned To</th><th>Chargeable</th><th>Status</th><th>Time Left</th><th>Your Rating</th><th>Attachment</th><th></th></tr></thead>
        <tbody>
          @for (t of filteredTickets(); track t.id) {
            <tr>
              <td class="mono">{{ t.id.slice(0,8).toUpperCase() }}</td>
              <td class="text-muted">{{ t.systemProductName ?? '—' }}</td>
              <td>{{ categoryLabel(t.category) }}</td>
              <td class="text-muted">{{ t.failureTypeName ?? '—' }}</td>
              <td class="text-muted">{{ t.dateSubmitted | slice:0:10 }}</td>
              <td class="text-muted">{{ t.assignedEmployeeName || '—' }}</td>
              <td><app-badge [status]="t.chargeable ? 'Chargeable' : 'Free'"></app-badge></td>
              <td class="text-muted" style="font-size:0.8rem;">{{ countdownLabel(t) }}</td>
              <td><app-badge [status]="t.status"></app-badge></td>
              <td class="text-muted">{{ t.satisfactionStars ? (t.satisfactionStars + '★') : '—' }}</td>
              <td>
                @if (t.attachmentFileName) {
                  <button class="btn btn-outline btn-sm" (click)="viewAttachment(t.id, t.attachmentFileName)">View</button>
                } @else {
                  <span class="text-muted">—</span>
                }
              </td>
              <td>
                @if (t.status === 'AwaitingClientConfirmation') {
                  <a routerLink="/portal/confirm-resolution" class="btn btn-outline btn-sm">Verify Progress</a>
                } @else if (t.status === 'Closed') {
                  <a [routerLink]="['/portal/survey', t.id]" class="btn btn-outline btn-sm">Take Survey</a>
                }
              </td>
            </tr>
          }
          @empty { <tr><td colspan="12" class="text-muted" style="text-align:center; padding:1.5rem;">No tickets in this view yet.</td></tr> }
        </tbody>
      </table></div>
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
    .head-row { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    .field label .req { color: var(--red, #b3261e); }
    textarea { resize: vertical; width: 100%; }
    select { width: 100%; }
    .success { margin-top: 1rem; padding: 0.7rem 0.9rem; border-radius: 8px; background: var(--green-bg); color: var(--green); font-size: 0.85rem; }
    .error { margin-top: 1rem; padding: 0.7rem 0.9rem; border-radius: 8px; background: var(--red-bg, #fdecea); color: var(--red, #b3261e); font-size: 0.85rem; }
    .filter-row { display: flex; gap: 0.5rem; margin-top: 1.25rem; flex-wrap: wrap; }
    .chip {
      background: #fff; border: 1px solid var(--slate-200); padding: 0.4rem 0.85rem; border-radius: 999px;
      font-size: 0.8rem; font-weight: 600; color: var(--slate-500);
    }
    .chip.active { background: var(--portal-accent); border-color: var(--portal-accent); color: #fff; }
    .quote-box { border: 1px solid var(--slate-200); border-radius: 10px; padding: 0.75rem 0.9rem; background: #f7fbf8; }
    .quote-box.chargeable { background: #fff8ef; border-color: #f0d3a8; }
    .quote-box .ack { display: flex; align-items: center; gap: 0.5rem; margin-top: 0.6rem; font-size: 0.8rem; font-weight: 600; }
    .quote-box .ack input { width: 16px; height: 16px; }
    .btn.recording { background: var(--red-bg, #fdecea); border-color: var(--red, #b3261e); color: var(--red, #b3261e); animation: pulse 1.4s ease-in-out infinite; }
    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }
  `],
})
export class MaintenanceHistoryComponent implements OnInit, OnDestroy {
  showSubmitPanel = signal(false);
  category = signal<TicketCategory>('Frontend');
  /** Required — which of the client's systems/products this issue is about. Chosen first; the agreement to bill against is resolved from it. */
  systemProductId = signal<string>('');
  failureTypeId = signal<string>('');
  supportTypeId = signal<string>('');
  description = signal('');
  /** Server-priced quote for the issue being written up; null until one has been fetched. */
  quote = signal<TicketQuote | null>(null);
  quoteLoading = signal(false);
  acknowledged = signal(false);
  selectedFile = signal<File | null>(null);
  submittedId = signal<string | null>(null);
  submitting = signal(false);
  uploadError = signal<string | null>(null);
  filter = signal<FilterKey>('all');

  // Voice note recording state. recordedNote holds the raw Blob until
  // submit() actually uploads it — recording doesn't touch the server at
  // all, only submitting does (see uploadVoiceNoteIfPresent below).
  recordingUnsupported = signal(!(typeof MediaRecorder !== 'undefined' && navigator.mediaDevices?.getUserMedia));
  isRecording = signal(false);
  recordingSeconds = signal(0);
  recordedNote = signal<Blob | null>(null);
  recordedNoteUrl = signal<string | null>(null);
  recordingError = signal<string | null>(null);

  private mediaRecorder: MediaRecorder | null = null;
  private recordedChunks: Blob[] = [];
  private recordingTimer: ReturnType<typeof setInterval> | null = null;
  private activeStream: MediaStream | null = null;

  private readonly maxFileSizeBytes = 10 * 1024 * 1024;
  private readonly maxRecordingSeconds = 180; // 3 minutes — plenty for describing an issue, keeps upload size sane

  constructor(
    private auth: AuthService,
    private ticketsSvc: TicketService,
    private agreementsSvc: AgreementService,
    public systemProductsSvc: SystemProductService,
    private route: ActivatedRoute,
    public failureTypes: FailureTypeService,
    public supportTypes: SupportTypeService
  ) {}

  /** All of the client's systems/products — what the required selector offers. */
  myProducts = computed(() => {
    const client = this.auth.currentClient();
    if (!client) return [];
    this.systemProductsSvc.byClient();
    return this.systemProductsSvc.systemProductsFor(client.id);
  });

  onSystemProductChange(value: string) {
    this.systemProductId.set(value);
    void this.refreshQuote();
  }

  selectedFailureType = computed(() =>
    this.failureTypes.types().find(f => f.id === this.failureTypeId())
  );

  selectedSupportType = computed(() =>
    this.supportTypes.types().find(s => s.id === this.supportTypeId())
  );

  /**
   * A chargeable issue can't be sent until the client ticks the box. When the
   * quote hasn't arrived yet we also hold the button, so nobody can submit
   * before finding out whether they're about to be billed.
   */
  acknowledgementSatisfied = computed(() => {
    const q = this.quote();
    if (!q) return !this.quoteLoading();
    return !q.chargeable || this.acknowledged();
  });

  onCategoryChange(value: TicketCategory) {
    this.category.set(value);
    this.failureTypeId.set('');
    void this.refreshQuote();
  }

  onFailureTypeChange(value: string) {
    this.failureTypeId.set(value);
    void this.refreshQuote();
  }

  onSupportTypeChange(value: string) {
    this.supportTypeId.set(value);
    void this.refreshQuote();
  }

  /**
   * Re-prices the issue whenever the client changes something that affects
   * the total. Any change clears a previous acknowledgement — you can only
   * accept the figure you were actually shown.
   */
  async refreshQuote(): Promise<void> {
    const agreement = this.agreement();
    if (!agreement) { this.quote.set(null); return; }

    this.acknowledged.set(false);
    this.quoteLoading.set(true);
    try {
      const q = await this.ticketsSvc.quote(
        agreement.id,
        this.failureTypeId() || undefined,
        this.supportTypeId() || undefined
      );
      this.quote.set(q);
    } catch {
      this.quote.set(null);
      this.uploadError.set('We couldn\'t work out the cost of this request just now. Please try again in a moment.');
    } finally {
      this.quoteLoading.set(false);
    }
  }

  durationLabel(value: number, unit: string): string {
    const noun = unit === 'Hours' ? 'hour' : unit === 'Days' ? 'day' : 'month';
    return `${value} ${noun}${value === 1 ? '' : 's'}`;
  }

  /**
   * Live countdown against the server-computed expected resolution
   * deadline (AssignedAt + the failure type's expected resolution time).
   * `nowTick` re-evaluates this every second so the timer visibly runs.
   */
  countdownLabel(t: { status: TicketStatus; expectedResolutionBy?: string }): string {
    this.nowTick();
    if (!t.expectedResolutionBy) return '—';
    if (['Resolved', 'AwaitingClientConfirmation', 'Closed'].includes(t.status)) return 'Done';
    const remainingMs = new Date(t.expectedResolutionBy).getTime() - Date.now();
    return remainingMs <= 0 ? 'Overdue' : formatRemaining(remainingMs);
  }

  nowTick = signal(Date.now());
  private tickHandle: ReturnType<typeof setInterval> | undefined;

  private pollHandle: ReturnType<typeof setInterval> | undefined;

  ngOnInit() {
    const q = this.route.snapshot.queryParamMap.get('filter') as FilterKey | null;
    if (q && ['all', 'pending', 'accomplished', 'escalated'].includes(q)) this.filter.set(q);

    this.refreshTickets();
    this.tickHandle = setInterval(() => this.nowTick.set(Date.now()), 1000);
    void this.failureTypes.refresh();
    void this.supportTypes.refresh();
    const client = this.auth.currentClient();
    if (client) void this.systemProductsSvc.refreshForClient(client.id);
    this.pollHandle = setInterval(() => this.refreshTickets(), REFRESH_INTERVAL_MS);
  }

  ngOnDestroy() {
    if (this.pollHandle) clearInterval(this.pollHandle);
    if (this.tickHandle) clearInterval(this.tickHandle);
  }

  private refreshTickets() {
    const client = this.auth.currentClient();
    if (client) void this.ticketsSvc.refreshMyTickets(client.id);
  }

  reloadFailureTypes() {
    void this.failureTypes.refresh();
  }

  toggleSubmitPanel() {
    this.showSubmitPanel.update(v => !v);
    if (!this.showSubmitPanel()) { /* closing — nothing to refresh */ } else {
      void this.failureTypes.refresh();
      void this.supportTypes.refresh();
      void this.refreshQuote();
    }
    this.acknowledged.set(false);
    this.submittedId.set(null);
    this.selectedFile.set(null);
    this.uploadError.set(null);
    this.discardRecording();
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.uploadError.set(null);

    if (file && file.size > this.maxFileSizeBytes) {
      this.uploadError.set('That file is larger than 10 MB — please choose a smaller one.');
      this.selectedFile.set(null);
      input.value = '';
      return;
    }

    this.selectedFile.set(file);
  }

  async startRecording() {
    this.recordingError.set(null);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      this.activeStream = stream;
      this.recordedChunks = [];

      // audio/webm is what Chrome/Edge/Firefox produce by default and is
      // in the backend's allowed-extension list; Safari falls back to
      // whatever MediaRecorder picks (still accepted server-side, see
      // StorageOptions.AllowedExtensions).
      const mimeType = MediaRecorder.isTypeSupported('audio/webm') ? 'audio/webm' : '';
      this.mediaRecorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);

      this.mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) this.recordedChunks.push(e.data);
      };
      this.mediaRecorder.onstop = () => {
        const blob = new Blob(this.recordedChunks, { type: this.mediaRecorder?.mimeType || 'audio/webm' });
        this.recordedNote.set(blob);
        this.recordedNoteUrl.set(URL.createObjectURL(blob));
        // Release the mic once we're done recording — leaving the stream
        // open would keep the browser's "recording" indicator active.
        this.activeStream?.getTracks().forEach(t => t.stop());
        this.activeStream = null;
      };

      this.mediaRecorder.start();
      this.isRecording.set(true);
      this.recordingSeconds.set(0);
      this.recordingTimer = setInterval(() => {
        const next = this.recordingSeconds() + 1;
        this.recordingSeconds.set(next);
        if (next >= this.maxRecordingSeconds) this.stopRecording();
      }, 1000);
    } catch {
      this.recordingError.set('Could not access your microphone — check your browser permissions and try again.');
      this.isRecording.set(false);
    }
  }

  stopRecording() {
    if (this.recordingTimer) {
      clearInterval(this.recordingTimer);
      this.recordingTimer = null;
    }
    this.mediaRecorder?.stop();
    this.isRecording.set(false);
  }

  discardRecording() {
    if (this.isRecording()) this.stopRecording();
    const url = this.recordedNoteUrl();
    if (url) URL.revokeObjectURL(url);
    this.recordedNote.set(null);
    this.recordedNoteUrl.set(null);
    this.recordingError.set(null);
    this.recordingSeconds.set(0);
  }

  // Opens the attachment inline (image/PDF preview, or "open in new tab"
  // for other types) instead of forcing a download.
  previewOpen = signal(false);
  previewTitle = signal('Attachment');
  previewFileName = signal('');
  previewKind = signal<FilePreviewKind>('other');
  previewLoader: (() => Promise<Blob>) | undefined;

  viewAttachment(ticketId: string, fileName: string) {
    // Close first so [open] reliably transitions false→true even when
    // switching directly between two tickets' attachments.
    this.previewOpen.set(false);
    this.previewFileName.set(fileName);
    this.previewKind.set(filePreviewKindFor(fileName));
    this.previewLoader = () => this.ticketsSvc.downloadAttachment(ticketId);
    setTimeout(() => this.previewOpen.set(true));
  }

  closePreview() {
    this.previewOpen.set(false);
  }

  /** Resolves to the active (or most recent) agreement for whichever system/product the client has selected — not just any agreement on the account, since a client with multiple products may have a different agreement per product. */
  agreement = computed(() => {
    const client = this.auth.currentClient();
    const productId = this.systemProductId();
    if (!client || !productId) return undefined;
    const agreements = this.agreementsSvc.forClient(client.id).filter(a => a.systemProductId === productId);
    return agreements.find(a => a.status === 'Active') ?? agreements[0];
  });

  private myTickets = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.ticketsSvc.forClient(client.id) : [];
  });

  counts = computed(() => {
    const all = this.myTickets();
    return {
      all: all.length,
      pending: all.filter(t => PENDING_STATUSES.includes(t.status)).length,
      accomplished: all.filter(t => t.status === 'Closed').length,
      escalated: all.filter(t => t.status === 'Escalated').length,
    };
  });

  filteredTickets = computed(() => {
    const all = this.myTickets();
    switch (this.filter()) {
      case 'pending': return all.filter(t => PENDING_STATUSES.includes(t.status));
      case 'accomplished': return all.filter(t => t.status === 'Closed');
      case 'escalated': return all.filter(t => t.status === 'Escalated');
      default: return all;
    }
  });

  categoryLabel(c: string): string {
    return TICKET_CATEGORY_LABELS[c as keyof typeof TICKET_CATEGORY_LABELS] ?? c;
  }

  async submit() {
    const client = this.auth.currentClient();
    const agreement = this.agreement();
    if (!client || !this.systemProductId() || !agreement || !this.description().trim()) return;
    // Belt and braces alongside the disabled button: never post a chargeable
    // issue that hasn't been acknowledged (the server rejects it too).
    if (!this.acknowledgementSatisfied()) return;

    this.submitting.set(true);
    this.uploadError.set(null);

    try {
      // Voice note is uploaded first (no ticket exists yet) so its storage
      // key can be included directly in the submit call — the recording
      // ends up attached to the ticket from the moment it's created,
      // matching "record, then submit with that as extra context".
      let voiceNote: { storageKey: string; fileName: string } | undefined;
      const note = this.recordedNote();
      if (note) {
        try {
          voiceNote = await this.ticketsSvc.uploadVoiceNote(note, 'voice-note.webm');
        } catch {
          this.uploadError.set('Your voice note failed to upload — submitting without it. You can try recording again next time.');
        }
      }

      const ticket = await this.ticketsSvc.submitFromClient(
        client.id, agreement.id, this.systemProductId(), this.description().trim(), this.category(), this.failureTypeId() || undefined, voiceNote,
        this.supportTypeId() || undefined, this.acknowledged()
      );

      const file = this.selectedFile();
      if (file) {
        try {
          await this.ticketsSvc.uploadAttachment(ticket.id, file);
        } catch {
          this.uploadError.set('Your issue was submitted, but the attachment failed to upload. You can try attaching it again later.');
        }
      }

      this.submittedId.set(ticket.id);
      this.description.set('');
      this.systemProductId.set('');
      this.failureTypeId.set('');
      this.supportTypeId.set('');
      this.acknowledged.set(false);
      this.selectedFile.set(null);
      this.discardRecording();
    } finally {
      this.submitting.set(false);
    }
  }
}

/** Formats a remaining-time span as a human countdown (e.g. "2d 04h", "03:12:45"). */
export function formatRemaining(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return days > 0
    ? `${days}d ${pad(hours)}h ${pad(minutes)}m`
    : `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}
