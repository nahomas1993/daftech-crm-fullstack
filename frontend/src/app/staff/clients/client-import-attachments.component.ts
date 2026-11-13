import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ClientService } from '../../core/services/client.service';
import { SystemProductService } from '../../core/services/system-product.service';
import { AgreementService } from '../../core/services/agreement.service';
import { TrainingService } from '../../core/services/training.service';
import { EmployeeService } from '../../core/services/employee.service';
import { AgreementTypeService } from '../../core/services/agreement-type.service';
import { BadgeComponent } from '../../shared/badge.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { FilePreviewModalComponent, filePreviewKindFor, FilePreviewKind } from '../../shared/file-preview-modal.component';
import { Agreement, SystemProduct, TrainingRecord } from '../../core/models';

/**
 * Reconciliation screen for clients brought in via the CSV bulk import
 * (see ClientImportComponent) — the importer creates the Client,
 * SystemProduct, and (if AgreementType was filled in) the Agreement row,
 * but it can never carry an actual scanned file, since a CSV cell can't
 * hold binary content (see ClientImportService — every imported
 * Agreement's ScannedFileUrl starts null). This page is where an Admin
 * comes back afterward and attaches those files, one client at a time:
 * pick a client (searchable, paginated — there may be hundreds after a
 * bulk import), their Products load automatically, and each Product shows
 * its one Support Agreement plus every logged Training session by name,
 * each with its own upload/replace-file action.
 *
 * Not exclusive to imported clients — any client's agreement/training
 * files can be managed here, since the underlying upload endpoints are
 * the same ones the Client Detail page uses (AgreementService.uploadScannedFile,
 * TrainingService.uploadFile). Training-file upload is Admin-or-owning-Trainer
 * here (see TrainingRecordService.UploadFileAsync's callerIsAdmin param) —
 * a deliberate relaxation of the normal Trainer-only rule specifically so
 * an Admin can attach scans of paper training sign-off sheets that were
 * never captured by the Trainer at the time.
 */
