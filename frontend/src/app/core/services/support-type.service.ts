import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SupportType } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin-managed list of the ways support can be delivered — remote, on-site,
 * after hours, and so on — each carrying its own extra fee. Clients pick one
 * when they submit an issue, so GET is public (the backend marks
 * SupportTypesController.GetAll [AllowAnonymous]) while creating, editing and
 * deleting stay admin-only.
 */
@Injectable({ providedIn: 'root' })
export class SupportTypeService {
  private readonly _types = signal<SupportType[]>([]);
  readonly types = this._types.asReadonly();

  private readonly _loading = signal(false);
  readonly loading = this._loading.asReadonly();

  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  constructor(private http: HttpClient) {
    void this.refresh();
  }

  /** Never throws — the UI shows an inline message and a retry rather than a silently empty dropdown. */
  async refresh(): Promise<void> {
    this._loading.set(true);
    try {
      const result = await firstValueFrom(this.http.get<SupportType[]>(`${API_BASE_URL}/support-types`));
      this._types.set(result ?? []);
      this._error.set(null);
    } catch {
      this._error.set('Could not load support types. Check your connection and try again.');
    } finally {
      this._loading.set(false);
    }
  }

  async create(name: string, additionalFee: number, description?: string): Promise<SupportType> {
    const entry = await firstValueFrom(this.http.post<SupportType>(`${API_BASE_URL}/support-types`, { name, description, additionalFee }));
    await this.refresh();
    return entry;
  }

  async update(id: string, name: string, additionalFee: number, description?: string): Promise<SupportType> {
    const entry = await firstValueFrom(this.http.put<SupportType>(`${API_BASE_URL}/support-types/${id}`, { name, description, additionalFee }));
    await this.refresh();
    return entry;
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/support-types/${id}`));
    await this.refresh();
  }
}
