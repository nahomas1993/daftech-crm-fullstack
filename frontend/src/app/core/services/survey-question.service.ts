import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SurveyQuestion } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin-managed catalog of satisfaction survey questions (Settings →
 * Configuration → Satisfaction Survey). Fully dynamic — there is no fixed
 * question set; admins add, edit, reorder, retire, and delete questions
 * from here, and the client portal's survey form renders whatever
 * getActive() returns.
 */
@Injectable({ providedIn: 'root' })
export class SurveyQuestionService {
  private readonly _questions = signal<SurveyQuestion[]>([]);
  readonly questions = this._questions.asReadonly();

  private readonly _loading = signal(false);
  readonly loading = this._loading.asReadonly();

  constructor(private http: HttpClient) {}

  /** Every question, including retired ones — for the admin management screen. */
  async refresh(): Promise<void> {
    this._loading.set(true);
    try {
      const list = await firstValueFrom(this.http.get<SurveyQuestion[]>(`${API_BASE_URL}/survey-questions`));
      this._questions.set(list ?? []);
    } finally {
      this._loading.set(false);
    }
  }

  /** Active questions only, in display order — what the client-facing survey form shows. */
  async getActive(): Promise<SurveyQuestion[]> {
    return firstValueFrom(this.http.get<SurveyQuestion[]>(`${API_BASE_URL}/survey-questions/active`));
  }

  async create(text: string): Promise<SurveyQuestion> {
    const entry = await firstValueFrom(this.http.post<SurveyQuestion>(`${API_BASE_URL}/survey-questions`, { text }));
    await this.refresh();
    return entry;
  }

  async update(id: string, text: string, isActive: boolean): Promise<SurveyQuestion> {
    const entry = await firstValueFrom(this.http.put<SurveyQuestion>(`${API_BASE_URL}/survey-questions/${id}`, { text, isActive }));
    await this.refresh();
    return entry;
  }

  async reorder(orderedQuestionIds: string[]): Promise<void> {
    await firstValueFrom(this.http.put<void>(`${API_BASE_URL}/survey-questions/reorder`, { orderedQuestionIds }));
    await this.refresh();
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/survey-questions/${id}`));
    await this.refresh();
  }
}