@Component({
  selector: 'app-client-import-attachments',
  standalone: true,
  imports: [FormsModule, RouterLink, BadgeComponent, PaginationComponent, FilePreviewModalComponent],
  template: `
    <div class="header-row">
      <div>
        <h1>Upload Attachments</h1>
        <p class="text-muted" style="margin-top:0.3rem;">
          Attach scanned agreements and training files for any client — most useful right after a
          <a routerLink="/admin/clients/import">CSV import</a>, since those rows are created without files.
        </p>
      </div>
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem; max-width: 480px;">
      <div class="field">
        <label>Find Client</label>
        <input
          type="text"
          placeholder="Search by name, email, or phone…"
          [ngModel]="searchTerm()"
          (ngModelChange)="onSearchChange($event)"
        />
      </div>

      @if (clients.pagedClients().length === 0 && !searching()) {
        <p class="text-muted" style="margin-top:0.75rem; font-size:0.85rem;">No clients found.</p>
      } @else {
        <select
          style="margin-top:0.75rem; width:100%;"
          [ngModel]="selectedClientId()"
          (ngModelChange)="selectClient($event)"
        >
          <option value="">Select a client…</option>
          @for (c of clients.pagedClients(); track c.id) {
            <option [value]="c.id">{{ c.name }}</option>
          }
        </select>

        <app-pagination
          style="margin-top:0.5rem; display:block;"
          [page]="clients.page()"
          [totalPages]="clients.totalPages()"
          [totalCount]="clients.totalCount()"
          [pageSize]="clients.pageSize()"
          (pageChange)="onPageChange($event)">
        </app-pagination>
      }
    </div>

    @if (selectedClientId()) {
      @if (loadingProducts()) {
        <p class="text-muted" style="margin-top:1.25rem;">Loading products…</p>
      } @else if (products().length === 0) {
        <p class="text-muted" style="margin-top:1.25rem;">This client has no Systems/Products yet.</p>
      } @else {
        <div style="margin-top:1.25rem; display:flex; flex-direction:column; gap:1rem;">
          @for (sp of products(); track sp.id) {
            <div class="panel panel-pad">
              <div class="header-row">
                <h3 style="margin:0;">📦 {{ sp.name }}</h3>
                <app-badge [status]="sp.trainingCompletionStatus"></app-badge>
              </div>

              <p class="text-muted section-label">Support Agreement</p>
              @if (agreementsFor(sp.id).length === 0) {
                <p class="text-muted" style="font-size:0.85rem;">No agreement recorded for this product yet.</p>
              } @else {
                <ul class="entry-list">
                  @for (a of agreementsFor(sp.id); track a.id) {
                    <li class="entry-row">
                      <span class="entry-name">
                        {{ a.agreementTypeName }} · Doc {{ a.documentNumber }}
                        @if (a.scannedFileUrl) { <span class="ok-tag">✓ file attached</span> }
                      </span>
                      <div class="entry-actions">
                        @if (a.scannedFileUrl) {
                          <button class="btn btn-outline btn-sm" (click)="viewAgreementFile(a)">View</button>
                        }
                        <label class="btn btn-outline btn-sm upload-label">
                          {{ a.scannedFileUrl ? 'Replace file' : 'Upload file' }}
                          <input type="file" (change)="onAgreementFileSelected($event, a.id)" [disabled]="uploadingAgreementId() === a.id" />
                        </label>
                        @if (uploadingAgreementId() === a.id) { <span class="text-muted" style="font-size:0.78rem;">Uploading…</span> }
                      </div>
                    </li>
                  }
                </ul>
              }

              <div class="header-row" style="align-items:center;">
                <p class="text-muted section-label" style="margin:0;">Training Sessions</p>
                <button class="btn btn-outline btn-sm" (click)="toggleLogTrainingForm(sp.id)">
                  {{ logTrainingFormOpenFor() === sp.id ? 'Cancel' : '+ Log Training Session' }}
                </button>
              </div>

              @if (logTrainingFormOpenFor() === sp.id) {
                <div class="log-training-form">
                  <div class="form-grid">
                    <div class="field">
                      <label>Trainer <span class="req">*</span></label>
                      <select [ngModel]="logTrainingForm.trainerEmployeeId" (ngModelChange)="logTrainingForm.trainerEmployeeId = $event">
                        <option value="">Select trainer…</option>
                        @for (t of trainerEmployees(); track t.id) {
                          <option [value]="t.id">{{ t.fullName }}</option>
                        }
                      </select>
                      @if (trainerEmployees().length === 0) {
                        <span class="text-muted" style="font-size:0.75rem;">No employees have the Trainer role yet — add one under Employees first.</span>
                      }
                    </div>
                    <div class="field">
                      <label>Training Item <span class="req">*</span></label>
                      <select [ngModel]="logTrainingForm.agreementTypeId" (ngModelChange)="logTrainingForm.agreementTypeId = $event">
                        <option value="">Select item…</option>
                        @for (t of agreementTypesSvc.types(); track t.id) {
                          <option [value]="t.id">{{ t.name }}</option>
                        }
                      </select>
                    </div>
                    <div class="field">
                      <label>Date <span class="req">*</span></label>
                      <input type="date" [ngModel]="logTrainingForm.trainingDate" (ngModelChange)="logTrainingForm.trainingDate = $event" />
                    </div>
                    <div class="field">
                      <label>Attachment (optional)</label>
                      <input type="file" (change)="onLogTrainingFileSelected($event)" />
                      @if (logTrainingFile()) { <span class="text-muted" style="font-size:0.75rem;">{{ logTrainingFile()!.name }}</span> }
                    </div>
                    <div class="field" style="grid-column: 1 / -1;">
                      <label>Description <span class="req">*</span></label>
                      <textarea rows="2" [ngModel]="logTrainingForm.description" (ngModelChange)="logTrainingForm.description = $event" placeholder="What was taught/conducted"></textarea>
                    </div>
                  </div>
                  @if (logTrainingError()) { <p class="upload-error" style="margin-top:0.6rem;">{{ logTrainingError() }}</p> }
                  <button class="btn btn-primary btn-sm" style="margin-top:0.75rem;" [disabled]="loggingTraining()" (click)="submitLogTraining(sp.id)">
                    {{ loggingTraining() ? 'Saving…' : 'Save Training Session' }}
                  </button>
                </div>
              }

              @if (recordsFor(sp.id).length === 0) {
                <p class="text-muted" style="font-size:0.85rem; margin-top:0.5rem;">No training sessions logged for this product yet.</p>
              } @else {
                <ul class="entry-list">
                  @for (r of recordsFor(sp.id); track r.id) {
                    <li class="entry-row">
                      <span class="entry-name">
                        {{ r.agreementTypeName }} · {{ r.trainingDate }} · {{ r.trainerEmployeeName }}
                        @if (r.fileName) { <span class="ok-tag">✓ {{ r.fileName }}</span> }
                      </span>
                      <div class="entry-actions">
                        @if (r.fileName) {
                          <button class="btn btn-outline btn-sm" (click)="viewRecordFile(r)">View</button>
                        }
                        <label class="btn btn-outline btn-sm upload-label">
                          {{ r.fileName ? 'Replace file' : 'Upload file' }}
                          <input type="file" (change)="onRecordFileSelected($event, r.id)" [disabled]="uploadingRecordId() === r.id" />
                        </label>
                        @if (uploadingRecordId() === r.id) { <span class="text-muted" style="font-size:0.78rem;">Uploading…</span> }
                      </div>
                    </li>
                  }
                </ul>
              }
            </div>
          }
        </div>

        <p class="text-muted" style="margin-top:1rem; font-size:0.82rem;">
          {{ completionSummary() }}
        </p>
      }

      @if (uploadError()) { <div class="err" style="margin-top:0.75rem;">{{ uploadError() }}</div> }
    }

    <app-file-preview-modal
      [open]="previewOpen"
      [title]="previewTitle"
      [fileName]="previewFileName"
      [kind]="previewKind"
      [load]="previewLoader"
      (closed)="closePreview()">
    </app-file-preview-modal>
  `,
  styles: [`
    .header-row { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 1rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field label { font-size: 0.76rem; font-weight: 600; color: var(--slate-500); }
    .section-label { font-size: 0.76rem; margin: 1rem 0 0.4rem; text-transform: uppercase; letter-spacing: 0.02em; }
    .entry-list { list-style: none; margin: 0; padding: 0; }
    .entry-row {
      display: flex; justify-content: space-between; align-items: center; gap: 0.75rem;
      padding: 0.55rem 0; border-bottom: 1px solid var(--slate-100, #f1f5f9); flex-wrap: wrap;
    }
    .entry-row:last-child { border-bottom: none; }
    .entry-name { font-size: 0.86rem; }
    .entry-actions { display: flex; align-items: center; gap: 0.5rem; flex-shrink: 0; }
    .ok-tag { color: var(--green, #16a34a); font-size: 0.78rem; margin-left: 0.5rem; font-weight: 600; }
    .upload-label { position: relative; overflow: hidden; cursor: pointer; }
    .upload-label input[type="file"] {
      position: absolute; inset: 0; opacity: 0; cursor: pointer; width: 100%;
    }
    .err { padding: 0.6rem 0.75rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.85rem; }
    .log-training-form { margin-top: 0.6rem; padding: 0.85rem; border: 1px solid var(--slate-200); border-radius: 10px; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 0.75rem; }
    .field label .req { color: var(--red, #b3261e); }
    .upload-error { color: var(--red); font-size: 0.85rem; }
  `],
})
export class ClientImportAttachmentsComponent {
  constructor(
    public clients: ClientService,
    private systemProductsSvc: SystemProductService,
    private agreementsSvc: AgreementService,
    private trainingSvc: TrainingService,
    private employeesSvc: EmployeeService,
    public agreementTypesSvc: AgreementTypeService,
  ) {}

