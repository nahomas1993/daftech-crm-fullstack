import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClientImportService } from '../../core/services/client-import.service';
import { ClientImportResult, ClientImportRowResult } from '../../core/models';

/**
 * Bulk-imports old, paper-based client records from a CSV — for
 * migrating the hundreds of existing clients that predate this system.
 * See the backend's ClientImportService for the full import behavior;
 * this page is just: download a template, upload it filled in, read the
 * per-row report.
 */
@Component({
  selector: 'app-client-import',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="header-row">
      <div>
        <h1>Import Clients</h1>
        <p class="text-muted" style="margin-top:0.3rem;">
          Bulk-add old, paper-based client records — client, systems/products, agreements, and training status — from a CSV.
        </p>
      </div>
      <a routerLink="/admin/clients" class="btn btn-outline">Back to Clients</a>
    </div>

    <div class="panel panel-pad" style="max-width: 760px;">
      <h3>1. Download the template</h3>
      <p class="text-muted" style="font-size:0.85rem;">
        Every column is explained by its example row. Don't rename, remove, or reorder the columns — add data below the
        two example rows (or replace them) and re-upload. One row = one client's one system/product; if a client has
        several, give them one row per system/product with the same client details repeated on each row.
      </p>
      <ul class="text-muted" style="font-size:0.82rem; line-height:1.6; margin: 0.5rem 0 0.9rem 1.1rem;">
        <li>Leave <span class="mono">AgreementType</span> blank to skip creating an agreement for that row — add it later from the client's page instead. In practice this will almost always be <span class="mono">Support</span> — Training is no longer tracked as its own agreement, that's what the <span class="mono">TrainingCompleted</span> column is for.</li>
        <li>A <span class="mono">Support</span> agreement can't be created unless <span class="mono">TrainingCompleted</span> is <span class="mono">Yes</span> on that row.</li>
        <li>Dates use <span class="mono">YYYY-MM-DD</span>. <span class="mono">BillingTier</span> is <span class="mono">Basic</span>, <span class="mono">Intermediate</span>, or <span class="mono">Advanced</span>.</li>
        <li>Real login credentials are created for each new client, but no email is sent automatically — send each one individually from the client's page when you're ready, using "Resend credential email".</li>
      </ul>
      <button class="btn btn-outline" [disabled]="downloadingTemplate()" (click)="downloadTemplate()">
        {{ downloadingTemplate() ? 'Preparing…' : 'Download CSV Template' }}
      </button>
    </div>

    <div class="panel panel-pad" style="max-width: 760px; margin-top:1.25rem;">
      <h3>2. Upload the filled-in CSV</h3>
      <div style="display:flex; align-items:center; gap:0.75rem; margin-top:0.6rem; flex-wrap:wrap;">
        <input type="file" accept=".csv" (change)="onFileSelected($event)" #fileInput />
        <button class="btn btn-primary" [disabled]="!selectedFile() || importing()" (click)="runImport()">
          {{ importing() ? 'Importing…' : 'Import' }}
        </button>
      </div>
      @if (selectedFile()) {
        <p class="text-muted" style="font-size:0.8rem; margin-top:0.5rem;">Selected: {{ selectedFile()!.name }}</p>
      }
      @if (uploadError()) {
        <div class="err" style="margin-top:0.75rem;">{{ uploadError() }}</div>
      }
    </div>

    @if (result(); as r) {
      <div class="panel panel-pad" style="max-width: 1000px; margin-top:1.25rem;">
        <h3>Results</h3>
        <div class="summary-row">
          <div class="summary-chip"><strong>{{ r.totalRows }}</strong><span>Rows in file</span></div>
          <div class="summary-chip summary-good"><strong>{{ r.succeededCount }}</strong><span>Imported</span></div>
          <div class="summary-chip summary-bad"><strong>{{ r.failedCount }}</strong><span>Failed</span></div>
          <div class="summary-chip summary-warn"><strong>{{ r.flaggedDuplicateCount }}</strong><span>Flagged (possible duplicate)</span></div>
        </div>

        @if (r.failedCount > 0 || r.flaggedDuplicateCount > 0) {
          <h4 style="margin-top:1.25rem;">Rows that need attention</h4>
          <table class="report-table">
            <thead><tr><th>Row</th><th>Client</th><th>System/Product</th><th>Issue</th></tr></thead>
            <tbody>
              @for (row of problemRows(); track row.rowNumber) {
                <tr>
                  <td class="mono">{{ row.rowNumber }}</td>
                  <td>{{ row.clientName }}</td>
                  <td>{{ row.systemProductName }}</td>
                  <td [class.warn-text]="row.flaggedAsDuplicate">{{ row.error }}</td>
                </tr>
              }
            </tbody>
          </table>
          <p class="text-muted" style="font-size:0.8rem; margin-top:0.6rem;">
            Fix these rows in the CSV (or handle duplicates manually) and re-upload just those rows — rows that already
            succeeded won't be duplicated as long as you remove them from the file first.
          </p>
        }

        @if (r.succeededCount > 0) {
          <h4 style="margin-top:1.25rem;">Imported successfully</h4>
          <table class="report-table">
            <thead><tr><th>Row</th><th>Client</th><th>System/Product</th><th>Username issued</th></tr></thead>
            <tbody>
              @for (row of successRows(); track row.rowNumber) {
                <tr>
                  <td class="mono">{{ row.rowNumber }}</td>
                  <td>{{ row.clientName }}</td>
                  <td>{{ row.systemProductName }}</td>
                  <td class="mono text-muted">{{ row.issuedUsername ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }
  `,
  styles: [`
    .header-row { display:flex; justify-content:space-between; align-items:flex-start; gap:1rem; margin-bottom:1.25rem; flex-wrap:wrap; }
    h3 { margin: 0 0 0.4rem; }
    h4 { margin: 0 0 0.6rem; font-size: 0.95rem; }
    .err { color: var(--red, #b3261e); font-size: 0.85rem; }
    .summary-row { display:flex; gap:0.75rem; flex-wrap:wrap; margin-top:0.75rem; }
    .summary-chip { display:flex; flex-direction:column; align-items:center; justify-content:center; border:1px solid var(--slate-200); border-radius:10px; padding:0.7rem 1.1rem; min-width:110px; }
    .summary-chip strong { font-size:1.3rem; }
    .summary-chip span { font-size:0.72rem; color: var(--slate-500); margin-top:0.15rem; }
    .summary-good strong { color: var(--green, #1a7f37); }
    .summary-bad strong { color: var(--red, #b3261e); }
    .summary-warn strong { color: var(--amber, #b45309); }
    .report-table { width:100%; border-collapse: collapse; margin-top:0.5rem; font-size:0.85rem; }
    .report-table th { text-align:left; font-size:0.72rem; text-transform:uppercase; letter-spacing:0.02em; color: var(--slate-500); padding:0.4rem 0.6rem; border-bottom:1px solid var(--slate-200); }
    .report-table td { padding:0.5rem 0.6rem; border-bottom:1px solid var(--slate-100); vertical-align:top; }
    .warn-text { color: var(--amber, #b45309); }
  `],
})
export class ClientImportComponent {
  selectedFile = signal<File | null>(null);
  importing = signal(false);
  downloadingTemplate = signal(false);
  uploadError = signal('');
  result = signal<ClientImportResult | null>(null);

  constructor(private importer: ClientImportService) {}

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
    this.uploadError.set('');
    this.result.set(null);
  }

  async downloadTemplate() {
    this.downloadingTemplate.set(true);
    try {
      await this.importer.downloadTemplate();
    } catch {
      this.uploadError.set('Could not download the template — please try again.');
    } finally {
      this.downloadingTemplate.set(false);
    }
  }

  async runImport() {
    const file = this.selectedFile();
    if (!file) return;

    this.importing.set(true);
    this.uploadError.set('');
    this.result.set(null);
    try {
      const result = await this.importer.import(file);
      this.result.set(result);
    } catch (err: any) {
      this.uploadError.set(err?.error?.error ?? err?.error ?? 'Import failed — check the file and try again.');
    } finally {
      this.importing.set(false);
    }
  }

  problemRows(): ClientImportRowResult[] {
    return this.result()?.rows.filter(r => !r.success) ?? [];
  }

  successRows(): ClientImportRowResult[] {
    return this.result()?.rows.filter(r => r.success) ?? [];
  }
}
