import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

// The root: everything is a route — the signed-in screens inside the shell, the sign-in pages outside it.
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
