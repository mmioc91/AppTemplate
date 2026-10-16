import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

// The first screen after signing in — a placeholder for the product's own: replace it with the first feature's page.
@Component({
  selector: 'app-home-page',
  template: `
    <h1 i18n="@@home.title">Home</h1>
    @if (auth.user(); as user) {
      <p i18n="@@home.welcome">Signed in as {{ user.displayName }}.</p>
    }
  `,
  styles: `
    :host {
      display: block;
      padding: 24px 16px;
    }

    h1 {
      margin-top: 0;
      font: var(--mat-sys-headline-medium);
    }
  `,
})
export class HomePage {
  protected readonly auth = inject(AuthService);
}
