# Secrets Management Guide

This document describes all secrets and configuration values required to run the SteamNexus application.

## Required Secrets

### Database Connection Strings

| Secret Name | Description | Where to Configure |
|-------------|-------------|-------------------|
| `ConnectionStrings:AuthenticationConnection` | SQL Server connection string for identity/auth database | Azure App Configuration / User Secrets |
| `ConnectionStrings:SteamInfoConnection` | SQL Server connection string for main application database | Azure App Configuration / User Secrets |

### API Keys

| Secret Name | Description | Where to Obtain |
|-------------|-------------|-----------------|
| `SteamKey` | Steam Web API key for accessing Steam user data | [Steam Developer Portal](https://steamcommunity.com/dev/apikey) |
| `OpenAiKey` | OpenAI API key for AI-powered features | [OpenAI Platform](https://platform.openai.com/api-keys) |
| `AdminSeedingApiKey` | Internal API key for admin seeding operations | Generate a secure random string |
| `ClientId` | IGDB Client ID for game metadata | [IGDB API](https://api-docs.igdb.com/) |
| `AccessToken` | IGDB Access Token for game metadata | [IGDB API](https://api-docs.igdb.com/) |
| `SendGridKey` | SendGrid API key for sending emails | [SendGrid](https://sendgrid.com/) |

### Seeding Passwords

| Secret Name | Description |
|-------------|-------------|
| `SeedUserPW` | Password for seeded test users |
| `SeedAdminPW` | Password for seeded admin user |

### Application Configuration

| Config Key | Description | Default |
|------------|-------------|---------|
| `Email:FromAddress` | Email address for outgoing emails | `noreply@steamnexus.com` |
| `Email:FromName` | Display name for outgoing emails | `SteamNexus` |
| `SendGrid:ConfirmTemplateId` | SendGrid template ID for confirmation emails | - |
| `SendGrid:InviteTemplateId` | SendGrid template ID for invitation emails | - |
| `SendGrid:ResetTemplateId` | SendGrid template ID for password reset emails | - |
| `App:BaseUrl` | Base URL of the application | `https://localhost:7298` |
| `Steam:CuratorSteamId` | Steam ID for the curator account (game library source) | - |
| `ApplicationInsights:ConnectionString` | Azure Application Insights connection string | - |

## Local Development Setup

### Using User Secrets

1. Navigate to the SteamProject directory:
   ```bash
   cd src/SteamProject
   ```

2. Initialize user secrets (if not already done):
   ```bash
   dotnet user-secrets init
   ```

3. Set required secrets:
   ```bash
   # Database
   dotnet user-secrets set "ConnectionStrings:AuthenticationConnection" "Server=(localdb)\\mssqllocaldb;Database=SteamInfoAuth;Trusted_Connection=True;MultipleActiveResultSets=true"
   dotnet user-secrets set "ConnectionStrings:SteamInfoConnection" "Server=(localdb)\\mssqllocaldb;Database=SteamInfo;Trusted_Connection=True;MultipleActiveResultSets=true"

   # API Keys
   dotnet user-secrets set "SteamKey" "YOUR_STEAM_API_KEY"
   dotnet user-secrets set "OpenAiKey" "YOUR_OPENAI_API_KEY"
   dotnet user-secrets set "AdminSeedingApiKey" "YOUR_ADMIN_SEEDING_KEY"
   dotnet user-secrets set "ClientId" "YOUR_IGDB_CLIENT_ID"
   dotnet user-secrets set "AccessToken" "YOUR_IGDB_ACCESS_TOKEN"
   dotnet user-secrets set "SendGridKey" "YOUR_SENDGRID_API_KEY"

   # Seeding
   dotnet user-secrets set "SeedUserPW" "TestPassword123!"
   dotnet user-secrets set "SeedAdminPW" "AdminPassword123!"

   # App Config
   dotnet user-secrets set "Steam:CuratorSteamId" "YOUR_CURATOR_STEAM_ID"
   ```

## Azure Deployment Setup

### Azure App Configuration

For production deployment, configure these settings in Azure App Configuration or Azure Key Vault:

1. **Azure App Service Configuration**:
   - Go to your App Service in Azure Portal
   - Navigate to Settings > Configuration > Application settings
   - Add each secret as an application setting

2. **Azure Key Vault (Recommended for secrets)**:
   - Create a Key Vault in Azure
   - Add secrets with the same names as above
   - Configure Key Vault references in App Service

### Connection String Configuration

In Azure App Service:
1. Go to Settings > Configuration > Connection strings
2. Add `AuthenticationConnection` and `SteamInfoConnection`
3. Set type to "SQLAzure"

## Security Best Practices

1. **Never commit secrets to source control** - Use user secrets for local development
2. **Use Azure Key Vault for production** - Centralized secret management with audit logging
3. **Rotate API keys regularly** - Especially after team member changes
4. **Use managed identities where possible** - Reduces need for connection strings
5. **Limit API key permissions** - Use least privilege principle

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Connection string not found" | Ensure secrets are set in user secrets or Azure configuration |
| "Steam API returns 401" | Verify SteamKey is valid and not expired |
| "Emails not sending" | Check SendGridKey and template IDs are configured |
| "IGDB returns 401" | Verify ClientId and AccessToken are valid |

### Verifying Secrets

Check if secrets are loaded correctly:
```bash
dotnet user-secrets list
```

## Health Check Endpoint

The application exposes a `/health` endpoint that verifies:
- SteamInfoDb connectivity
- AuthDb connectivity

Use this endpoint for Azure App Service health probes and monitoring.
