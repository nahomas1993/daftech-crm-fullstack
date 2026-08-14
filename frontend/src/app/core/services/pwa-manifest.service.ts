import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class PwaManifestService {

  constructor(private router: Router) {}

  init(): void {
    // Disabled temporarily
    return;
  }
}