import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

export type FilePreviewKind = 'audio' | 'image' | 'pdf' | 'other';

/**
 * Shows a ticket's attachment or voice note inline — audio plays with a
 * native <audio> element, images/PDFs render in an <iframe>/<img>, and
 * anything else falls back to "open in a new tab" (never a forced
 * download link). Blob URL is revoked when the modal closes so we don't
 * leak memory across many opens in one session.
 */
@Component({
  selector: 'app-file-preview-modal',
  standalone: true,
  template: `
    @if (open) {
      <div class="overlay" (click)="handleClose()">
        <div class="modal panel panel-pad" (click)="$event.stopPropagation()">
          <div class="modal-head">
            <h3>{{ title }}</h3>
            <button type="button" class="btn btn-outline btn-sm" (click)="handleClose()">Close</button>
          </div>

          @if (loading()) {
            <p class="text-muted" style="padding: 1.5rem 0; text-align:center;">Loading…</p>
          } @else if (error()) {
            <p class="err">{{ error() }}</p>
          } @else if (kind === 'audio' && safeUrl()) {
            <audio controls autoplay style="width:100%; margin-top:0.75rem;" [src]="safeUrl()!"></audio>
          } @else if (kind === 'image' && safeUrl()) {
            <img [src]="safeUrl()!" [alt]="fileName" style="max-width:100%; max-height:70vh; display:block; margin:0.75rem auto 0; border-radius:8px;" />
          } @else if (kind === 'pdf' && sanitizedFrameUrl()) {
            <iframe [src]="sanitizedFrameUrl()!" style="width:100%; height:70vh; border:0; margin-top:0.75rem; border-radius:8px;"></iframe>
          } @else if (safeUrl()) {
            <p class="text-muted" style="margin-top:0.75rem;">
              This file type can't be previewed here.
              <a [href]="safeUrl()!" target="_blank" rel="noopener">Open in a new tab</a> to view it.
            </p>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .overlay {
      position: fixed; inset: 0; background: rgba(15, 23, 42, 0.45); z-index: 100;
      display: flex; align-items: center; justify-content: center; padding: 1rem;
    }
    .modal { width: 560px; max-width: 100%; }
    .modal-head { display:flex; align-items:center; justify-content:space-between; gap: 1rem; }
    .modal-head h3 { margin: 0; word-break: break-word; }
    .err { margin-top: 0.75rem; padding: 0.6rem 0.75rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.85rem; }
  `],
})
export class FilePreviewModalComponent implements OnChanges {
  @Input() open = false;
  @Input() title = 'Preview';
  @Input() fileName = '';
  @Input() kind: FilePreviewKind = 'other';
  /** Async loader supplied by the parent (e.g. `() => tickets.downloadVoiceNote(id)`); called each time the modal opens. */
  @Input() load?: () => Promise<Blob>;
  @Output() closed = new EventEmitter<void>();

  loading = signal(false);
  error = signal<string | null>(null);
  private objectUrl: string | null = null;
  private _safeUrl = signal<string | null>(null);
  safeUrl = this._safeUrl.asReadonly();

  constructor(private sanitizer: DomSanitizer) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']) {
      if (this.open) {
        void this.fetchAndShow();
      } else {
        this.cleanup();
      }
    }
  }

  sanitizedFrameUrl(): SafeResourceUrl | null {
    const url = this._safeUrl();
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  }

  private async fetchAndShow() {
    this.cleanup();
    this.error.set(null);
    this.loading.set(true);
    try {
      if (!this.load) throw new Error('No loader provided.');
      const blob = await this.load();
      this.objectUrl = URL.createObjectURL(blob);
      this._safeUrl.set(this.objectUrl);
    } catch (err: any) {
      // Surface the real cause where we can — a stale/expired session
      // (401, or a 403 the auth interceptor's silent refresh already gave
      // up on) needs a different fix (re-login) than the file genuinely
      // being gone (404), and a bare "please try again" hides that
      // distinction from whoever's debugging a report of this happening.
      if (err?.status === 401 || err?.status === 403) {
        this.error.set('Your session has expired — please refresh the page and sign in again to view this file.');
      } else if (err?.status === 404) {
        this.error.set('This file could not be found — it may have been removed.');
      } else {
        this.error.set('Could not load this file — please try again.');
      }
    } finally {
      this.loading.set(false);
    }
  }

  handleClose() {
    this.cleanup();
    this.closed.emit();
  }

  private cleanup() {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
    this._safeUrl.set(null);
  }
}

/** Infer preview kind from a filename's extension, for choosing how to render it in the modal. */
export function filePreviewKindFor(fileName: string | undefined | null): FilePreviewKind {
  const ext = (fileName ?? '').split('.').pop()?.toLowerCase() ?? '';
  if (['mp3', 'wav', 'webm', 'ogg', 'm4a', 'aac'].includes(ext)) return 'audio';
  if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg'].includes(ext)) return 'image';
  if (ext === 'pdf') return 'pdf';
  return 'other';
}
