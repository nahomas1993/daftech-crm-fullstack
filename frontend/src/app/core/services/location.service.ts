import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { LocationEntry, LocationOptions, LocationType } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin-managed dropdown/checklist options: Region / Zone / City / Woreda
 * (client forms), Specialization and CustomRole (employee form). Region,
 * Zone, and Woreda form a strict parent chain (a Zone belongs to a Region,
 * a Woreda belongs to a Zone) via parentId — City remains an independent
 * flat list alongside them, not a rename of Zone. Requires an
 * authenticated staff session, matching the backend [Authorize] on
 * LocationsController.GetAll.
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

  async create(type: LocationType, name: string, parentId?: string): Promise<LocationEntry> {
    const entry = await firstValueFrom(this.http.post<LocationEntry>(`${API_BASE_URL}/locations`, { type, name, parentId: parentId || null }));
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

  /** Zones belonging to the given Region, for cascading dropdowns. Empty array if regionId is falsy. */
  zonesFor(regionId: string | null | undefined): LocationEntry[] {
    if (!regionId) return [];
    return this._options().zones.filter(z => z.parentId === regionId);
  }

  /** Woredas belonging to the given Zone, for cascading dropdowns. Empty array if zoneId is falsy. */
  woredasFor(zoneId: string | null | undefined): LocationEntry[] {
    if (!zoneId) return [];
    return this._options().woredas.filter(w => w.parentId === zoneId);
  }

  regionName(id: string | null | undefined): string | undefined {
    return id ? this._options().regions.find(r => r.id === id)?.name : undefined;
  }

  zoneName(id: string | null | undefined): string | undefined {
    return id ? this._options().zones.find(z => z.id === id)?.name : undefined;
  }
}
