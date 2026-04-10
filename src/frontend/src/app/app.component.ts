import { Component, OnInit } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

/**
 * Root component of the application
 */
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'Portivio';

  constructor(private router: Router) {}

  ngOnInit(): void {
    // Update page title on route change
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        const title = this.getTitle(this.router.routerState.root);
        if (title) {
          document.title = title;
        }
      });
  }

  /**
   * Get page title from route data
   */
  private getTitle(route: any): string {
    let title = '';

    if (route.data && route.data['title']) {
      title = route.data['title'];
    }

    if (route.firstChild) {
      title = this.getTitle(route.firstChild) || title;
    }

    return title;
  }
}
