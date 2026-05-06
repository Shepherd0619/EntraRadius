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

3. **API Permissions**:
   - Go to "API permissions"
   - Grant admin consent for `User.Read`.

4. **Add Redirect URI**
   - Go to "Authentication"
   - Add a redirect URI (e.g., `https://localhost:5001`, content doesn't matter).

> [!WARNING]
> Adding redirect URI is required to exclude this app from conditional access policies that block ROPC for MFA.
>
> If you don't add a redirect URI, and you configured require MFA for all users, ROPC will be blocked by default and you won't be able to authenticate.
>
> Redirect URI is not used in ROPC flow, but it must be added to allow ROPC to work when MFA is required.

5. **Grant `GroupMember.Read.All` permission** (required for VLAN group assignment):
   - Go to "API permissions" > "Add a permission" > "Microsoft Graph" > "Delegated permissions"
   - Search for and add `GroupMember.Read.All`
   - Click **Grant admin consent**

6. **Exclude Windows Azure Active Directory from Conditional Access**:
   - Go to Microsoft Entra ID > Protection > Conditional Access
   - Open any CA policy that enforces MFA or blocks legacy authentication
   - Under "Cloud apps or actions", add an exclusion for:
     **Windows Azure Active Directory** (`00000002-0000-0000-c000-000000000000`)
   - Save the policy

> [!WARNING]
> Excluding `Windows Azure Active Directory` from CA is required because the ROPC token
> acquisition and the subsequent Graph API call (`/me/memberOf`) both target this resource.
> If a CA policy applies to it, authentication will be blocked even if your EntraRadius app
> registration is excluded.

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
  },
  "VlanConfiguration": {
    "Mappings": [
      {
        // Corp-WiFi-VLAN100
        "GroupId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        "VlanId": 100
      }
    ],
    "DefaultVlanId": null
  }
}
```

**EntraConfiguration options**:
- `TenantId`: Your Azure tenant ID
- `ClientId`: Your application (client) ID
- `Scopes`: The scopes to request (typically `https://graph.microsoft.com/.default` for ROPC)
- `CacheDurationMinutes`: How long to cache successful authentications (default: 60 minutes)

**VlanConfiguration options**:
- `Mappings`: List of group-to-VLAN mappings. `GroupId` is the Entra group Object ID (GUID). First match wins when a user belongs to multiple mapped groups. Use a `//` comment above each entry to label it (e.g. the group display name) — .NET's config reader supports JSON comments.
- `DefaultVlanId`: VLAN ID assigned to users who authenticate successfully but belong to no mapped group. Set to `null` to send no VLAN attributes in that case (NAS uses its own default).

> [!WARNING]
> If your NAS (e.g. UniFi) has **VLAN assignment enabled** on the RADIUS profile, it delegates VLAN
> placement entirely to RADIUS. When no VLAN attributes are returned, the NAS falls back to the
> native VLAN (typically VLAN 1), **not** the VLAN configured on the wireless network.
>
> To avoid this, set `DefaultVlanId` to the VLAN ID you want as a fallback instead of leaving it `null`.

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

- **200 OK** (Authenticated via Entra, no VLAN mapping):
```json
{
  "message": "Authentication successful",
  "source": "entra"
}
```

- **200 OK** (Authenticated via Entra, VLAN mapping found):
```json
{
  "message": "Authentication successful",
  "source": "entra",
  "reply:Tunnel-Type:0": "VLAN",
  "reply:Tunnel-Medium-Type:0": "IEEE-802",
  "reply:Tunnel-Private-Group-Id:0": "100"
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
