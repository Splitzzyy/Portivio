# Portivio - Financial Portfolio Management Application

[![Frontend CI](https://github.com/Splitzzyy/Portivio/actions/workflows/frontend.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/frontend.yml)

An enterprise-level financial portfolio management application built with **Angular 18** and **Bun** runtime. Track investments, SIPs, and asset performance across multiple user profiles with enterprise-grade security.

## 🎯 Project Overview

Portivio is a comprehensive portfolio management system designed for:
- **Multi-user profiles**: Manage portfolios for family members
- **Investment tracking**: Track stocks, mutual funds, bonds, and other assets
- **SIP management**: Systematic Investment Plan tracking and monitoring
- **Performance analytics**: Real-time portfolio performance and asset allocation
- **Security**: Enterprise-level authentication and data protection

## 📁 Project Structure

```
frontend/
├── src/
│   ├── app/
│   │   ├── core/                    # Singleton services, guards, interceptors
│   │   │   ├── models/              # Data models and interfaces
│   │   │   │   ├── auth.model.ts
│   │   │   │   └── portfolio.model.ts
│   │   │   ├── services/            # Core services
│   │   │   │   └── auth.service.ts
│   │   │   ├── guards/              # Route guards
│   │   │   │   └── auth.guard.ts
│   │   │   ├── interceptors/        # HTTP interceptors
│   │   │   │   └── jwt.interceptor.ts
│   │   │   └── core.module.ts
│   │   │
│   │   ├── shared/                  # Shared components and utilities
│   │   │   └── shared.module.ts
│   │   │
│   │   ├── features/                # Feature modules
│   │   │   ├── auth/                # Authentication module
│   │   │   │   ├── pages/
│   │   │   │   │   ├── login/
│   │   │   │   │   ├── signup/
│   │   │   │   │   ├── forgot-password/
│   │   │   │   │   └── reset-password/
│   │   │   │   ├── auth.module.ts
│   │   │   │   └── auth-routing.module.ts
│   │   │   │
│   │   │   └── home/                # Home/Dashboard module (protected)
│   │   │       ├── pages/
│   │   │       │   ├── home/        # Main layout
│   │   │       │   └── dashboard/   # Dashboard view
│   │   │       ├── home.module.ts
│   │   │       └── home-routing.module.ts
│   │   │
│   │   ├── app.component.ts
│   │   ├── app.component.html
│   │   ├── app.component.scss
│   │   ├── app.module.ts
│   │   └── app-routing.module.ts
│   │
│   ├── environments/                 # Environment configurations
│   │   ├── environment.ts           # Development
│   │   └── environment.prod.ts      # Production
│   │
│   ├── styles/                       # Global styles
│   │   └── global.scss
│   │
│   ├── main.ts                       # Application entry point
│   └── index.html
│
├── angular.json                      # Angular CLI configuration
├── tsconfig.json                     # TypeScript configuration
├── tsconfig.app.json
├── tsconfig.spec.json
├── package.json
├── bunfig.toml                       # Bun configuration
└── README.md
```

## 🚀 Getting Started

### Prerequisites
- **Node.js** 18+ or **Bun** 1.0+
- **Git**

### Installation

1. **Install dependencies** using Bun:
```bash
cd src/frontend
bun install
```

Or using npm:
```bash
npm install
```

### Development Server

Run the development server:
```bash
bun start
# or
ng serve
# or
npm run dev
```

Navigate to `http://localhost:4200/`. The application will auto-reload when you modify files.

### Production Build

Build for production:
```bash
bun run prod
# or
ng build --configuration production
# or
npm run prod
```

The build artifacts will be stored in the `dist/portivio` directory.

## 🏗️ Architecture

### Modules Architecture

#### Core Module
- **Auth Service**: Handles authentication, token management, SSO
- **Guards**: Route protection (AuthGuard, NoAuthGuard)
- **Interceptors**: JWT token attachment, token refresh, error handling
- **Models**: TypeScript interfaces for type safety

#### Auth Module
- **Login Page**: Traditional login + SSO (Google, Microsoft)
- **Signup Page**: User registration with password strength validation
- **Forgot Password**: Password reset email request
- **Reset Password**: Token-based password reset

#### Home Module
- **Home Component**: Main layout with sidebar and header
- **Dashboard Component**: Portfolio overview, asset allocation, recent activity

### Security Architecture

1. **JWT Token Management**
   - Automatic token refresh using refresh tokens
   - Secure token storage in localStorage
   - Token expiration handling

2. **Route Guards**
   - Protected routes require authentication
   - Redirect unauthenticated users to login
   - Prevent authenticated users from accessing auth pages

3. **HTTP Interceptor**
   - Automatically attach JWT token to requests
   - Handle 401 responses with token refresh
   - Error handling and logging

### Service Architecture

```typescript
// Example: Authentication Flow
1. User submits login credentials
2. AuthService sends request to backend
3. Backend validates and returns JWT token
4. AuthService stores token and user data
5. JwtInterceptor attaches token to subsequent requests
6. AuthGuard protects routes based on authentication state
```

## 🎨 UI/UX Features

### Responsive Design
- Mobile-first approach
- Tablet and desktop layouts
- Hamburger menu for mobile navigation

### Modern Components
- **Login Page**: Gradient background, SSO buttons, form validation
- **Signup Page**: Password strength indicator, multiple validations
- **Dashboard**: Statistics cards, asset allocation chart, transaction table
- **Navigation**: Collapsible sidebar, user dropdown menu

### Accessibility
- ARIA labels for screen readers
- Keyboard navigation support
- Focus indicators
- Proper color contrast ratios

## 📋 Features

### Authentication
✅ Traditional email/password login
✅ SSO (Google, Microsoft, GitHub)
✅ User registration with validation
✅ Forgot password functionality
✅ Password reset with token validation
✅ Remember me functionality
✅ Session management with token refresh
✅ Two-factor authentication ready (for future implementation)

### Portfolio Management
✅ Multi-profile support
✅ Asset allocation tracking
✅ Portfolio performance metrics
✅ Transaction history
✅ Real-time portfolio value updates
✅ Risk profile assessment

### Dashboard
✅ Portfolio overview cards
✅ Asset allocation visualization
✅ Performance charts
✅ Recent activity feed
✅ Quick action buttons

## 🔧 Configuration

### Environment Variables

**Development** (`environment.ts`):
```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:3000/api',
  oauth: {
    google: { clientId: 'YOUR_GOOGLE_CLIENT_ID', ... },
    microsoft: { clientId: 'YOUR_MICROSOFT_CLIENT_ID', ... }
  }
};
```

**Production** (`environment.prod.ts`):
```typescript
export const environment = {
  production: true,
  apiUrl: 'https://api.portivio.com/api',
  oauth: { ... }
};
```

### Runtime Configuration

Update `bunfig.toml` for build optimization:
```toml
[build]
loader = { tsx = "tsx", ts = "tsx", jsx = "jsx", js = "jsx" }
target = "browser"
splitting = true
```

## 🔌 API Integration

### Backend API Endpoints

```
Authentication:
POST   /auth/login              - Login
POST   /auth/signup             - Register
POST   /auth/sso-login          - SSO login
POST   /auth/logout             - Logout
POST   /auth/forgot-password    - Request password reset
POST   /auth/reset-password     - Reset password
POST   /auth/refresh-token      - Refresh JWT token
POST   /auth/validate-reset-token - Validate reset token

Portfolio:
GET    /portfolio               - Get user portfolios
GET    /portfolio/:id           - Get portfolio details
POST   /portfolio               - Create portfolio
PUT    /portfolio/:id           - Update portfolio

Assets:
GET    /assets                  - Get user assets
POST   /assets                  - Add asset
PUT    /assets/:id              - Update asset
DELETE /assets/:id              - Delete asset
```

## 📦 Dependencies

### Core Dependencies
- **@angular/core**: ^18.0.0
- **@angular/router**: ^18.0.0
- **@angular/forms**: ^18.0.0
- **rxjs**: ^7.8.0

### UI Framework
- **bootstrap**: ^5.3.0
- **@fortawesome/fontawesome-free**: ^6.5.0

### Optional Libraries (for future enhancement)
- **chart.js**: Portfolio performance charts
- **ngx-oauth2-oidc**: Advanced OAuth2/OIDC flows
- **ngx-toastr**: Toast notifications
- **ng-zorro**: Custom UI components

## 🧪 Testing

Run unit tests:
```bash
ng test
```

Run e2e tests:
```bash
ng e2e
```

## 📚 Best Practices Implemented

1. **Lazy Loading**: Feature modules are lazy-loaded for better performance
2. **Standalone Components**: Preparation for Angular standalone components (future upgrade)
3. **Typed Services**: Full TypeScript typing for type safety
4. **Reactive Forms**: FormBuilder and Reactive Forms pattern
5. **State Management**: BehaviorSubject for state management
6. **Error Handling**: Comprehensive error handling and user feedback
7. **Security**: HTTPS, JWT tokens, secure password policies
8. **Performance**: Tree-shaking, lazy loading, code splitting
9. **Responsive Design**: Mobile-first responsive layout
10. **Accessibility**: WCAG 2.1 compliance

## 🔐 Security Measures

1. **Authentication**
   - MD5/SHA256 hashing for passwords
   - JWT tokens with expiration
   - Refresh token rotation

2. **Data Protection**
   - HTTPS encryption in transit
   - Secure token storage
   - XSS protection
   - CSRF token implementation

3. **Authorization**
   - Route-based access control
   - Role-based access (future)
   - Principle of least privilege

## 📝 Environment Setup

### Local Development Setup

1. Clone repository:
```bash
git clone https://github.com/yourusername/portivio.git
cd portivio/src/frontend
```

2. Install dependencies:
```bash
bun install
```

3. Update environment variables:
```bash
# Update src/environments/environment.ts with your OAuth credentials
```

4. Start development server:
```bash
bun start
```

5. Access application:
```
http://localhost:4200
```

## 🚀 Deployment

### Deploy to Vercel
```bash
bun run build
vercel --prod
```

### Deploy to Netlify
```bash
bun run build
netlify deploy --prod --dir=dist/portivio
```

### Docker Deployment
```dockerfile
FROM node:18-alpine as build
WORKDIR /app
COPY . .
RUN bun install && bun run prod

FROM nginx:alpine
COPY --from=build /app/dist/portivio /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

## 🤝 Contributing

1. Create a feature branch: `git checkout -b feature/amazing-feature`
2. Commit changes: `git commit -m 'Add amazing feature'`
3. Push to branch: `git push origin feature/amazing-feature`
4. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see LICENSE file for details.

## 📞 Support

For support, email support@portivio.com or open an issue in the repository.

## 🙏 Acknowledgments

- Angular team for the excellent framework
- Bun for the fast JavaScript runtime
- Bootstrap for UI components
- FontAwesome for icons

---

**Built with ❤️ using Angular and Bun**
