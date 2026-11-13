import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AgreementType } from '../models';
import { API_BASE_URL } from './api-base';
import { AuthService } from './auth.service';

/**
 * Admin-managed lookup of agreement types — Support and Training always
 * exist (seeded server-side, see AgreementTypeNames) and can't be
 * deleted; an Admin can add further custom types. GET requires AnyEmployee
 * on the API, so — like AgreementService — this only auto-fetches for a
 * logged-in staff session, never for the client portal.
 */
@Injectable({ providedIn: 'root' })
export class AgreementTypeService {
  private readonly _types = signal<AgreementType[]>([]);
  readonly types = this._types.asReadonly();

  constructor(private http: HttpClient, private auth: AuthService) {
    if (this.auth.isStaffAuthenticated()) {
      void this.refresh();
    }
  }

  async refresh(): Promise<void> {
    const result = await firstValueFrom(this.http.get<AgreementType[]>(`${API_BASE_URL}/agreement-types`));
    this._types.set(result ?? []);
  }

  /** Convenience lookup for the two built-in types, so the "New Agreement" flow can default the type dropdown without waiting on a name string match at the call site. */
  findByName(name: 'Support' | 'Training'): AgreementType | undefined {
    return this._types().find(t => t.name === name);
  }

  async create(name: string, description?: string): Promise<AgreementType> {
    const entry = await firstValueFrom(this.http.post<AgreementType>(`${API_BASE_URL}/agreement-types`, { name, description }));
    await this.refresh();
    return entry;
  }

  async update(id: string, description?: string): Promise<AgreementType> {
    const entry = await firstValueFrom(this.http.put<AgreementType>(`${API_BASE_URL}/agreement-types/${id}`, { description }));
    await this.refresh();
    return entry;
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/agreement-types/${id}`));
    await this.refresh();
  }
}
