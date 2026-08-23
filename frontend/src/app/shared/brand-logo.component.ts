import { Component, Input } from '@angular/core';

/**
 * The DAFTECH brand mark, drawn natively as inline SVG instead of loading
 * assets/daftech-logo.png. The raster logo carried its own off-white,
 * grid-textured background, so it always read as a picture pasted on top of
 * the UI. This version is transparent, crisp at any size, inherits the
 * brand CSS variables (so it themes with the app), and needs no network
 * request.
 *
 * Usage:
 *   <app-brand-logo [size]="34"></app-brand-logo>                  mark only
 *   <app-brand-logo [size]="44" variant="full"></app-brand-logo>   mark + wordmark
 *   <app-brand-logo [size]="56" variant="full" tone="light">       for dark backgrounds
 */
@Component({
  selector: 'app-brand-logo',
  standalone: true,
  template: `
    <span class="brand-logo" [class.brand-logo-full]="variant === 'full'" [class.tone-light]="tone === 'light'">
      <svg
        class="mark"
        [attr.width]="size"
        [attr.height]="size"
        viewBox="0 0 100 100"
        role="img"
        [attr.aria-label]="variant === 'full' ? null : 'DAFTECH'"
        [attr.aria-hidden]="variant === 'full' ? 'true' : null"
      >
        <!-- top triangle -->
        <path d="M50 6 L69 40 Q57 33 43 36 Z" fill="var(--brand-red)" />
        <!-- bottom-left triangle -->
        <path d="M38 32 L38 78 L8 78 Z" fill="var(--brand-blue)" />
        <!-- bottom-right triangle (concave inner edge, mirrors the mark) -->
        <path d="M62 36 L92 78 L56 78 Q66 60 62 36 Z" fill="var(--brand-red)" />
      </svg>

      @if (variant === 'full') {
        <span class="wordmark">
          <span class="name">DAF-TECH</span>
          <span class="tagline">Computer Engineering</span>
        </span>
      }
    </span>
  `,
  styles: [`
    .brand-logo { display: inline-flex; align-items: center; gap: 0.6rem; line-height: 1; }
    .mark { display: block; flex-shrink: 0; }
    .wordmark { display: flex; flex-direction: column; gap: 0.15rem; }
    .name {
      font-weight: 700;
      letter-spacing: 0.14em;
      font-size: 1rem;
      color: var(--brand-charcoal);
      white-space: nowrap;
    }
    .tagline {
      font-size: 0.62rem;
      letter-spacing: 0.11em;
      text-transform: uppercase;
      color: var(--slate-500);
      white-space: nowrap;
    }
    .tone-light .name { color: #fff; }
    .tone-light .tagline { color: rgba(255, 255, 255, 0.72); }
  `],
})
export class BrandLogoComponent {
  /** Pixel size of the square mark. */
  @Input() size = 36;
  /** 'mark' shows the triangle only; 'full' adds the DAF-TECH wordmark. */
  @Input() variant: 'mark' | 'full' = 'mark';
  /** 'light' recolors the wordmark for dark backgrounds. */
  @Input() tone: 'dark' | 'light' = 'dark';
}