  searchTerm = signal('');
  searching = signal(false);
  private searchDebounce?: ReturnType<typeof setTimeout>;

  selectedClientId = signal<string>('');
  loadingProducts = signal(false);
  uploadError = signal<string | null>(null);
  uploadingAgreementId = signal<string | null>(null);
  uploadingRecordId = signal<string | null>(null);

  private _products = signal<SystemProduct[]>([]);
  products = this._products.asReadonly();
  private _agreementsByProduct = signal<Record<string, Agreement[]>>({});
  private _recordsByProduct = signal<Record<string, TrainingRecord[]>>({});

  agreementsFor(systemProductId: string): Agreement[] {
    return this._agreementsByProduct()[systemProductId] ?? [];
  }

  recordsFor(systemProductId: string): TrainingRecord[] {
    return this._recordsByProduct()[systemProductId] ?? [];
  }

  /** Active employees with the Trainer role — the only valid picks for the Log Training Session form's Trainer dropdown (see TrainingRecordService.AdminCreateAsync's role check). */
  trainerEmployees = computed(() => this.employeesSvc.activeEmployees().filter(e => e.roles.includes('Trainer')));

  logTrainingFormOpenFor = signal<string | null>(null);
  loggingTraining = signal(false);
  logTrainingError = signal<string | null>(null);
  logTrainingFile = signal<File | null>(null);
  logTrainingForm = this.blankLogTrainingForm();

