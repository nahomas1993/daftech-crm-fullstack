import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ProductCatalogItem } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin-managed catalog of system/product names (e.g. "Branch POS
 * System", "HR Portal") — configurable from Settings without a code
 * change, matching the FailureTypeService/SupportTypeService pattern.
 * GET (active-only) is public since client registration and the client
 * portal's system/product selection both need this list; create/edit/
 * remove stay admin-only.
 */
@Injectable({ providedIn: 'root' })
export class ProductCatalogService {
  /** Active entries only — what registration/system-product/ticket forms pick from. */
  private readonly _items = signal<ProductCatalogItem[]>([]);
  readonly items = this._items.asReadonly();

  /** Every entry including inactive ones — the admin Settings management list. */
  private readonly _adminItems = signal<ProductCatalogItem[]>([]);
  readonly adminItems = this._adminItems.asReadonly();

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
      const result = await firstValueFrom(this.http.get<ProductCatalogItem[]>(`${API_BASE_URL}/product-catalog`));
      this._items.set(result ?? []);
      this._error.set(null);
    } catch {
      this._error.set('Could not load systems/products. Check your connection and try again.');
    } finally {
      this._loading.set(false);
    }
  }

  /** Admin-only: every entry, including retired ones, for the Settings management list. */
  async refreshForAdmin(): Promise<void> {
    const result = await firstValueFrom(this.http.get<ProductCatalogItem[]>(`${API_BASE_URL}/product-catalog/admin`));
    this._adminItems.set(result ?? []);
  }

  async create(name: string, description?: string): Promise<ProductCatalogItem> {
    const entry = await firstValueFrom(this.http.post<ProductCatalogItem>(`${API_BASE_URL}/product-catalog`, { name, description }));
    await Promise.all([this.refresh(), this.refreshForAdmin()]);
    return entry;
  }

  async update(id: string, name: string, description: string | undefined, isActive: boolean): Promise<ProductCatalogItem> {
    const entry = await firstValueFrom(this.http.put<ProductCatalogItem>(`${API_BASE_URL}/product-catalog/${id}`, { name, description, isActive }));
    await Promise.all([this.refresh(), this.refreshForAdmin()]);
    return entry;
  }

  /** Deactivates rather than hard-deletes — see the backend's ProductCatalogItemService.DeleteAsync. */
  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/product-catalog/${id}`));
    await Promise.all([this.refresh(), this.refreshForAdmin()]);
  }
}
