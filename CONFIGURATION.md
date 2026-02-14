# EntraRadius Configuration Guide

## Overview
EntraRadius authenticates users against Microsoft Entra using the ROPC (Resource Owner Password Credentials) flow and caches successful authentications in memory.

## Configuration Steps

### 1. Azure/Entra Setup

1. **Register an application in Microsoft Entra**:
   - Go to Azure Portal > Microsoft Entra ID > App registrations
   - Click "New registration"
   - Name: `EntraRadius`
   - Supported account types: Choose based on your needs
   - Click "Register"

2. **Configure the application**:
   - Copy the **Application (client) ID**
   - Copy the **Directory (tenant) ID**
   - Go to "Authentication" > "Advanced settings"
   - Enable "Allow public client flows" (required for ROPC)
   - Click "Save"

3. **API Permissions** (if needed):
   - Go to "API permissions"
   - Add required permissions (e.g., User.Read)
   - Grant admin consent if necessary

### 2. Application Configuration

Update `appsettings.json` with your Entra details:

```json
{
  "EntraConfiguration": {
    "TenantId": "your-tenant-id-here",
    "ClientId": "your-client-id-here",
    "Scopes": [
      "https://graph.microsoft.com/.default"
    ],
    "CacheDurationMinutes": 60
  }
}
```

**Configuration Options**:
- `TenantId`: Your Azure tenant ID
- `ClientId`: Your application (client) ID
- `Scopes`: The scopes to request (typically `https://graph.microsoft.com/.default` for ROPC)
- `CacheDurationMinutes`: How long to cache successful authentications (default: 60 minutes)

### 3. Production Configuration

For production, use environment variables or Azure Key Vault instead of hardcoding credentials in `appsettings.json`:

```bash
# Environment variables
export EntraConfiguration__TenantId="your-tenant-id"
export EntraConfiguration__ClientId="your-client-id"
```

Or use `appsettings.Production.json`:
```json
{
  "EntraConfiguration": {
    "TenantId": "${ENTRA_TENANT_ID}",
    "ClientId": "${ENTRA_CLIENT_ID}"
  }
}
```

## API Usage

### Authentication Endpoint

**POST** `/api/radius/authenticate`

**Request Body**:
```json
{
  "userName": "user@domain.com",
  "password": "userpassword"
}
```

**Responses**:

- **200 OK** (Authenticated via Entra):
```json
{
  "message": "Authentication successful",
  "source": "entra"
}
```

- **200 OK** (Authenticated via cache fallback):
```json
{
  "message": "Authentication successful (fallback)",
  "source": "cache"
}
```

- **400 Bad Request** (Missing credentials):
```json
{
  "message": "Username and password are required"
}
```

- **401 Unauthorized** (Invalid credentials):
```json
{
  "message": "Authentication failed"
}
```

- **503 Service Unavailable** (Entra unreachable and not in cache):
```json
{
  "message": "Authentication failed - Entra is unreachable and user not found in cache"
}
```

## How It Works

1. **Primary Authentication**: The application attempts to authenticate against Microsoft Entra using ROPC
2. **Caching**: Successful authentications are cached in memory for the configured duration
3. **Fallback**: If Entra is unreachable, the application falls back to validating against the cache
4. **Security**: Passwords are hashed (SHA256) before being stored in cache

## Testing

```bash
# Start the application
dotnet run

# Test authentication (replace with your credentials)
curl -X POST https://localhost:5001/api/radius/authenticate \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "user@yourdomain.com",
    "password": "yourpassword"
  }'
```

## FreeRADIUS Integration

To integrate with FreeRADIUS, configure FreeRADIUS to call this API endpoint:

1. Configure FreeRADIUS to use `rlm_rest` module
2. Point it to `https://your-server/api/radius/authenticate`
3. Map RADIUS attributes to the JSON request format

## Important Notes

- **ROPC Flow Warning**: The ROPC flow is deprecated by Microsoft and should only be used when modern authentication flows (OAuth2/OIDC) are not feasible
- **Security**: Always use HTTPS in production
- **Cache**: The in-memory cache is lost when the application restarts
- **Scalability**: For multi-instance deployments, consider using a distributed cache (Redis) instead of in-memory cache
