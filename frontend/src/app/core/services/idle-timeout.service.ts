import { Injectable, NgZone } from '@angular/core';

const IDLE_TIMEOUT_MS = 15 * 60 * 1000; // 15 minutes, per the security requirement
const ACTIVITY_EVENTS = ['mousedown', 'mousemove', 'keydown', 'scroll', 'touchstart', 'wheel'] as const;

/**
 * Force-logs-out the current user (Employee or Client) after 15 minutes of
 * no mouse/keyboard/touch/scroll activity, regardless of whether API calls
 * are still happening in the background. This is distinct from
 * SessionService's heartbeat (which just reports the user as online/active
 * for admin visibility) and from the JWT's own 15-minute access-token
 * expiry (which the auth interceptor silently refreshes and would
 * otherwise keep a genuinely idle session alive forever).
 *
 * Listens at the document level with passive, capturing listeners outside
 * Angular's zone (so mouse-move doesn't trigger change detection on every
 * pixel) and only re-enters the zone to actually run the timeout callback.
 */
@Injectable({ providedIn: 'root' })
export class IdleTimeoutService {
  private timerHandle: ReturnType<typeof setTimeout> | null = null;
  private boundReset = () => this.resetTimer();
  private onTimeout: (() => void) | null = null;
  private listening = false;

  constructor(private zone: NgZone) {}

  /** Starts watching for inactivity. Call once after a successful login (and on session restore). */
  start(onTimeout: () => void): void {
    this.onTimeout = onTimeout;
    this.resetTimer();

    if (!this.listening) {
      this.zone.runOutsideAngular(() => {
        for (const evt of ACTIVITY_EVENTS) {
          document.addEventListener(evt, this.boundReset, { passive: true, capture: true });
        }
      });
      this.listening = true;
    }
  }

  /** Stops watching — call on logout so a signed-out tab doesn't keep firing timers. */
  stop(): void {
    if (this.timerHandle !== null) {
      clearTimeout(this.timerHandle);
      this.timerHandle = null;
    }
    if (this.listening) {
      for (const evt of ACTIVITY_EVENTS) {
        document.removeEventListener(evt, this.boundReset, { capture: true });
      }
      this.listening = false;
    }
    this.onTimeout = null;
  }

  private resetTimer(): void {
    if (this.timerHandle !== null) clearTimeout(this.timerHandle);
    this.timerHandle = setTimeout(() => {
      // Re-enter Angular's zone so the logout callback's router navigation
      // and signal updates actually trigger change detection.
      this.zone.run(() => this.onTimeout?.());
    }, IDLE_TIMEOUT_MS);
  }
}
