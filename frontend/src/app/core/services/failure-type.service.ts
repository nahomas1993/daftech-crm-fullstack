import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FailureType, DurationUnit } from '../models';
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

  constructor(private http: HttpClient) {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    const result = await firstValueFrom(this.http.get<FailureType[]>(`${API_BASE_URL}/failure-types`));
    this._types.set(result);
  }

  async create(name: string, durationValue: number, durationUnit: DurationUnit, description?: string): Promise<FailureType> {
    const entry = await firstValueFrom(this.http.post<FailureType>(`${API_BASE_URL}/failure-types`, { name, description, durationValue, durationUnit }));
    await this.refresh();
    return entry;
  }

  async update(id: string, name: string, durationValue: number, durationUnit: DurationUnit, description?: string): Promise<FailureType> {
    const entry = await firstValueFrom(this.http.put<FailureType>(`${API_BASE_URL}/failure-types/${id}`, { name, description, durationValue, durationUnit }));
    await this.refresh();
    return entry;
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/failure-types/${id}`));
    await this.refresh();
  }
}
