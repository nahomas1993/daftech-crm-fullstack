import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SystemSetting } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class SystemConfigurationService {
  private readonly _settings = signal<SystemSetting[]>([]);
  readonly settings = this._settings.asReadonly();

  constructor(private http: HttpClient) {}

  async refresh(): Promise<void> {
    const list = await firstValueFrom(this.http.get<SystemSetting[]>(`${API_BASE_URL}/system-configuration`));
    this._settings.set(list);
  }

  /** Settings grouped by their Category, in the order the API returned them — used to render one panel per section. */
  byCategory(): { category: string; settings: SystemSetting[] }[] {
    const groups = new Map<string, SystemSetting[]>();
    for (const s of this._settings()) {
      if (!groups.has(s.category)) groups.set(s.category, []);
      groups.get(s.category)!.push(s);
    }
    return Array.from(groups.entries()).map(([category, settings]) => ({ category, settings }));
  }

  /** Saves one or more settings in a single request, then refreshes the local cache. */
  async update(changes: { key: string; value: string }[]): Promise<void> {
    const updated = await firstValueFrom(
      this.http.put<SystemSetting[]>(`${API_BASE_URL}/system-configuration`, {
        // The API models every value as a string; coerce defensively so a
        // numeric input value can never produce a 400 from JSON binding.
        settings: changes.map(c => ({ key: c.key, value: String(c.value ?? '') })),
      })
    );
    this._settings.set(updated);
  }
}
