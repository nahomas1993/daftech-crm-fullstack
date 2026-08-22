import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ClientService } from '../../core/services/client.service';
import { LocationService } from '../../core/services/location.service';

@Component({
  selector: 'app-portal-signup',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="wrap">
      <div class="card panel panel-pad">
        @if (!submitted()) {
          <img src="assets/daftech-logo.png" alt="DAFTECH" class="brand-logo-img brand-logo-md" />
          <h2>Create your Client Portal account</h2>
          <p class="text-muted" style="margin: 0.35rem 0 1.25rem;">Your request will be reviewed by a DAFTECH Admin before you can log in.</p>

          <div class="field"><label>Name / Organization</label><input type="text" [ngModel]="form.name" (ngModelChange)="form.name = $event" /></div>
          <div class="field"><label>Phone Number</label><input type="text" [ngModel]="form.phoneNumber" (ngModelChange)="form.phoneNumber = $event" /></div>
          <div class="field"><label>Email</label><input type="email" [ngModel]="form.email" (ngModelChange)="form.email = $event" /></div>
          <div class="field"><label>Office</label><input type="text" [ngModel]="form.office" (ngModelChange)="form.office = $event" /></div>
          <div class="field"><label>Location</label><input type="text" [ngModel]="form.location" (ngModelChange)="form.location = $event" /></div>
          <div class="field"><label>Region</label>
            <select [ngModel]="form.region" (ngModelChange)="form.region = $event">
              <option value="">Select region…</option>
              @for (r of locations.options().regions; track r.id) {
                <option [value]="r.name">{{ r.name }}</option>
              }
            </select>
          </div>
          <div class="field"><label>City</label>
            <select [ngModel]="form.city" (ngModelChange)="form.city = $event">
              <option value="">Select city…</option>
              @for (c of locations.options().cities; track c.id) {
                <option [value]="c.name">{{ c.name }}</option>
              }
            </select>
          </div>
          <div class="field"><label>Woreda</label>
            <select [ngModel]="form.woreda" (ngModelChange)="form.woreda = $event">
              <option value="">Select woreda…</option>
              @for (w of locations.options().woredas; track w.id) {
                <option [value]="w.name">{{ w.name }}</option>
              }
            </select>
          </div>

          <button class="btn btn-primary" style="width:100%; margin-top:1rem;" (click)="submit()">Submit Request</button>
          <p class="alt-link">Already approved? <a routerLink="/login">Log in</a></p>
        } @else {
          <img src="assets/daftech-logo.png" alt="DAFTECH" class="brand-logo-img brand-logo-md" />
          <h2>Request submitted</h2>
          <p class="text-muted" style="margin-top:0.5rem; line-height:1.5;">
            Your signup request is awaiting Admin approval. You'll be notified once it's reviewed.
          </p>
          <a routerLink="/login" class="btn btn-secondary" style="margin-top:1.25rem; display:inline-block;">Back to Login</a>
        }
      </div>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: var(--portal-bg); padding: 1rem; }
    .card { width: 420px; text-align: center; }
    .card .field, .card label { text-align: left; }
    .brand-logo-img { margin: 0 auto 0.75rem; }
    .field { display: flex; flex-direction: column; gap: 0.25rem; margin-top: 0.7rem; }
    .field label { font-size: 0.78rem; font-weight: 600; color: var(--slate-500); }
    .field input, .field select { width: 100%; }
    .alt-link { font-size: 0.82rem; margin-top: 0.9rem; text-align: center; }
    .alt-link a { color: var(--portal-accent); font-weight: 600; }
  `],
})
export class PortalSignupComponent {
  submitted = signal(false);
  form = { name: '', phoneNumber: '', email: '', office: '', location: '', region: '', city: '', woreda: '' };

  constructor(private clients: ClientService, public locations: LocationService) {}

  async submit() {
    if (!this.form.name) return;
    await this.clients.submitSignup({ ...this.form });
    this.submitted.set(true);
  }
}