  private blankLogTrainingForm() {
    return { trainerEmployeeId: '', agreementTypeId: '', trainingDate: new Date().toISOString().slice(0, 10), description: '' };
  }

  toggleLogTrainingForm(systemProductId: string) {
    if (this.logTrainingFormOpenFor() === systemProductId) {
      this.logTrainingFormOpenFor.set(null);
      return;
    }
    this.logTrainingFormOpenFor.set(systemProductId);
    this.logTrainingForm = this.blankLogTrainingForm();
    this.logTrainingFile.set(null);
    this.logTrainingError.set(null);
  }

  onLogTrainingFileSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    this.logTrainingFile.set(file ?? null);
  }

  async submitLogTraining(systemProductId: string) {
    const form = this.logTrainingForm;
    if (!form.trainerEmployeeId || !form.agreementTypeId || !form.trainingDate || !form.description.trim()) {
      this.logTrainingError.set('Trainer, Training Item, Date, and Description are all required.');
      return;
    }

    this.loggingTraining.set(true);
    this.logTrainingError.set(null);
    try {
      const created = await this.trainingSvc.adminCreate({
        trainerEmployeeId: form.trainerEmployeeId,
        systemProductId,
        agreementTypeId: form.agreementTypeId,
        trainingDate: form.trainingDate,
        description: form.description,
      });

      const file = this.logTrainingFile();
      if (file) {
        await this.trainingSvc.uploadFile(created.id, file);
      }

      await this.refreshProductFiles(systemProductId);
      this.logTrainingFormOpenFor.set(null);
    } catch (err: any) {
      this.logTrainingError.set(err?.error ?? 'Could not save this training session — please try again.');
      console.error(err);
    } finally {
      this.loggingTraining.set(false);
    }
  }

  completionSummary = computed(() => {
    const prods = this.products();
    let agreementsTotal = 0, agreementsDone = 0, recordsTotal = 0, recordsDone = 0;
    for (const sp of prods) {
      for (const a of this.agreementsFor(sp.id)) { agreementsTotal++; if (a.scannedFileUrl) agreementsDone++; }
      for (const r of this.recordsFor(sp.id)) { recordsTotal++; if (r.fileName) recordsDone++; }
    }
    return `Agreements: ${agreementsDone}/${agreementsTotal} have a file attached · Training sessions: ${recordsDone}/${recordsTotal} have a file attached.`;
  });

  onSearchChange(term: string) {
    this.searchTerm.set(term);
    this.searching.set(true);
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(async () => {
      await this.clients.search(term);
      this.searching.set(false);
    }, 300);
  }

  async onPageChange(page: number) {
    await this.clients.refreshPaged(page, this.clients.pageSize(), this.searchTerm());
  }

  async selectClient(clientId: string) {
    this.selectedClientId.set(clientId);
    this.uploadError.set(null);
    this._products.set([]);
    this._agreementsByProduct.set({});
    this._recordsByProduct.set({});
    if (!clientId) return;

    this.loadingProducts.set(true);
    try {
      const products = await this.systemProductsSvc.refreshForClient(clientId);
      this._products.set(products);

      // Products' Agreements/TrainingRecords aren't included on the
      // SystemProduct payload itself — fetch each product's own lists in
      // parallel rather than one round trip per product sequentially.
      const [agreementLists, recordLists] = await Promise.all([
        Promise.all(products.map(sp => this.agreementsSvc.fetchForSystemProduct(sp.id))),
        Promise.all(products.map(sp => this.trainingSvc.getForSystemProduct(sp.id))),
      ]);

      const agreementsMap: Record<string, Agreement[]> = {};
      const recordsMap: Record<string, TrainingRecord[]> = {};
      products.forEach((sp, i) => {
        agreementsMap[sp.id] = agreementLists[i];
        recordsMap[sp.id] = recordLists[i];
      });
      this._agreementsByProduct.set(agreementsMap);
      this._recordsByProduct.set(recordsMap);
    } catch (err) {
      this.uploadError.set('Could not load this client\'s products — please try again.');
      console.error(err);
    } finally {
      this.loadingProducts.set(false);
    }
  }

  private async refreshProductFiles(systemProductId: string) {
    const [agreements, records] = await Promise.all([
      this.agreementsSvc.fetchForSystemProduct(systemProductId),
      this.trainingSvc.getForSystemProduct(systemProductId),
    ]);
    this._agreementsByProduct.update(map => ({ ...map, [systemProductId]: agreements }));
    this._recordsByProduct.update(map => ({ ...map, [systemProductId]: records }));
  }

  async onAgreementFileSelected(event: Event, agreementId: string) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file later (e.g. after a failed attempt)
    if (!file) return;

    const systemProductId = this.products().find(sp => this.agreementsFor(sp.id).some(a => a.id === agreementId))?.id;
    this.uploadError.set(null);
    this.uploadingAgreementId.set(agreementId);
    try {
      await this.agreementsSvc.uploadScannedFile(agreementId, file);
      if (systemProductId) await this.refreshProductFiles(systemProductId);
    } catch (err) {
      this.uploadError.set('Could not upload this agreement file — please try again.');
      console.error(err);
    } finally {
      this.uploadingAgreementId.set(null);
    }
  }

  async onRecordFileSelected(event: Event, recordId: string) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    const systemProductId = this.products().find(sp => this.recordsFor(sp.id).some(r => r.id === recordId))?.id;
    this.uploadError.set(null);
    this.uploadingRecordId.set(recordId);
    try {
      await this.trainingSvc.uploadFile(recordId, file);
      if (systemProductId) await this.refreshProductFiles(systemProductId);
    } catch (err) {
      this.uploadError.set('Could not upload this training file — please try again.');
      console.error(err);
    } finally {
      this.uploadingRecordId.set(null);
    }
  }

  // Shown inline in the shared preview modal (audio player / image / PDF
  // viewer) rather than forcing a download — see FilePreviewModalComponent.
  previewOpen = false;
  previewTitle = 'Preview';
  previewFileName = '';
  previewKind: FilePreviewKind = 'other';
  previewLoader?: () => Promise<Blob>;

  viewAgreementFile(a: Agreement) {
    this.previewTitle = 'Scanned Agreement';
    this.previewFileName = a.scannedFileUrl ?? '';
    this.previewKind = filePreviewKindFor(a.scannedFileUrl);
    this.previewLoader = () => this.agreementsSvc.downloadScannedFile(a.id);
    this.previewOpen = true;
  }

  viewRecordFile(r: TrainingRecord) {
    this.previewTitle = 'Training Record File';
    this.previewFileName = r.fileName ?? '';
    this.previewKind = filePreviewKindFor(r.fileName);
    this.previewLoader = () => this.trainingSvc.downloadFile(r.id);
    this.previewOpen = true;
  }

  closePreview() {
    this.previewOpen = false;
  }
}
