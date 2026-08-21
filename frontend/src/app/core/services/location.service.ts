import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { LocationEntry, LocationOptions, LocationType } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin-managed dropdown/checklist options: Region / Zone / City / Woreda
 * (client forms), Specialization and CustomRole (employee form). Six
 * independent flat lists — not a hierarchy; Zone is a separate field
 * alongside City, not a replacement for it. GET is public (no auth header
 * required), matching the backend [AllowAnonymous] on
 * LocationsController.GetAll, since the self-signup portal needs
 * Region/Zone/City/Woreda before the client has any credentials.
 */
@Injectable({ providedIn: 'root' })
export class LocationService {
  private readonly _options = signal<LocationOptions>({
    regions: [], zones: [], cities: [], woredas: [], specializations: [], customRoles: [],
  });
  readonly options = this._options.asReadonly();

  constructor(private http: HttpClient) {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    const result = await firstValueFrom(this.http.get<LocationOptions>(`${API_BASE_URL}/locations`));
    this._options.set(result);
  }

  async create(type: LocationType, name: string): Promise<LocationEntry> {
    const entry = await firstValueFrom(this.http.post<LocationEntry>(`${API_BASE_URL}/locations`, { type, name }));
    await this.refresh();
    return entry;
  }

  async update(id: string, name: string): Promise<LocationEntry> {
    const entry = await firstValueFrom(this.http.put<LocationEntry>(`${API_BASE_URL}/locations/${id}`, { name }));
    await this.refresh();
    return entry;
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/locations/${id}`));
    await this.refresh();
  }
}
