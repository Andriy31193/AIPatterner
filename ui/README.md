# AIPatterner UI

Production-ready React + TypeScript web application for managing the AIPatterner middleware service.

## Features

- **Authentication**: Login/register with JWT tokens
- **Dashboard**: Overview of reminder candidates and transitions
- **Reminder Management**: View, filter, and force-check reminder candidates
- **Event Feed**: Monitor incoming action events (requires backend endpoint)
- **User Management**: Admin interface for user CRUD (requires backend endpoints)
- **API Key Management**: Generate and manage API keys (requires backend endpoints)

## Tech Stack

- **Next.js 14** with App Router
- **React 18** with TypeScript
- **TanStack Query** for data fetching
- **Tailwind CSS** for styling
- **Axios** for HTTP requests

## Getting Started

### Prerequisites

- Node.js 20+
- npm or yarn

### Local Development

1. Install dependencies:
```bash
npm install
```

2. Create `.env.local` file:
```bash
NEXT_PUBLIC_API_URL=http://localhost:8080
```

3. Run development server:
```bash
npm run dev
```

4. Open [http://localhost:3000](http://localhost:3000)

### Docker

Build and run with docker-compose (from project root):
```bash
docker-compose up -d ui
```

Or build standalone:
```bash
docker build -t aipatterner-ui .
docker run -p 3000:3000 -e NEXT_PUBLIC_API_URL=http://localhost:8080 aipatterner-ui
```

## Project Structure

```
ui/
├── app/                    # Next.js app router pages
│   ├── dashboard/         # Dashboard page
│   ├── reminders/         # Reminder management
│   ├── events/            # Event feed
│   ├── users/             # User management (admin)
│   └── api-keys/          # API key management (admin)
├── components/            # Reusable UI components
├── context/               # React context providers
├── services/              # API service layer
├── types/                 # TypeScript interfaces
└── hooks/                 # Custom React hooks
```

## Environment Variables

- `NEXT_PUBLIC_API_URL`: Backend API URL (default: http://localhost:8080)
- `NEXT_PUBLIC_WS_URL`: WebSocket URL for real-time updates (optional)

## Authentication

The UI uses JWT tokens stored in localStorage. Tokens are automatically included in API requests via axios interceptors.

**Note**: Authentication endpoints (`/api/v1/auth/login`, `/api/v1/auth/register`) need to be implemented in the backend. Currently, the UI is configured to use API keys for authentication with the existing backend.

## API Integration

All API calls go through the centralized `apiService` in `services/api.ts`. The service handles:
- Authentication token management
- API key headers
- Error handling and redirects
- Request/response interceptors

## Testing

Run tests:
```bash
npm test
```

Run tests in watch mode:
```bash
npm run test:watch
```

## Building for Production

```bash
npm run build
npm start
```

## Notes

- Some features (user management, API key management, event feed) require additional backend endpoints
- The UI is designed to work with the AIPatterner .NET backend API
- Authentication endpoints (`/api/v1/auth/login`, `/api/v1/auth/register`) need to be implemented in the backend
- Admin role checking is client-side only; backend should enforce authorization
- For now, the UI uses API keys for authentication (set via browser localStorage or environment)
