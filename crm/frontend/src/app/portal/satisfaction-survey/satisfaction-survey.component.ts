import { Component, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { SatisfactionSurveyService } from '../../core/services/satisfaction-survey.service';

interface RatingQuestion {
  key: 'responseSpeedRating' | 'professionalismRating' | 'communicationClarityRating' | 'likelihoodToRecommend';
  question: string;
}

const QUESTIONS: RatingQuestion[] = [
  { key: 'responseSpeedRating', question: '1. How would you rate the speed of our response?' },
  { key: 'professionalismRating', question: "2. How would you rate the technician's professionalism?" },
  { key: 'communicationClarityRating', question: '3. How clearly was the issue explained to you?' },
  { key: 'likelihoodToRecommend', question: '4. How likely are you to recommend DAFTECH support to a colleague?' },
];

@Component({
  selector: 'app-satisfaction-survey',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h1>Quick Survey</h1>
    <p class="text-muted" style="margin-top:0.3rem;">
      Five quick questions about your experience — optional, and separate from the resolution rating you already gave.
    </p>

    @if (submitted()) {
      <div class="panel panel-pad thanks" style="margin-top:1.25rem;">
        Thanks for the feedback — it's been recorded.
      </div>
    } @else {
      <div class="panel panel-pad" style="margin-top:1.25rem;">
        @for (q of questions; track q.key) {
          <div class="question">
            <p class="q-text">{{ q.question }}</p>
            <div class="stars">
              @for (s of [1,2,3,4,5]; track s) {
                <button
                  class="star"
                  [class.filled]="s <= (hover()[q.key] || answers()[q.key] || 0)"
                  (mouseenter)="setHover(q.key, s)"
                  (mouseleave)="setHover(q.key, 0)"
                  (click)="setAnswer(q.key, s)"
                >★</button>
              }
            </div>
          </div>
        }

        <div class="question">
          <p class="q-text">5. What could we have done better? <span class="text-muted">(optional)</span></p>
          <textarea rows="3" [ngModel]="feedback()" (ngModelChange)="feedback.set($event)" placeholder="Your thoughts…"></textarea>
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
    .question { margin-bottom: 1.4rem; }
    .question:last-of-type { margin-bottom: 1rem; }
    .q-text { font-size: 0.9rem; margin-bottom: 0.5rem; }
    .stars { display: flex; gap: 0.15rem; }
    .star { background: none; border: none; font-size: 1.6rem; line-height: 1; color: var(--slate-300); padding: 0; }
    .star.filled { color: #f5b800; }
    textarea { width: 100%; resize: vertical; }
    .thanks { color: var(--green); background: var(--green-bg); text-align: center; }
    .error { color: var(--red); font-size: 0.82rem; margin-bottom: 0.75rem; }
  `],
})
export class SatisfactionSurveyComponent {
  ticketId = input.required<string>();
  questions = QUESTIONS;

  answers = signal<Partial<Record<RatingQuestion['key'], number>>>({});
  hover = signal<Partial<Record<RatingQuestion['key'], number>>>({});
  feedback = signal('');
  submitting = signal(false);
  submitted = signal(false);
  error = signal<string | null>(null);

  constructor(
    private auth: AuthService,
    private surveys: SatisfactionSurveyService
  ) {}

  allAnswered = computed(() => this.questions.every(q => (this.answers()[q.key] ?? 0) > 0));

  setAnswer(key: RatingQuestion['key'], value: number) {
    this.answers.update(m => ({ ...m, [key]: value }));
  }

  setHover(key: RatingQuestion['key'], value: number) {
    this.hover.update(m => ({ ...m, [key]: value }));
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
        responseSpeedRating: a.responseSpeedRating!,
        professionalismRating: a.professionalismRating!,
        communicationClarityRating: a.communicationClarityRating!,
        likelihoodToRecommend: a.likelihoodToRecommend!,
        improvementFeedback: this.feedback().trim() || undefined,
      });
      this.submitted.set(true);
    } catch {
      this.error.set('Something went wrong submitting the survey — please try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
