import { Component, computed, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { TicketService } from '../../core/services/ticket.service';

type Answer = 'fixed' | 'not-fixed' | null;

@Component({
  selector: 'app-confirm-resolution',
  standalone: true,
  imports: [SlicePipe],
  template: `
    <h1>Confirm Resolution</h1>
    <p class="text-muted" style="margin-top:0.3rem;">
      These tickets have been marked resolved. Let us know if the fix is working — your answer helps us make sure it actually is.
    </p>

    <div class="list" style="margin-top:1.25rem;">
      @for (t of tickets(); track t.id) {
        <div class="panel panel-pad card">
          <div class="head">
            <span class="mono text-muted">{{ t.id.slice(0,8) }}</span>
            @if (t.clientConfirmationDeadline) {
              <span class="text-muted deadline">Respond by {{ t.clientConfirmationDeadline | slice:0:10 }}</span>
            }
          </div>
          <p class="desc">{{ t.description }}</p>

          @if (submittedFor() === t.id) {
            <div class="thanks">
              @if (lastOutcome() === 'reopened') {
                Thanks for letting us know — this ticket has been reopened and sent back to the technician.
              } @else {
                Thanks for confirming — your feedback has been recorded.
              }
            </div>
          } @else if (answers()[t.id] !== 'fixed') {
            <div class="yn-row">
              <span class="text-muted" style="font-size:0.85rem;">Is the issue fixed?</span>
              <div class="yn-buttons">
                <button class="btn btn-outline btn-sm" (click)="answerFixed(t.id)">Yes, it's fixed</button>
                <button class="btn btn-outline btn-sm" (click)="answerNotFixed(t.id)">No, still broken</button>
              </div>
            </div>
          } @else {
            <div class="rate-row">
              <span class="text-muted" style="font-size:0.85rem;">Great — how would you rate the resolution?</span>
              <div class="stars">
                @for (s of [1,2,3,4,5]; track s) {
                  <button
                    class="star"
                    [class.filled]="s <= (hoverStars() || selectedStars()[t.id] || 0)"
                    (mouseenter)="hoverStars.set(s)"
                    (mouseleave)="hoverStars.set(0)"
                    (click)="selectStars(t.id, s)"
                  >★</button>
                }
              </div>
            </div>
            <div class="btn-row">
              <button
                class="btn btn-primary btn-sm"
                [disabled]="!selectedStars()[t.id]"
                (click)="submitRating(t.id)"
              >Submit Rating</button>
              <button class="btn btn-outline btn-sm" (click)="backToQuestion(t.id)">Back</button>
            </div>
          }
        </div>
      }
      @empty {
        <div class="panel panel-pad text-muted" style="text-align:center;">Nothing awaiting your confirmation right now.</div>
      }
    </div>
  `,
  styles: [`
    .list { display: flex; flex-direction: column; gap: 1rem; }
    .card { display: flex; flex-direction: column; gap: 0.6rem; }
    .head { display: flex; justify-content: space-between; align-items: center; }
    .deadline { font-size: 0.78rem; }
    .desc { font-size: 0.9rem; }
    .yn-row { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
    .yn-buttons { display: flex; gap: 0.5rem; }
    .rate-row { display: flex; align-items: center; gap: 0.75rem; }
    .btn-row { display: flex; gap: 0.5rem; align-items: center; }
    .stars { display: flex; gap: 0.15rem; }
    .star { background: none; border: none; font-size: 1.5rem; line-height: 1; color: var(--slate-300); padding: 0; }
    .star.filled { color: #f5b800; }
    .thanks { font-size: 0.85rem; color: var(--green); background: var(--green-bg); padding: 0.6rem 0.8rem; border-radius: 8px; }
  `],
})
export class ConfirmResolutionComponent {
  hoverStars = signal(0);
  selectedStars = signal<Record<string, number>>({});
  answers = signal<Record<string, Answer>>({});
  submittedFor = signal<string | null>(null);
  lastOutcome = signal<'closed' | 'reopened' | null>(null);

  constructor(private auth: AuthService, private ticketsSvc: TicketService) {}

  tickets = computed(() => {
    const client = this.auth.currentClient();
    return client ? this.ticketsSvc.awaitingConfirmationForClient(client.id) : [];
  });

  answerFixed(ticketId: string) {
    this.answers.update(m => ({ ...m, [ticketId]: 'fixed' }));
  }

  backToQuestion(ticketId: string) {
    this.answers.update(m => ({ ...m, [ticketId]: null }));
  }

  async answerNotFixed(ticketId: string) {
    await this.ticketsSvc.confirmResolution(ticketId, false);
    this.lastOutcome.set('reopened');
    this.submittedFor.set(ticketId);
  }

  selectStars(ticketId: string, stars: number) {
    this.selectedStars.update(m => ({ ...m, [ticketId]: stars }));
  }

  async submitRating(ticketId: string) {
    const stars = this.selectedStars()[ticketId];
    if (!stars) return;
    await this.ticketsSvc.confirmResolution(ticketId, true, stars);
    this.lastOutcome.set('closed');
    this.submittedFor.set(ticketId);
  }
}
