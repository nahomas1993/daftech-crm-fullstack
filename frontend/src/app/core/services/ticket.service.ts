import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Ticket, TicketCategory, TicketStatus, PagedResult } from '../models';
import { API_BASE_URL } from './api-base';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class TicketService {
  private readonly _tickets = signal<Ticket[]>([]);
  readonly tickets = this._tickets.asReadonly();

  private readonly _myTickets = signal<Ticket[]>([]);
  readonly myTickets = this._myTickets.asReadonly();

  private readonly _page = signal(1);
  private readonly _pageSize = signal(20);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _pagedTickets = signal<Ticket[]>([]);
  readonly pagedTickets = this._pagedTickets.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();

  constructor(private http: HttpClient, private auth: AuthService) {
    if (this.auth.isStaffAuthenticated()) {
      void this.refresh();
      void this.refreshPaged();
    }
    // NOTE: this only fires once, at app bootstrap, when TicketService
    // (providedIn: 'root') is first constructed — typically before login
    // has happened. It doesn't reliably load data after the fact. Pages
    // that need myTickets data must call refreshMyTickets() themselves in
    // ngOnInit (see MyTicketsComponent, MaintenanceHistoryComponent,
    // DashboardComponent) rather than relying on this constructor check.
  }

  async refresh(): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<Ticket[]>(`${API_BASE_URL}/tickets`)
    );
    this._tickets.set(list);
  }

  async refreshPaged(
    page = this._page(),
    pageSize = this._pageSize()
  ): Promise<void> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    const result = await firstValueFrom(
      this.http.get<PagedResult<Ticket>>(
        `${API_BASE_URL}/tickets/paged`,
        { params }
      )
    );

    this._page.set(result.page);
    this._pageSize.set(result.pageSize);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._pagedTickets.set(result.items);
  }

  async goToPage(page: number): Promise<void> {
    await this.refreshPaged(page);
  }

  getById(id: string): Ticket | undefined {
    return this._tickets().find(t => t.id === id)
      ?? this._myTickets().find(t => t.id === id);
  }

  async refreshMyTickets(clientId: string): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<Ticket[]>(
        `${API_BASE_URL}/tickets/client/${clientId}`
      )
    );

    this._myTickets.set(list);
  }

  forClient(clientId: string): Ticket[] {
    return this._myTickets()
      .filter(t => t.clientId === clientId)
      .sort((a, b) =>
        b.dateSubmitted.localeCompare(a.dateSubmitted)
      );
  }

  forEmployee(employeeId: string): Ticket[] {
    return this._tickets()
      .filter(t => t.assignedEmployeeId === employeeId);
  }

  awaitingConfirmationForClient(clientId: string): Ticket[] {
    return this._myTickets()
      .filter(
        t =>
          t.clientId === clientId &&
          t.status === 'AwaitingClientConfirmation'
      );
  }

  escalated(): Ticket[] {
    return this._tickets()
      .filter(t => t.status === 'Escalated');
  }

  openTicketCountByEmployee(): Record<string, number> {
    const open: TicketStatus[] = ['Assigned', 'InProgress'];
    const counts: Record<string, number> = {};

    for (const t of this._tickets()) {
      if (
        t.assignedEmployeeId &&
        open.includes(t.status)
      ) {
        counts[t.assignedEmployeeId] =
          (counts[t.assignedEmployeeId] ?? 0) + 1;
      }
    }

    return counts;
  }

  async submitFromClient(
    clientId: string,
    agreementId: string,
    description: string,
    category: TicketCategory,
    failureTypeId?: string,
    voiceNote?: {
      storageKey: string;
      fileName: string;
    }
  ): Promise<Ticket> {
    const ticket = await firstValueFrom(
      this.http.post<Ticket>(
        `${API_BASE_URL}/tickets`,
        {
          clientId,
          agreementId,
          description,
          category,
          failureTypeId,
          voiceNoteStorageKey: voiceNote?.storageKey,
          voiceNoteFileName: voiceNote?.fileName,
        }
      )
    );

    if (this.auth.isStaffAuthenticated()) {
      await Promise.all([
        this.refresh(),
        this.refreshPaged()
      ]);
    }

    await this.refreshMyTickets(clientId);

    return ticket;
  }

  async uploadVoiceNote(
    blob: Blob,
    fileName: string
  ): Promise<{
    storageKey: string;
    fileName: string;
  }> {
    const form = new FormData();
    form.append('file', blob, fileName);

    const result = await firstValueFrom(
      this.http.post<{
        storageKey: string;
        fileName: string;
      }>(
        `${API_BASE_URL}/tickets/voice-note`,
        form
      )
    );

    return result;
  }

  async downloadVoiceNote(ticketId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(
        `${API_BASE_URL}/tickets/${ticketId}/voice-note`,
        { responseType: 'blob' }
      )
    );
  }

  async updateStatus(
    ticketId: string,
    status: TicketStatus,
    actorName: string
  ): Promise<void> {
    // Send the change against the latest server state, and retry a couple of
    // times on 409 before ever showing an error. A 409 only means "the row
    // moved between the server's read and its write" — it is recoverable and
    // the backend's resolve path is idempotent, so retrying can never
    // double-apply. Between attempts we reload the list so the next PATCH is
    // built from fresh ticket data.
    const maxAttempts = 3;
    let lastError: any = null;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        await this.patchStatus(ticketId, status, actorName);
        lastError = null;
        break;
      } catch (err: any) {
        lastError = err;

        if (err?.status !== 409 || attempt === maxAttempts) break;

        await this.refreshAll();

        // The intent may already be satisfied by whoever won the race — that
        // is a success, so stop and never show a conflict message.
        if (this.hasReached(ticketId, status)) {
          lastError = null;
          break;
        }
      }
    }

    // Always resync from the server, whatever happened, so the list the
    // technician sees (and any follow-up retry) reflects the latest data.
    await this.refreshAll();

    if (lastError) {
      // Final safety net: if the change actually landed server-side, the
      // update succeeded and no error must be shown.
      if (this.hasReached(ticketId, status)) return;

      throw new Error(TicketService.describeStatusUpdateError(lastError));
    }
  }

  private async refreshAll(): Promise<void> {
    await Promise.all([
      this.refresh(),
      this.refreshPaged()
    ]);
  }

  /**
   * True when the freshly loaded ticket already reflects the requested
   * change. "Resolved" is special: the server does not store Resolved — it
   * moves the ticket to AwaitingClientConfirmation and starts the client
   * confirmation window, so anything at or beyond that point counts as done.
   */
  private hasReached(ticketId: string, status: TicketStatus): boolean {
    const current = this._tickets().find(t => t.id === ticketId)
      ?? this._pagedTickets().find(t => t.id === ticketId);

    if (!current) return false;

    if (status === 'Resolved') {
      return current.status === 'AwaitingClientConfirmation'
        || current.status === 'Closed'
        || current.status === 'Escalated';
    }

    return current.status === status;
  }

  private async patchStatus(
    ticketId: string,
    status: TicketStatus,
    actorName: string
  ): Promise<void> {
    await firstValueFrom(
      this.http.patch<Ticket>(
        `${API_BASE_URL}/tickets/${ticketId}/status`,
        {
          status,
          actorName
        }
      )
    );
  }

  /**
   * Maps a failed status-update request to a user-facing message by HTTP
   * status code, not by echoing whatever text the server happened to send.
   * This is only ever reached after every retry lost the race AND a fresh
   * read confirmed the change did not land (see updateStatus), so the
   * conflict wording can never appear for a successful update or for an
   * unrelated failure. 404 means the ticket no longer exists; 403 means the
   * caller isn't the assigned technician; anything else is a server error.
   */
  private static describeStatusUpdateError(err: any): string {
    switch (err?.status) {
      case 409:
        return 'This ticket was changed by another user. The list has been refreshed — please try again.';
      case 404:
        return 'Ticket not found. It may have been removed.';
      case 403:
        return 'You are not the technician assigned to this ticket.';
      case 500:
        return 'Server error. Please try again.';
      default:
        return 'Server error. Please try again.';
    }
  }

  async confirmResolution(
    ticketId: string,
    isFixed: boolean,
    satisfactionStars?: number
  ): Promise<Ticket> {
    const ticket = await firstValueFrom(
      this.http.post<Ticket>(
        `${API_BASE_URL}/tickets/${ticketId}/confirm`,
        {
          isFixed,
          satisfactionStars
        }
      )
    );

    if (this.auth.isStaffAuthenticated()) {
      await Promise.all([
        this.refresh(),
        this.refreshPaged()
      ]);
    } else {
      await this.refreshMyTickets(ticket.clientId);
    }

    return ticket;
  }

  async uploadAttachment(
    ticketId: string,
    file: File
  ): Promise<Ticket> {
    const form = new FormData();
    form.append('file', file, file.name);

    const ticket = await firstValueFrom(
      this.http.post<Ticket>(
        `${API_BASE_URL}/tickets/${ticketId}/attachment`,
        form
      )
    );

    if (this.auth.isStaffAuthenticated()) {
      await Promise.all([
        this.refresh(),
        this.refreshPaged()
      ]);
    } else {
      await this.refreshMyTickets(ticket.clientId);
    }

    return ticket;
  }

  async downloadAttachment(ticketId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(
        `${API_BASE_URL}/tickets/${ticketId}/attachment`,
        { responseType: 'blob' }
      )
    );
  }
}
