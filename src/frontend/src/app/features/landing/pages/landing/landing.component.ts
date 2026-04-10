import { Component, HostListener } from '@angular/core';

/**
 * Public marketing landing page. Unauthenticated. Sections:
 *   1. Sticky top nav (logo + Login/Sign Up CTAs)
 *   2. Hero (headline, subcopy, dual CTAs, SVG illustration)
 *   3. Features grid
 *   4. How it works
 *   5. CTA band
 *   6. Footer
 */
@Component({
  selector: 'app-landing',
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss']
})
export class LandingComponent {
  mobileMenuOpen = false;
  scrolled = false;
  currentYear = new Date().getFullYear();

  features = [
    {
      icon: 'fa-chart-line',
      title: 'Track Investments',
      description:
        'Monitor stocks, mutual funds, bonds and cash positions in one place. Live performance, allocation and gains — at a glance.'
    },
    {
      icon: 'fa-calendar-check',
      title: 'SIP Management',
      description:
        'Automate your Systematic Investment Plans. Never miss a contribution, and visualise how each SIP compounds over time.'
    },
    {
      icon: 'fa-users',
      title: 'Multi-Profile Support',
      description:
        'Manage portfolios for yourself and every family member from a single login. Each profile has its own dashboard and history.'
    }
  ];

  steps = [
    {
      number: 1,
      title: 'Create your account',
      description: 'Sign up in under a minute with email or Google. No credit card, no setup fees.'
    },
    {
      number: 2,
      title: 'Add your portfolio',
      description: 'Import or enter your holdings across brokers. Portivio tracks cost basis and current value automatically.'
    },
    {
      number: 3,
      title: 'Grow with confidence',
      description: 'Watch performance unfold in real time, rebalance on insight, and plan SIPs for the long haul.'
    }
  ];

  @HostListener('window:scroll')
  onScroll(): void {
    this.scrolled = window.scrollY > 12;
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen = false;
  }

  scrollToSection(id: string, event: Event): void {
    event.preventDefault();
    this.closeMobileMenu();
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
