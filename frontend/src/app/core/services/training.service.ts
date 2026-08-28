import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MyTrainingAssignment, TrainingRecord } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * The open-ended training activity log — see TrainingRecord. A Trainer
 * logs one of these each time they actually conduct a session ("Add
 * Training" on their own page); there is no submit/approve lifecycle per
 * record. Admin reviews the accumulated set informally, then marks the
 * whole SystemProduct's training Completed via SystemProductService —
 * that decision lives there, not here, since it's a property of the
 * system/product as a whole, not of any one record.
 */
@Injectable({ providedIn: 'root' })
export class TrainingService {
  constructor(private http: HttpClient) {}

  /**
   * "Add Training": logs one session the calling Trainer conducted
   * against data.systemProductId, for a specific admin-configured
   * checklist item (data.agreementTypeId — e.g. "Attendance"), with the
   * date and what was taught/conducted. startDateTime/endDateTime are
   * optional — leave both out for items with no real duration. Only a
   * Trainer currently on that system/product's training roster may do
   * this (403 otherwise — the roster is set up first, via
   * SystemProductService.addTrainingAssignment/autoAssignTrainers).
   * Attach a file afterward with uploadFile() if needed. Call again for
   * each additional item on the checklist (or a repeat/refresher) —
   * every call inserts a new record.
   */
  async create(data: {
    systemProductId: string;
    agreementTypeId: string;
    trainingDate: string;
    startDateTime?: string | null;
    endDateTime?: string | null;
    description: string;
  }): Promise<TrainingRecord> {
    return firstValueFrom(this.http.post<TrainingRecord>(`${API_BASE_URL}/training`, data));
  }

  /** The system/products Admin has assigned the logged-in Trainer to train on — the only valid targets for create(). Trainers never pick a client themselves. */
  async getMyAssignments(): Promise<MyTrainingAssignment[]> {
    return firstValueFrom(this.http.get<MyTrainingAssignment[]>(`${API_BASE_URL}/training/my-assignments`));
  }

  /** The logged-in Trainer's own training records across every system/product — the "My Trainings" list. */
  async getMyRecords(): Promise<TrainingRecord[]> {
    return firstValueFrom(this.http.get<TrainingRecord[]>(`${API_BASE_URL}/training/my-records`));
  }

  /** Every training record logged against one system/product — newest first. The log Admin reviews before deciding to mark training Completed. Reachable from the Client, SystemProduct, and Agreement detail pages. */
  async getForSystemProduct(systemProductId: string): Promise<TrainingRecord[]> {
    return firstValueFrom(this.http.get<TrainingRecord[]>(`${API_BASE_URL}/training/system-product/${systemProductId}`));
  }

  /** Uploads (or replaces) the supporting file for one training record. Only the trainer who logged it may attach/replace its file. */
  async uploadFile(recordId: string, file: File): Promise<TrainingRecord> {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(this.http.post<TrainingRecord>(`${API_BASE_URL}/training/${recordId}/file`, form));
  }

  /** Fetches a training record's supporting file as a Blob. */
  async downloadFile(recordId: string): Promise<Blob> {
    return firstValueFrom(this.http.get(`${API_BASE_URL}/training/${recordId}/file`, { responseType: 'blob' }));
  }
}
