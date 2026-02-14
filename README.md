# EntraRadius

A simple API that bridges FreeRADIUS with Microsoft Entra (Azure AD), enabling your RADIUS network to authenticate users against cloud identities.

## What Problem Does It Solve?

FreeRADIUS can't directly talk to Microsoft Entra for authentication. EntraRadius acts as a translator between them, allowing your network infrastructure (VPN, WiFi, etc.) to authenticate users using their Microsoft 365 credentials.

## How It Works

```
User Login → FreeRADIUS → EntraRadius API → Microsoft Entra
                              ↓
                         Memory Cache (for resilience)
```

1. **User tries to connect** to your network (VPN, WiFi, etc.)
2. **FreeRADIUS sends credentials** to EntraRadius via HTTP
3. **EntraRadius checks with Microsoft Entra** if credentials are valid
4. **On success**: User gets access + credentials are cached
5. **If Entra is down**: EntraRadius falls back to the cache

This two-tier approach ensures your network stays accessible even when Microsoft services are temporarily unavailable.

## Quick Start

```bash
# Configure your Entra details
# Edit appsettings.json with your TenantId and ClientId

# Run the API
dotnet run

# Test it
curl -X POST https://localhost:5001/api/radius/authenticate \
  -H "Content-Type: application/json" \
  -d '{"userName":"user@domain.com","password":"password"}'
```

See [CONFIGURATION.md](CONFIGURATION.md) for setup details.
