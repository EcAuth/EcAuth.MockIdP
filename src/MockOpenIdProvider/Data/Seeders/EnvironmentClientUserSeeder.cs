using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MockOpenIdProvider.Models;

namespace MockOpenIdProvider.Data.Seeders;

/// <summary>
/// 環境固有の Client と MockIdpUser を投入するシーダー。
/// Staging/Production 環境用のシードデータを冪等に作成します。
/// </summary>
public class EnvironmentClientUserSeeder : IDbSeeder
{
    private readonly string _envPrefix;
    private readonly string _tenantName;

    /// <inheritdoc />
    public string RequiredMigration => "20251115233637_AddOrganizationSupport";

    /// <inheritdoc />
    public int Order { get; }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="envPrefix">環境変数プレフィックス（例: "MOCKIDP_STAGING", "MOCKIDP_PRODUCTION"）</param>
    /// <param name="tenantName">Organization の tenant_name（例: "staging", "production"）</param>
    /// <param name="order">実行順序</param>
    public EnvironmentClientUserSeeder(string envPrefix, string tenantName, int order)
    {
        _envPrefix = envPrefix;
        _tenantName = tenantName;
        Order = order;
    }

    /// <inheritdoc />
    public async Task SeedAsync(IdpDbContext context, IConfiguration configuration, ILogger logger)
    {
        var clientId = configuration[$"{_envPrefix}_CLIENT_ID"];
        var clientSecret = configuration[$"{_envPrefix}_CLIENT_SECRET"];
        var clientName = configuration[$"{_envPrefix}_CLIENT_NAME"];
        var redirectUri = configuration[$"{_envPrefix}_REDIRECT_URI"];
        var userEmail = configuration[$"{_envPrefix}_USER_EMAIL"];
        var userPassword = configuration[$"{_envPrefix}_USER_PASSWORD"];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogInformation("Skipped {Tenant} - {Prefix}_CLIENT_ID not configured", _tenantName, _envPrefix);
            return;
        }

        if (string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri)
            || string.IsNullOrWhiteSpace(userEmail) || string.IsNullOrWhiteSpace(userPassword))
        {
            logger.LogInformation("Skipped {Tenant} - required environment variables not fully configured", _tenantName);
            return;
        }

        // Organization を取得
        var organization = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.TenantName == _tenantName);

        if (organization == null)
        {
            logger.LogWarning("Skipped {Tenant} - Organization with tenant_name '{TenantName}' not found", _tenantName, _tenantName);
            return;
        }

        // Client を冪等に作成
        var client = await SeedClientAsync(context, organization, clientId, clientSecret, clientName, redirectUri, logger);

        if (client == null) return;

        // MockIdpUser を冪等に作成
        await SeedUserAsync(context, organization, client, userEmail, userPassword, logger);
    }

    private static async Task<Client?> SeedClientAsync(
        IdpDbContext context,
        Organization organization,
        string clientId,
        string clientSecret,
        string? clientName,
        string redirectUri,
        ILogger logger)
    {
        var existing = await context.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.OrganizationId == organization.Id);

        if (existing != null)
        {
            var updated = false;

            if (existing.ClientSecret != clientSecret)
            {
                existing.ClientSecret = clientSecret;
                updated = true;
            }

            var desiredName = clientName ?? $"{organization.TenantName}Client";
            if (existing.ClientName != desiredName)
            {
                existing.ClientName = desiredName;
                updated = true;
            }

            if (existing.RedirectUri != redirectUri)
            {
                existing.RedirectUri = redirectUri;
                updated = true;
            }

            if (updated)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Updated Client {ClientId} for {TenantName}",
                    clientId, organization.TenantName);
            }
            else
            {
                logger.LogInformation("Client {ClientId} already up-to-date for {TenantName}",
                    clientId, organization.TenantName);
            }

            return existing;
        }

        using RSA rsa = RSA.Create();
        var privateKey = rsa.ExportRSAPrivateKeyPem();
        var publicKey = rsa.ExportRSAPublicKeyPem();

        var client = new Client
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientName = clientName ?? $"{organization.TenantName}Client",
            RedirectUri = redirectUri,
            PublicKey = publicKey,
            PrivateKey = privateKey,
            OrganizationId = organization.Id
        };

        context.Clients.Add(client);
        await context.SaveChangesAsync();

        logger.LogInformation("Created Client {ClientId} for {TenantName}",
            clientId, organization.TenantName);
        return client;
    }

    private static async Task SeedUserAsync(
        IdpDbContext context,
        Organization organization,
        Client client,
        string userEmail,
        string userPassword,
        ILogger logger)
    {
        var existing = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == userEmail && u.OrganizationId == organization.Id);

        var passwordHasher = new PasswordHasher<MockIdpUser>();

        if (existing != null)
        {
            var updated = false;

            var verifyResult = passwordHasher.VerifyHashedPassword(existing, existing.Password!, userPassword);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                existing.Password = passwordHasher.HashPassword(existing, userPassword);
                updated = true;
            }

            if (existing.ClientId != client.Id)
            {
                existing.ClientId = client.Id;
                updated = true;
            }

            if (updated)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Updated User for {TenantName}",
                    organization.TenantName);
            }
            else
            {
                logger.LogInformation("User already up-to-date for {TenantName}",
                    organization.TenantName);
            }

            return;
        }

        var hashedPassword = passwordHasher.HashPassword(new MockIdpUser { Email = userEmail }, userPassword);

        var user = new MockIdpUser
        {
            Email = userEmail,
            Password = hashedPassword,
            ClientId = client.Id,
            OrganizationId = organization.Id
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        logger.LogInformation("Created User for {TenantName}",
            organization.TenantName);
    }
}
