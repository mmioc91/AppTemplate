import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/guards';

// Sign-in pages stand alone; signed-in screens live inside the shell, behind the guard. Each screen, and the shell
// itself, loads the first time its route is visited, so the sign-in page doesn't download the rest. Titles name the
// browser tab, translated like every other text.
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: $localize`:@@title.login:Sign in · AppTemplate`,
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: $localize`:@@title.register:Register · AppTemplate`,
    loadComponent: () => import('./features/auth/register-page').then((m) => m.RegisterPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: $localize`:@@title.home:AppTemplate`,
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
