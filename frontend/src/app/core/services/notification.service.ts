import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AppNotification, NotificationRecipientType } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly _byKey = signal<Map<string, AppNotification[]>>(new Map());

  private keyFor(type: NotificationRecipientType, id: string): string {
    return `${type}:${id}`;
  }

  forRecipient(type: NotificationRecipientType, id: string): AppNotification[] {
    return this._byKey().get(this.keyFor(type, id)) ?? [];
  }

  unreadCountFor(type: NotificationRecipientType, id: string): number {
    return this.forRecipient(type, id).filter(n => !n.readStatus).length;
  }

  async loadFor(type: NotificationRecipientType, id: string): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<AppNotification[]>(`${API_BASE_URL}/notifications`, {
        params: { recipientType: type, recipientId: id },
      })
    );
    const next = new Map(this._byKey());
    next.set(this.keyFor(type, id), list);
    this._byKey.set(next);
  }

  constructor(private http: HttpClient) {}

  async markRead(type: NotificationRecipientType, id: string, notificationId: string): Promise<void> {
    await firstValueFrom(this.http.post(`${API_BASE_URL}/notifications/${notificationId}/read`, {}));
    await this.loadFor(type, id);
  }

  async markAllReadFor(type: NotificationRecipientType, id: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${API_BASE_URL}/notifications/mark-all-read`, {}, { params: { recipientType: type, recipientId: id } })
    );
    await this.loadFor(type, id);
  }
}
