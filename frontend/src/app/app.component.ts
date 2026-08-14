import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PwaManifestService } from './core/services/pwa-manifest.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet></router-outlet>`,
})
export class AppComponent {
  constructor(private pwaManifest: PwaManifestService) {
    this.pwaManifest.init();
  }
}