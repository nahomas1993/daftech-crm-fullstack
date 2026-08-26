import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SatisfactionSurvey } from '../models';
import { API_BASE_URL } from './api-base';

export interface SubmitSurveyAnswerPayload {
  questionId: string;
  rating: number;
}

export interface SubmitSurveyPayload {
  ticketId: string;
  clientId: string;
  answers: SubmitSurveyAnswerPayload[];
  satisfactionComment?: string;
}

@Injectable({ providedIn: 'root' })
export class SatisfactionSurveyService {
  private readonly _surveys = signal<SatisfactionSurvey[]>([]);
  readonly surveys = this._surveys.asReadonly();

  constructor(private http: HttpClient) {}

  /** All submitted surveys — used by the Reports page. Loaded on demand (not in the constructor) since only Admins visit that page. */
  async refresh(): Promise<void> {
    const list = await firstValueFrom(this.http.get<SatisfactionSurvey[]>(`${API_BASE_URL}/satisfaction-surveys`));
    this._surveys.set(list);
  }

  async submit(payload: SubmitSurveyPayload): Promise<SatisfactionSurvey> {
    return firstValueFrom(this.http.post<SatisfactionSurvey>(`${API_BASE_URL}/satisfaction-surveys`, payload));
  }

  async getForTicket(ticketId: string): Promise<SatisfactionSurvey | null> {
    try {
      return await firstValueFrom(this.http.get<SatisfactionSurvey>(`${API_BASE_URL}/satisfaction-surveys/ticket/${ticketId}`));
    } catch {
      return null; // 404 — no survey submitted yet
    }
  }
}
