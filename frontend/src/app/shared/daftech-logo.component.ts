import { Component, Input } from '@angular/core';
import { NgIf } from '@angular/common';

/**
 * DAFTECH brand mark, drawn inline as vector art so it inherits the
 * surrounding theme (no white image box, scales crisply at any size).
 *
 * variant="mark"  -> triangle mark only
 * variant="full"  -> triangle mark + DAF-TECH wordmark
 */
@Component({
  selector: 'daftech-logo',
  standalone: true,
  imports: [NgIf],
  template: `
    <span class="dl" [class.dl-full]="variant === 'full'" [style.--dl-size.px]="size">
      <svg class="dl-mark" viewBox="0 0 100 100" role="img" [attr.aria-label]="variant === 'full' ? null : 'DAFTECH'">
        <!-- apex -->
        <path d="M50 4 L69 42 C62 36 52 33 40 33 Z" fill="var(--brand-red, #e0342b)" />
        <!-- left stroke -->
        <path d="M44 44 L44 96 L11 96 Z" fill="var(--brand-blue, #3457b2)" />
        <!-- right stroke with the counter-curve -->
        <path d="M62 46 L89 96 L53 96 C61 82 63 63 62 46 Z" fill="var(--brand-red, #e0342b)" />
      </svg>
      <span class="dl-text" *ngIf="variant === 'full'">
        <span class="dl-word">DAF<span class="dl-dash">-</span>TECH</span>
        <span class="dl-tag">Computer Engineering</span>
      </span>
    </span>
  `,
  styles: [
    `
      :host { display: inline-flex; }
      .dl {
        display: inline-flex;
        align-items: center;
        gap: calc(var(--dl-size) * 0.22);
        line-height: 1;
        color: inherit;
      }
      .dl-mark {
        width: var(--dl-size);
        height: var(--dl-size);
        display: block;
        overflow: visible;
      }
      .dl-full { flex-direction: column; gap: calc(var(--dl-size) * 0.18); }
      .dl-text { display: flex; flex-direction: column; align-items: center; gap: 0.25em; }
      .dl-word {
        font-weight: 700;
        letter-spacing: 0.16em;
        font-size: calc(var(--dl-size) * 0.34);
        color: currentColor;
      }
      .dl-dash { color: var(--brand-red, #e0342b); }
      .dl-tag {
        font-size: calc(var(--dl-size) * 0.16);
        letter-spacing: 0.1em;
        opacity: 0.65;
        color: currentColor;
      }
    `,
  ],
})
export class DaftechLogoComponent {
  @Input() size = 34;
  @Input() variant: 'mark' | 'full' = 'mark';
}
