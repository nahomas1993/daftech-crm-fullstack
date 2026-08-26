import { Component, OnInit, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { SatisfactionSurveyService } from '../../core/services/satisfaction-survey.service';
import { SurveyQuestionService } from '../../core/services/survey-question.service';
import { SurveyQuestion, SATISFACTION_RATING_LABELS } from '../../core/models';

@Component({
  selector: 'app-satisfaction-survey',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h1>Quick Survey</h1>
    <p class="text-muted" style="margin-top:0.3rem;">
      A few quick questions about your experience — optional, and separate from the resolution rating you already gave.
    </p>

    @if (submitted()) {
      <div class="panel panel-pad thanks" style="margin-top:1.25rem;">
        Thanks for the feedback — it's been recorded.
      </div>
    } @else if (loading()) {
      <p class="text-muted" style="margin-top:1.25rem;">Loading survey…</p>
    } @else if (questions().length === 0) {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        <p class="text-muted">No survey questions are configured right now.</p>
      </div>
    } @else {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        @for (q of questions(); track q.id; let i = $index) {
          <div class="question">
            <p class="q-text">{{ i + 1 }}. {{ q.text }}</p>
            <div class="rating-options">
              @for (r of ratingValues; track r) {
                <label class="rating-option" [class.selected]="answers()[q.id] === r">
                  <input
                    type="radio"
                    [name]="'q-' + q.id"
                    [value]="r"
                    [checked]="answers()[q.id] === r"
                    (change)="setAnswer(q.id, r)"
                  />
                  <span class="rating-number">{{ r }}</span>
                  <span class="rating-label">{{ ratingLabels[r] }}</span>
                </label>
              }
            </div>
          </div>
        }

        <div class="question">
          <p class="q-text">In your own words, how would you describe your overall experience? <span class="text-muted">(optional)</span></p>
          <textarea
            rows="5"
            maxlength="1000"
            [ngModel]="comment()"
            (ngModelChange)="comment.set($event)"
            placeholder="Tell us about your experience…"
          ></textarea>
        </div>

        @if (error()) {
          <p class="error">{{ error() }}</p>
        }

        <button class="btn btn-primary" [disabled]="!allAnswered() || submitting()" (click)="submit()">
          {{ submitting() ? 'Submitting…' : 'Submit Survey' }}
        </button>
      </div>
    }
  `,
  styles: [`
    .question { margin-bottom: 1.6rem; }
    .question:last-of-type { margin-bottom: 1rem; }
    .q-text { font-size: 0.9rem; margin-bottom: 0.6rem; }
    .rating-options { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .rating-option {
      display: flex; flex-direction: column; align-items: center; gap: 0.2rem;
      padding: 0.5rem 0.75rem; border: 1px solid var(--slate-300); border-radius: 8px;
      cursor: pointer; min-width: 74px; text-align: center;
    }
    .rating-option input { position: absolute; opacity: 0; pointer-events: none; }
    .rating-option.selected { border-color: var(--primary, #2563eb); background: var(--primary-bg, #eff6ff); }
    .rating-number { font-weight: 600; font-size: 1rem; }
    .rating-label { font-size: 0.72rem; color: var(--slate-500); }
    .rating-option.selected .rating-label { color: var(--primary, #2563eb); }
    textarea { width: 100%; resize: vertical; }
    .thanks { color: var(--green); background: var(--green-bg); text-align: center; }
    .error { color: var(--red); font-size: 0.82rem; margin-bottom: 0.75rem; }
  `],
})
export class SatisfactionSurveyComponent implements OnInit {
  ticketId = input.required<string>();

  ratingValues = [1, 2, 3, 4, 5];
  ratingLabels = SATISFACTION_RATING_LABELS;

  questions = signal<SurveyQuestion[]>([]);
  loading = signal(true);

  answers = signal<Record<string, number>>({});
  comment = signal('');
  submitting = signal(false);
  submitted = signal(false);
  error = signal<string | null>(null);

  constructor(
    private auth: AuthService,
    private surveys: SatisfactionSurveyService,
    private surveyQuestions: SurveyQuestionService
  ) {}

  async ngOnInit() {
    try {
      const active = await this.surveyQuestions.getActive();
      this.questions.set(active);
    } catch {
      this.error.set('Could not load the survey questions — please try again later.');
    } finally {
      this.loading.set(false);
    }
  }

  allAnswered = computed(() => {
    const qs = this.questions();
    const a = this.answers();
    return qs.length > 0 && qs.every(q => (a[q.id] ?? 0) > 0);
  });

  setAnswer(questionId: string, value: number) {
    this.answers.update(m => ({ ...m, [questionId]: value }));
  }

  async submit() {
    const client = this.auth.currentClient();
    const a = this.answers();
    if (!client || !this.allAnswered()) return;

    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.surveys.submit({
        ticketId: this.ticketId(),
        clientId: client.id,
        answers: this.questions().map(q => ({ questionId: q.id, rating: a[q.id] })),
        satisfactionComment: this.comment().trim() || undefined,
      });
      this.submitted.set(true);
    } catch {
      this.error.set('Something went wrong submitting the survey — please try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
