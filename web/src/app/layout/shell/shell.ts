import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

// The frame of every signed-in screen: the toolbar and the screen. Navigation joins it with the first features — at
// 320 px the toolbar holds the app name and "Sign out" only, so links go into a menu or a side navigation there.
@Component({
  selector: 'app-shell',
  imports: [MatButtonModule, MatToolbarModule, RouterLink, RouterOutlet],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly signingOut = signal(false);

  protected signOut(): void {
    this.signingOut.set(true);
    this.auth
      .signOut()
      .pipe(finalize(() => this.signingOut.set(false)))
      .subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
