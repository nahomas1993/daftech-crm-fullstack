import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FailureType, DurationUnit, TicketCategory } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin-managed list of client-system failure types, each with an
 * expected resolution duration (hours/days/months). Clients pick one when
 * submitting a ticket; GET is public (no auth header required), matching
 * the backend [AllowAnonymous] on FailureTypesController.GetAll, since
 * the client portal's Submit Issue form needs this list.
 */
@Injectable({ providedIn: 'root' })
export class FailureTypeService {
  private readonly _types = signal<FailureType[]>([]);
  readonly types = this._types.asReadonly();

  private readonly _loading = signal(false);
  readonly loading = this._loading.asReadonly();

  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  constructor(private http: HttpClient) {
    void this.refresh();
  }

  /**
   * Never throws — the UI shows an inline error + retry instead of silently
   * rendering an empty dropdown when the list cannot be loaded.
   */
  async refresh(): Promise<void> {
    this._loading.set(true);
    try {
      const result = await firstValueFrom(this.http.get<FailureType[]>(`${API_BASE_URL}/failure-types`));
      this._types.set(result ?? []);
      this._error.set(null);
    } catch {
      this._error.set('Could not load failure types. Check your connection and try again.');
    } finally {
      this._loading.set(false);
    }
  }

  async create(category: TicketCategory, name: string, durationValue: number, durationUnit: DurationUnit, description?: string, basePrice = 0, requiredSpecialization?: string): Promise<FailureType> {
    const entry = await firstValueFrom(this.http.post<FailureType>(`${API_BASE_URL}/failure-types`, { category, name, description, basePrice, durationValue, durationUnit, requiredSpecialization }));
    await this.refresh();
    return entry;
  }

  async update(id: string, category: TicketCategory, name: string, durationValue: number, durationUnit: DurationUnit, description?: string, basePrice = 0, requiredSpecialization?: string): Promise<FailureType> {
    const entry = await firstValueFrom(this.http.put<FailureType>(`${API_BASE_URL}/failure-types/${id}`, { category, name, description, basePrice, durationValue, durationUnit, requiredSpecialization }));
    await this.refresh();
    return entry;
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/failure-types/${id}`));
    await this.refresh();
  }
}
