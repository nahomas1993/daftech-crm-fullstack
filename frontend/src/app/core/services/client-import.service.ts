import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ClientImportResult } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Bulk-imports old, paper-based client records from a CSV — see the
 * backend's ClientImportController/ClientImportService for the full
 * behavior. Used from the "Import Clients" admin page rather than the
 * normal one-at-a-time registration flow, for migrating hundreds of
 * existing clients in one upload.
 */
@Injectable({ providedIn: 'root' })
export class ClientImportService {
  constructor(private http: HttpClient) {}

  /** Triggers a browser download of the starting CSV template with the exact expected columns and two filled-in example rows. */
  async downloadTemplate(): Promise<void> {
    const blob = await firstValueFrom(
      this.http.get(`${API_BASE_URL}/client-import/template`, { responseType: 'blob' })
    );
    this.saveBlob(blob, 'daftech-client-import-template.csv');
  }

  /** Uploads the filled-in CSV and returns the full per-row report — always resolves (even if every row failed); only throws for file-level problems like missing columns. */
  async import(file: File): Promise<ClientImportResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(this.http.post<ClientImportResult>(`${API_BASE_URL}/client-import`, form));
  }

  private saveBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }
}
