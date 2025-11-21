# EcAuth.MockIdP

Mock OpenID Provider for E2E testing with EcAuth Identity Provider.

## Overview

EcAuth.MockIdP is a mock OpenID Connect Provider designed specifically for E2E testing of the EcAuth Identity Provider. It simulates external Identity Providers (like Google, LINE, Facebook) in a controlled environment.

### Key Features

- **Multi-tenant Support**: Logical separation by Organization (dev/staging/production)
- **OpenID Connect Flow**: Full OAuth2/OIDC authorization code flow implementation
- **Token Management**: Authorization codes, access tokens, and refresh tokens
- **Azure Deployment**: Runs on Azure Container Apps + Azure SQL Database (Free tier)
- **Cost-Optimized**: Monthly cost ~¥75 using Azure free tier

## Architecture

```
Organization (Tenant)
  ├─ MockIdpUser (Users)
  ├─ Client (OAuth2 Clients)
  ├─ AuthorizationCode
  ├─ AccessToken
  └─ RefreshToken
```

### Multi-tenant Design

- Organization-based logical separation (no physical database separation)
- Query filters automatically applied to all database operations
- Organization specified via `?org=<tenant>` query parameter or `X-Organization` header

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- SQL Server 2022 (local) or Azure SQL Database (production)

### Local Development

1. **Clone the repository**

```bash
git clone https://github.com/EcAuth/EcAuth.MockIdP.git
cd EcAuth.MockIdP
```

2. **Configure GitHub Packages authentication**

This project depends on `EcAuth.IdpUtilities` NuGet package hosted on GitHub Packages.

```bash
# Using GitHub CLI
echo "protocol=https
host=github.com" | gh auth git-credential get | awk -F= '/username/ {u=$2} /password/ {p=$2} END {system("dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json --name github --username " u " --password " p " --store-password-in-clear-text")}'
```

3. **Setup environment variables**

```bash
cp .env.dist .env
# Edit .env and configure database connection
```

4. **Build and test**

```bash
# Build
dotnet build EcAuth.MockIdP.sln

# Run unit tests
dotnet test
```

5. **Apply database migrations**

```bash
cd src/MockOpenIdProvider
export $(cat ../../.env | grep -v '^#' | xargs)
dotnet ef database update
```

6. **Run the application**

```bash
cd src/MockOpenIdProvider
dotnet run
```

The API will be available at `https://localhost:5001` (or the port specified in `launchSettings.json`).

## API Endpoints

### Authorization Endpoint

```
GET /authorization?org={organization}
  &response_type=code
  &client_id={client_id}
  &redirect_uri={redirect_uri}
  &scope={scope}
  &state={state}
```

### Token Endpoint

```
POST /token?org={organization}
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&code={authorization_code}
&redirect_uri={redirect_uri}
&client_id={client_id}
&client_secret={client_secret}
```

### UserInfo Endpoint

```
GET /userinfo?org={organization}
Authorization: Bearer {access_token}
```

## Organization Management

Organizations are logically separated tenants within the same database:

- **dev**: Development environment
- **staging**: Staging environment
- **production**: Production environment

Specify the organization using query parameter:

```
https://mock-idp.azurecontainerapps.io/authorization?org=dev&...
```

Or using HTTP header:

```
X-Organization: dev
```

## Deployment

### Azure Container Apps

This application is designed to run on Azure Container Apps with Azure SQL Database (free tier).

**Cost Estimate**: ~¥75/month
- Azure SQL Database Free tier: ¥0 (100,000 vCore seconds/month)
- Azure Container Apps: ~¥75 (minimal usage)

See [ecauth-infrastructure](https://github.com/EcAuth/ecauth-infrastructure) repository for Infrastructure as Code (Terraform) deployment.

## Dependencies

- **EcAuth.IdpUtilities**: Common utilities library (NuGet package)
  - Email hashing
  - Iron encryption (State parameter)
  - Secure random string generation

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Related Repositories

- [EcAuth](https://github.com/EcAuth/EcAuth): Main Identity Provider application
- [EcAuth.IdpUtilities](https://github.com/EcAuth/EcAuth.IdpUtilities): Common utilities library
- [ecauth-infrastructure](https://github.com/EcAuth/ecauth-infrastructure): Infrastructure as Code (Terraform + Ansible)
