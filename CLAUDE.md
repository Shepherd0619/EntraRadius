# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EntraRadius is an ASP.NET Core 8.0 Web API that serves as a FreeRADIUS sidecar for authenticating users against Microsoft Entra (Azure AD) using the Resource Owner Password Credentials (ROPC) flow. It implements a two-tier authentication system with intelligent fallback caching.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application (development)
dotnet run

# Run with specific environment
dotnet run --environment Production

# Restore dependencies
dotnet restore

# Clean build artifacts
dotnet clean
```

The application runs on HTTPS by default. Check `Properties/launchSettings.json` for configured ports.

## Architecture

### Two-Tier Authentication Flow

The core architecture implements a resilient authentication pattern:

1. **Primary Authentication** (`GraphClientService.AuthenticateAsync`):
   - Attempts to authenticate against Microsoft Entra using MSAL (Microsoft.Identity.Client)
   - Uses ROPC flow via `AcquireTokenByUsernamePassword`
   - On success, returns true and triggers caching

2. **Caching Layer** (`UserCacheService`):
   - Stores successful authentications in IMemoryCache
   - Passwords are SHA256-hashed before storage
   - Cache duration configurable via `EntraConfiguration.CacheDurationMinutes`
   - Cache keys are normalized: `user:{username.ToLowerInvariant()}`

3. **Fallback Authentication**:
   - When `GraphClientService` throws `EntraServiceException` (network/service errors)
   - Controller catches exception and attempts cache validation
   - Returns 200 with `source: "cache"` on success
   - Returns 503 if both Entra and cache fail

### Service Dependency Injection

Services are registered in `Program.cs`:
- `IMemoryCache`: Singleton (framework-provided)
- `IUserCacheService` → `UserCacheService`: Singleton (cache must persist across requests)
- `GraphClientService`: Scoped (recreated per request)
- `EntraConfiguration`: Options pattern from appsettings.json

### Exception Handling Pattern

`GraphClientService` distinguishes between:
- `MsalServiceException`: Service/network errors → throws `EntraServiceException` (triggers fallback)
- `MsalClientException`: Client errors (wrong credentials) → returns false
- `MsalException`: General auth failures → returns false
- Other exceptions → throws `EntraServiceException`

This distinction is critical: only service-level failures trigger cache fallback, not authentication failures.

### API Contract

Single endpoint: `POST /api/radius/authenticate`

Request body:
```json
{
  "userName": "user@domain.com",
  "password": "userpassword"
}
```

Response status codes:
- 200: Success (check `source` field: "entra" or "cache")
- 400: Missing credentials
- 401: Invalid credentials (Entra reachable but auth failed)
- 503: Entra unreachable AND not in cache

## Configuration

Configuration is loaded from `appsettings.json` and environment-specific overrides. See `CONFIGURATION.md` for detailed setup instructions.

Key configuration section:
```json
{
  "EntraConfiguration": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "Scopes": ["https://graph.microsoft.com/.default"],
    "CacheDurationMinutes": 60
  }
}
```

**Important**: `Scopes` must include `https://graph.microsoft.com/.default` for ROPC flow to work.

## Testing the API

```bash
# Using curl
curl -X POST https://localhost:5001/api/radius/authenticate \
  -H "Content-Type: application/json" \
  -d '{"userName":"user@domain.com","password":"password"}'

# Using the included .http file
# Open EntraRadius.http in Visual Studio or use REST Client extension
```

## Project Structure

```
Controllers/
  RadiusController.cs       - Single API endpoint for authentication
Services/
  GraphClientService.cs     - Entra authentication via MSAL
  UserCacheService.cs       - In-memory cache implementation
  IUserCacheService.cs      - Cache service interface
Models/
  EntraConfiguration.cs     - Configuration POCO
  RadiusRequest.cs          - API request model
Program.cs                  - DI and middleware configuration
```

## Key Implementation Details

### MSAL Public Client Application

`GraphClientService` creates an `IPublicClientApplication` instance per service lifetime (scoped):
- Built with `PublicClientApplicationBuilder`
- Configured for Azure Public Cloud
- Uses tenant-specific authority

### Password Hashing

`UserCacheService.HashPassword()` uses SHA256:
```csharp
using var sha256 = System.Security.Cryptography.SHA256.Create();
var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
return Convert.ToBase64String(hashedBytes);
```

This is NOT for production password storage but for cache comparison. The actual passwords are validated by Entra.

### Logging

The application uses structured logging throughout:
- Information: Successful authentications, cache operations
- Warning: Failed authentications, missing credentials, cache misses
- Error: Service exceptions, unexpected errors

All logs include the username for traceability.

## Known Limitations

1. **In-Memory Cache**: Lost on application restart. For production multi-instance deployments, consider implementing a distributed cache (Redis) by creating a new `IUserCacheService` implementation.

2. **ROPC Flow**: Deprecated by Microsoft. This flow bypasses MFA and modern auth features. Only use when necessary for legacy RADIUS integration.

3. **No Rate Limiting**: Consider adding rate limiting to prevent brute force attacks.

4. **No Tests**: Project currently has no unit or integration tests.

## Development Notes

- The project uses nullable reference types (`<Nullable>enable</Nullable>`)
- Implicit usings are enabled
- Target framework: .NET 8.0
- All controllers, services, and models use explicit namespaces matching folder structure
