import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SystemProduct, TrainerAssignmentRecommendation } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Manages the SystemProduct layer between Client and Agreement:
 * Client -> SystemProduct -> Agreement -> AgreementType. A client can have
 * multiple systems/products; creating a new one never replaces an
 * existing one. Also manages the training workflow that lives directly on
 * SystemProduct — the assigned-trainer roster and the one-click "mark
 * training Completed" decision. See TrainingService for the open-ended
 * log of training sessions actually conducted against a system/product.
 */
@Injectable({ providedIn: 'root' })
export class SystemProductService {
  // Keyed by clientId so switching between clients on the Client Detail
  // page doesn't require a network round-trip every time you go back.
  private readonly _byClient = signal<Record<string, SystemProduct[]>>({});
  readonly byClient = this._byClient.asReadonly();

  constructor(private http: HttpClient) {}

  systemProductsFor(clientId: string): SystemProduct[] {
    return this._byClient()[clientId] ?? [];
  }

  async refreshForClient(clientId: string): Promise<SystemProduct[]> {
    const list = await firstValueFrom(this.http.get<SystemProduct[]>(`${API_BASE_URL}/system-products/client/${clientId}`));
    this._byClient.update(map => ({ ...map, [clientId]: list }));
    return list;
  }

  async getById(id: string): Promise<SystemProduct> {
    return firstValueFrom(this.http.get<SystemProduct>(`${API_BASE_URL}/system-products/${id}`));
  }

  /** Creates a new system/product for a client. Never overwrites or replaces one the client already has. Starts with an empty training roster — see addTrainingAssignment/autoAssignTrainers. */
  async create(data: { clientId: string; name: string; description?: string; deploymentDate?: string; catalogItemId?: string; expiryDate?: string }): Promise<SystemProduct> {
    const created = await firstValueFrom(this.http.post<SystemProduct>(`${API_BASE_URL}/system-products`, data));
    await this.refreshForClient(data.clientId);
    return created;
  }

  async update(id: string, clientId: string, data: { name: string; description?: string; deploymentDate?: string; catalogItemId?: string; expiryDate?: string }): Promise<SystemProduct> {
    const updated = await firstValueFrom(this.http.put<SystemProduct>(`${API_BASE_URL}/system-products/${id}`, data));
    await this.refreshForClient(clientId);
    return updated;
  }

  /** Soft-deletes — agreement/training history under this system/product stays intact, just hidden from the active list. */
  async delete(id: string, clientId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/system-products/${id}`));
    await this.refreshForClient(clientId);
  }

  /** Whether this system/product's training has been marked Completed — the precondition for signing a Support agreement for it. */
  async hasCompletedTraining(id: string): Promise<boolean> {
    return firstValueFrom(this.http.get<boolean>(`${API_BASE_URL}/system-products/${id}/training-complete`));
  }

  /** Every eligible Trainer's current workload plus a recommendation — used for Manual Assignment's dropdown. Not scoped to one system/product. */
  async getTrainerWorkload(): Promise<TrainerAssignmentRecommendation> {
    return firstValueFrom(this.http.get<TrainerAssignmentRecommendation>(`${API_BASE_URL}/system-products/trainer-workload`));
  }

  /** Manual Assignment: adds one Trainer/Technician to this system/product's training roster. */
  async addTrainingAssignment(systemProductId: string, trainerEmployeeId: string): Promise<SystemProduct> {
    return firstValueFrom(
      this.http.post<SystemProduct>(`${API_BASE_URL}/system-products/${systemProductId}/training-assignments`, { trainerEmployeeId })
    );
  }

  /** Automatic Assignment: fills this system/product's remaining training-roster slots (up to the configured maximum) by current Trainer workload. */
  async autoAssignTrainers(systemProductId: string): Promise<SystemProduct> {
    return firstValueFrom(this.http.post<SystemProduct>(`${API_BASE_URL}/system-products/${systemProductId}/training-assignments/auto`, {}));
  }

  /** Removes a Trainer from this system/product's training roster. Any TrainingRecords they already logged remain as history. */
  async removeTrainingAssignment(systemProductId: string, assignmentId: string): Promise<SystemProduct> {
    return firstValueFrom(
      this.http.delete<SystemProduct>(`${API_BASE_URL}/system-products/${systemProductId}/training-assignments/${assignmentId}`)
    );
  }

  /** One-click Admin decision: marks this system/product's training Completed. Unlocks signing a Support agreement for it; does not stop further training records being logged afterward. */
  async markTrainingCompleted(systemProductId: string): Promise<SystemProduct> {
    return firstValueFrom(this.http.post<SystemProduct>(`${API_BASE_URL}/system-products/${systemProductId}/training-complete`, {}));
  }

  /** Trainer's own "done, ready for Admin" action once every checklist item has been saved. Stamps trainingSubmittedAt; does not itself mark training Completed. */
  async submitTraining(systemProductId: string): Promise<SystemProduct> {
    return firstValueFrom(this.http.post<SystemProduct>(`${API_BASE_URL}/system-products/${systemProductId}/training-submit`, {}));
  }
}
