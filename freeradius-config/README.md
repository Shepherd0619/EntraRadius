# FreeRADIUS Configuration for EntraRadius

This directory contains FreeRADIUS configuration files to integrate with the EntraRadius API backend.

## Overview

These configurations enable FreeRADIUS to authenticate users against Microsoft Entra (Azure AD) by proxying authentication requests to the EntraRadius REST API.

## Files

### Core Configuration
- **clients.conf** - RADIUS client definitions (network devices that can send auth requests)
- **README.md** - This file

### Standard Deployment (PAP/CHAP Authentication)
- **mods-available/rest** - REST module configuration for standard deployments
- **sites-available/entra-radius** - Virtual server configuration for basic authentication

### Docker/Container Deployment
- **mods-available/rest.docker** - REST module configured for Docker container networking

### WiFi/EAP-TTLS Deployment (WPA3-Enterprise)
- **mods-available/eap-entra** - EAP/TTLS module configuration for WiFi authentication
- **sites-available/inner-tunnel-entra** - Inner tunnel virtual server for EAP-TTLS authentication

## Deployment Scenarios

Choose the installation method based on your use case:

### Scenario 1: Basic Authentication (PAP/CHAP)
Use for VPN, wired network access, or simple authentication needs.
- Files needed: `mods-available/rest`, `sites-available/entra-radius`, `clients.conf`
- See: [Standard Installation](#standard-installation)

### Scenario 2: WiFi/WPA3-Enterprise (EAP-TTLS)
Use for wireless networks with WPA3-Enterprise security.
- Files needed: `mods-available/rest`, `mods-available/eap-entra`, `sites-available/entra-radius`, `sites-available/inner-tunnel-entra`, `clients.conf`
- See: [EAP-TTLS/WiFi Setup](#eap-ttlswifi-setup)

### Scenario 3: Docker Container Deployment
Use when running FreeRADIUS and EntraRadius in Docker containers.
- Files needed: `mods-available/rest.docker`, `sites-available/entra-radius`, `clients.conf`
- See: [Docker Installation](#docker-installation)

## Installation

This section covers the standard installation. For WiFi/EAP-TTLS setup, see [EAP-TTLS/WiFi Setup](#eap-ttlswifi-setup). For Docker, see [Docker Installation](#docker-installation).

### Standard Installation

#### 1. Copy Configuration Files

Copy these files to your FreeRADIUS configuration directory (typically `/etc/freeradius/3.0/` on Linux):

```bash
# Copy the REST module configuration
sudo cp mods-available/rest /etc/freeradius/3.0/mods-available/rest

# Copy the virtual server configuration
sudo cp sites-available/entra-radius /etc/freeradius/3.0/sites-available/entra-radius

# Copy or merge the clients configuration
sudo cp clients.conf /etc/freeradius/3.0/clients.conf
# OR append to existing clients.conf:
# sudo cat clients.conf >> /etc/freeradius/3.0/clients.conf
```

#### 2. Enable the REST Module

```bash
# Create symbolic link to enable the module
sudo ln -s /etc/freeradius/3.0/mods-available/rest /etc/freeradius/3.0/mods-enabled/rest
```

### 3. Enable the Virtual Server

```bash
# Create symbolic link to enable the virtual server
sudo ln -s /etc/freeradius/3.0/sites-available/entra-radius /etc/freeradius/3.0/sites-enabled/entra-radius

# Optional: Disable the default site if only using EntraRadius
sudo rm /etc/freeradius/3.0/sites-enabled/default
```

### 4. Update Configuration

Edit `/etc/freeradius/3.0/mods-enabled/rest`:

```conf
# Update the connect_uri to point to your EntraRadius API endpoint
connect_uri = "https://your-entra-radius-server:5001"

# For production with proper SSL certificates:
http {
    tls {
        ca_file = "/path/to/ca-bundle.crt"
        check_cert = yes
        check_cert_cn = yes
    }
}

# For development with self-signed certificates (NOT for production):
http {
    tls {
        check_cert = no
        check_cert_cn = no
    }
}
```

Edit `/etc/freeradius/3.0/clients.conf`:

Add your network devices (switches, access points, VPN gateways) with their IP addresses and shared secrets.

### 5. Test Configuration

```bash
# Test the configuration syntax
sudo freeradius -CX

# Run FreeRADIUS in debug mode
sudo freeradius -X
```

### 6. Start FreeRADIUS

```bash
# Start the service
sudo systemctl start freeradius

# Enable automatic startup
sudo systemctl enable freeradius

# Check status
sudo systemctl status freeradius
```

## Testing

**Note**: This section covers testing for basic PAP/CHAP authentication (Scenario 1). For EAP-TTLS/WiFi testing (Scenario 2), see [Testing EAP-TTLS](#testing-eap-ttls). For Docker testing, see [Docker Installation](#docker-installation).

### Using radtest

Test basic PAP authentication from the FreeRADIUS server:

```bash
# Format: radtest username password radius-server port shared-secret
radtest user@domain.com userpassword localhost 1812 testing123
```

Expected output for successful authentication:
```
Sent Access-Request Id 123 from 0.0.0.0:12345 to 127.0.0.1:1812 length 77
        User-Name = "user@domain.com"
        User-Password = "userpassword"
        NAS-IP-Address = 127.0.0.1
        NAS-Port = 1812
        Message-Authenticator = 0x00
Received Access-Accept Id 123 from 127.0.0.1:1812 to 127.0.0.1:12345 length 20
```

### Using radclient

More advanced testing:

```bash
echo "User-Name = user@domain.com, User-Password = userpassword" | \
  radclient localhost:1812 auth testing123
```

### Debug Mode

Run FreeRADIUS in debug mode to see detailed request/response flow:

```bash
sudo systemctl stop freeradius
sudo freeradius -X
```

Then test with radtest in another terminal.

**Important**: `radtest` and `radclient` only work for basic PAP/CHAP authentication. They **cannot** test EAP-TTLS because they don't perform EAP negotiation or TLS tunnel establishment. For WiFi/WPA3-Enterprise testing, use `eapol_test` as described in [Testing EAP-TTLS](#testing-eap-ttls).

## Configuration Details

### Authentication Flow

1. **Client Request** - Network device sends RADIUS Access-Request to FreeRADIUS
2. **Authorize Phase** - FreeRADIUS processes the request and sets Auth-Type to `rest`
3. **Authenticate Phase** - FreeRADIUS calls the REST module
4. **REST API Call** - REST module sends POST request to EntraRadius API at `/api/radius/authenticate`
5. **EntraRadius Processing** - API authenticates against Microsoft Entra or cache
6. **Response Mapping** - HTTP status codes mapped to RADIUS responses:
   - 200 → Access-Accept
   - 401 → Access-Reject
   - 400 → Access-Reject
   - 503 → Access-Reject (with retry possible)
7. **Post-Auth** - FreeRADIUS sends Access-Accept or Access-Reject to client

### HTTP Status Code Handling

The EntraRadius API returns these status codes:

- **200 OK** - Authentication successful (via Entra or cache)
  - FreeRADIUS returns: Access-Accept

- **401 Unauthorized** - Invalid credentials
  - FreeRADIUS returns: Access-Reject

- **400 Bad Request** - Missing username or password
  - FreeRADIUS returns: Access-Reject

- **503 Service Unavailable** - Entra unreachable and user not in cache
  - FreeRADIUS returns: Access-Reject (user may retry later)

### Username Format

Microsoft Entra requires one of these username formats:

- User Principal Name: `user@domain.com`
- Entra format: `user@tenant.onmicrosoft.com`

Configure your RADIUS clients to send usernames in the correct format.

## Troubleshooting

### Connection Refused

If you see "Connection refused" errors:

1. Ensure EntraRadius API is running: `dotnet run` from the project directory
2. Check the API is listening on the configured port (default: 5001)
3. Verify the `connect_uri` in the REST module configuration

### TLS/SSL Certificate Errors

For development with self-signed certificates:

```conf
http {
    tls {
        check_cert = no
        check_cert_cn = no
    }
}
```

For production, use proper CA-signed certificates and set:

```conf
http {
    tls {
        ca_file = "/path/to/ca-bundle.crt"
        check_cert = yes
        check_cert_cn = yes
    }
}
```

### Authentication Failures

1. Check FreeRADIUS debug output: `sudo freeradius -X`
2. Check EntraRadius API logs for authentication attempts
3. Verify the username format is correct (user@domain.com)
4. Ensure the user exists in Microsoft Entra
5. Check that ROPC flow is enabled for the Entra application

### Performance Issues

Adjust the REST module connection pool settings:

```conf
pool {
    start = 10       # Initial connections
    min = 5          # Minimum connections
    max = 20         # Maximum connections
    spare = 5        # Spare connections
}
```

## Security Considerations

1. **TLS/HTTPS** - Always use HTTPS between FreeRADIUS and EntraRadius API in production
2. **Shared Secrets** - Use strong, unique shared secrets for each RADIUS client
3. **Message Authenticator** - Enable `require_message_authenticator = yes` for all clients
4. **IP Restrictions** - Limit RADIUS clients to specific IP addresses, not broad subnets
5. **Firewall Rules** - Restrict access to RADIUS ports (1812/1813) to authorized devices only
6. **Certificate Validation** - Always validate SSL certificates in production
7. **Logging** - Monitor authentication attempts for suspicious activity

## EAP-TTLS/WiFi Setup

This section covers configuration for WPA3-Enterprise wireless networks using EAP-TTLS authentication.

### Overview

EAP-TTLS (Extensible Authentication Protocol - Tunneled Transport Layer Security) creates an encrypted TLS tunnel between the client and RADIUS server. Inside this tunnel, user credentials are sent securely using PAP to the EntraRadius API.

### Installation Steps

#### 1. Copy All Required Files

```bash
# Copy the REST module configuration
sudo cp mods-available/rest /etc/freeradius/3.0/mods-available/rest

# Copy the EAP module configuration
sudo cp mods-available/eap-entra /etc/freeradius/3.0/mods-available/eap

# Copy both virtual server configurations
sudo cp sites-available/entra-radius /etc/freeradius/3.0/sites-available/entra-radius
sudo cp sites-available/inner-tunnel-entra /etc/freeradius/3.0/sites-available/inner-tunnel-entra

# Copy or merge the clients configuration
sudo cp clients.conf /etc/freeradius/3.0/clients.conf
```

#### 2. Enable Required Modules

```bash
# Enable the REST module
sudo ln -s /etc/freeradius/3.0/mods-available/rest /etc/freeradius/3.0/mods-enabled/rest

# Enable the EAP module
sudo ln -s /etc/freeradius/3.0/mods-available/eap /etc/freeradius/3.0/mods-enabled/eap
```

#### 3. Enable Virtual Servers

```bash
# Enable the main virtual server
sudo ln -s /etc/freeradius/3.0/sites-available/entra-radius /etc/freeradius/3.0/sites-enabled/entra-radius

# Enable the inner tunnel virtual server
sudo ln -s /etc/freeradius/3.0/sites-available/inner-tunnel-entra /etc/freeradius/3.0/sites-enabled/inner-tunnel-entra

# Optional: Disable default sites
sudo rm /etc/freeradius/3.0/sites-enabled/default
sudo rm /etc/freeradius/3.0/sites-enabled/inner-tunnel
```

#### 4. Configure TLS Certificates

For production, use properly signed certificates. For testing, use FreeRADIUS default certificates:

```bash
# Default certificates are located at:
# /etc/freeradius/3.0/certs/server.pem
# /etc/freeradius/3.0/certs/server.key
# /etc/freeradius/3.0/certs/ca.pem

# For production, replace these with your own certificates
sudo cp your-server.pem /etc/freeradius/3.0/certs/server.pem
sudo cp your-server.key /etc/freeradius/3.0/certs/server.key
sudo cp your-ca.pem /etc/freeradius/3.0/certs/ca.pem
```

#### 5. Update REST Module Configuration

Edit `/etc/freeradius/3.0/mods-enabled/rest` and set the `connect_uri` to your EntraRadius API endpoint (same as standard installation).

#### 6. Update Clients Configuration

Add your wireless access points to `/etc/freeradius/3.0/clients.conf`:

```conf
client wireless_ap {
    ipaddr = 192.168.1.10
    secret = your-strong-shared-secret
    require_message_authenticator = yes
    nas_type = other
}
```

#### 7. Test and Start

```bash
# Test configuration
sudo freeradius -CX

# Run in debug mode
sudo freeradius -X

# Start the service
sudo systemctl start freeradius
sudo systemctl enable freeradius
```

### WiFi Client Configuration

Configure your WiFi clients (laptops, phones, etc.) with these settings:

- **Security**: WPA3-Enterprise (or WPA2-Enterprise for compatibility)
- **Authentication Method**: PEAP or TTLS
- **Inner Authentication**: PAP
- **Username**: `user@domain.com` (full UPN)
- **Password**: User's Entra password
- **CA Certificate**: Your RADIUS server's CA certificate (for production)
- **Anonymous Identity**: `anonymous@domain.com` (optional, for privacy)

### Testing EAP-TTLS

Use `eapol_test` for testing:

```bash
# Create a test configuration file
cat > eapol-test.conf <<EOF
network={
    ssid="test"
    key_mgmt=WPA-EAP
    eap=TTLS
    identity="user@domain.com"
    anonymous_identity="anonymous@domain.com"
    password="userpassword"
    phase2="auth=PAP"
    ca_cert="/etc/freeradius/certs/ca.pem"
}
EOF

# Run the test
eapol_test -c eapol-test.conf -a 127.0.0.1 -p 1812 -s testing123
```

Expected output includes:
```
EAP: Status notification: remote certificate verification (param=success)
CTRL-EVENT-EAP-SUCCESS EAP authentication completed successfully
```

### Troubleshooting EAP-TTLS

1. **Certificate Errors**: Check that TLS certificates are valid and properly configured in `mods-available/eap`
2. **Inner Authentication Fails**: Verify the inner-tunnel-entra virtual server is enabled and REST module is working
3. **Debug Mode**: Run `sudo freeradius -X` and look for EAP and TLS-related messages
4. **Client Issues**: Ensure clients trust your RADIUS server's CA certificate

## Docker Installation

This section covers running FreeRADIUS and EntraRadius in Docker containers.

### Prerequisites

- Docker and Docker Compose installed
- EntraRadius API running in a container named `entraradius` on port 8080
- Both containers on the same Docker network

### Installation Steps

#### 1. Copy Docker-Specific Configuration

```bash
# Copy the Docker REST module configuration
sudo cp mods-available/rest.docker /etc/freeradius/3.0/mods-available/rest

# Copy the virtual server configuration
sudo cp sites-available/entra-radius /etc/freeradius/3.0/sites-available/entra-radius

# Copy the clients configuration
sudo cp clients.conf /etc/freeradius/3.0/clients.conf
```

#### 2. Key Differences from Standard Installation

The `rest.docker` file uses:
- **HTTP instead of HTTPS**: `http://entraradius:8080` (containers communicate over internal network)
- **Container hostname**: `entraradius` instead of `localhost` or IP address
- **No TLS configuration**: TLS not needed for internal container-to-container communication

#### 3. Docker Compose Example

```yaml
version: '3.8'

services:
  entraradius:
    image: your-entraradius-image:latest
    container_name: entraradius
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - EntraConfiguration__TenantId=${TENANT_ID}
      - EntraConfiguration__ClientId=${CLIENT_ID}
      - EntraConfiguration__CacheDurationMinutes=60
    networks:
      - radius-net

  freeradius:
    image: freeradius/freeradius-server:latest
    container_name: freeradius
    ports:
      - "1812:1812/udp"
      - "1813:1813/udp"
    volumes:
      - ./freeradius-config/mods-available/rest.docker:/etc/freeradius/3.0/mods-available/rest
      - ./freeradius-config/sites-available/entra-radius:/etc/freeradius/3.0/sites-available/entra-radius
      - ./freeradius-config/clients.conf:/etc/freeradius/3.0/clients.conf
    depends_on:
      - entraradius
    networks:
      - radius-net

networks:
  radius-net:
    driver: bridge
```

#### 4. Enable Modules and Sites

Inside the FreeRADIUS container:

```bash
docker exec -it freeradius bash

# Enable REST module
ln -s /etc/freeradius/3.0/mods-available/rest /etc/freeradius/3.0/mods-enabled/rest

# Enable virtual server
ln -s /etc/freeradius/3.0/sites-available/entra-radius /etc/freeradius/3.0/sites-enabled/entra-radius

# Restart FreeRADIUS
kill -HUP 1
```

#### 5. Testing

```bash
# Test from host machine
radtest user@domain.com userpassword localhost 1812 testing123

# Test from inside container
docker exec -it freeradius radtest user@domain.com userpassword localhost 1812 testing123
```

### Docker Security Notes

1. **Internal Network Only**: The `http://entraradius:8080` endpoint should only be accessible within the Docker network
2. **External Access**: If exposing RADIUS to external networks, use proper firewall rules
3. **Secrets Management**: Use Docker secrets or environment variables for sensitive configuration
4. **TLS for External**: If RADIUS clients are outside the Docker host, consider TLS/DTLS

## Production Deployment

For production deployments:

1. Use a reverse proxy (nginx, HAProxy) in front of the EntraRadius API
2. Enable proper SSL/TLS with CA-signed certificates
3. Implement rate limiting to prevent brute force attacks
4. Use a distributed cache (Redis) instead of in-memory cache for multi-instance deployments
5. Set up monitoring and alerting for authentication failures
6. Implement log aggregation (ELK stack, Splunk, etc.)
7. Consider high availability with multiple FreeRADIUS and EntraRadius instances
8. Regular security audits and updates
9. For WPA3-Enterprise: Ensure proper certificate management and rotation
10. For Docker: Use orchestration tools (Kubernetes, Docker Swarm) for scaling and resilience

## References

- [FreeRADIUS Documentation](https://freeradius.org/documentation/)
- [FreeRADIUS REST Module](https://networkradius.com/doc/current/raddb/mods-available/rest.html)
- [EntraRadius Project](../README.md)
- [Microsoft Entra ROPC Flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth-ropc)
