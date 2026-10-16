import { Injectable } from '@angular/core';
import { REFRESH_TOKEN_KEY } from './storage-keys';

// Where the refresh token lives between visits: localStorage, shared by the tabs of this origin; the access token never
// leaves memory (ADR 0031, 0032). Storage may be unavailable (private mode, blocked) — then the session lasts as long
// as the tab.
@Injectable({ providedIn: 'root' })
export class TokenStore {
  read(): string | null {
    try {
      return localStorage.getItem(REFRESH_TOKEN_KEY);
    } catch {
      return null;
    }
  }

  write(refreshToken: string | null): void {
    try {
      if (refreshToken === null) {
        localStorage.removeItem(REFRESH_TOKEN_KEY);
      } else {
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
      }
    } catch {
      // No storage: nothing to remember.
    }
  }
}
