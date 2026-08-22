import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SystemProduct } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Manages the SystemProduct layer between Client and Agreement:
 * Client -> SystemProduct -> Agreement -> AgreementType. A client can have
 * multiple systems/products; creating a new one never replaces an
 * existing one.
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

  /** Creates a new system/product for a client. Never overwrites or replaces one the client already has. */
  async create(data: { clientId: string; name: string; description?: string; deploymentDate?: string }): Promise<SystemProduct> {
    const created = await firstValueFrom(this.http.post<SystemProduct>(`${API_BASE_URL}/system-products`, data));
    await this.refreshForClient(data.clientId);
    return created;
  }

  async update(id: string, clientId: string, data: { name: string; description?: string; deploymentDate?: string }): Promise<SystemProduct> {
    const updated = await firstValueFrom(this.http.put<SystemProduct>(`${API_BASE_URL}/system-products/${id}`, data));
    await this.refreshForClient(clientId);
    return updated;
  }

  /** Soft-deletes — agreement/training history under this system/product stays intact, just hidden from the active list. */
  async delete(id: string, clientId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/system-products/${id}`));
    await this.refreshForClient(clientId);
  }
}
