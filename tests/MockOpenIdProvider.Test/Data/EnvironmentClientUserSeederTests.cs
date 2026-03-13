using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MockOpenIdProvider.Data.Seeders;
using MockOpenIdProvider.Models;
using MockOpenIdProvider.Services;
using Moq;

namespace MockOpenIdProvider.Test.Data
{
    public class EnvironmentClientUserSeederTests : IDisposable
    {
        private readonly IdpDbContext _context;
        private readonly Mock<ILogger> _mockLogger;

        public EnvironmentClientUserSeederTests()
        {
            var options = new DbContextOptionsBuilder<IdpDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var mockOrganizationService = new Mock<IOrganizationService>();
            mockOrganizationService.Setup(os => os.TenantName).Returns("staging");

            _context = new IdpDbContext(options, mockOrganizationService.Object);
            _mockLogger = new Mock<ILogger>();

            // テスト用 Organization を作成
            _context.Organizations.Add(new Organization
            {
                Name = "Staging",
                TenantName = "staging"
            });
            _context.Organizations.Add(new Organization
            {
                Name = "Production",
                TenantName = "production"
            });
            _context.SaveChanges();
        }

        [Fact]
        public async Task SeedAsync_ShouldCreateClientAndUser_WhenNotExists()
        {
            // Arrange
            var seeder = new EnvironmentClientUserSeeder("MOCKIDP_STAGING", "staging", 10);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MOCKIDP_STAGING_CLIENT_ID"] = "test-client-id",
                    ["MOCKIDP_STAGING_CLIENT_SECRET"] = "test-secret",
                    ["MOCKIDP_STAGING_CLIENT_NAME"] = "TestClient",
                    ["MOCKIDP_STAGING_REDIRECT_URI"] = "https://localhost/callback",
                    ["MOCKIDP_STAGING_USER_EMAIL"] = "test@example.com",
                    ["MOCKIDP_STAGING_USER_PASSWORD"] = "password123",
                })
                .Build();

            // Act
            await seeder.SeedAsync(_context, config, _mockLogger.Object);

            // Assert
            var client = await _context.Clients.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.ClientId == "test-client-id");
            Assert.NotNull(client);
            Assert.Equal("TestClient", client.ClientName);
            Assert.Equal("https://localhost/callback", client.RedirectUri);

            var user = await _context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == "test@example.com");
            Assert.NotNull(user);
            Assert.NotEqual("password123", user.Password); // ハッシュ化されていること
            Assert.Equal(client.Id, user.ClientId);
        }

        [Fact]
        public async Task SeedAsync_ShouldBeIdempotent_WhenCalledTwice()
        {
            // Arrange
            var seeder = new EnvironmentClientUserSeeder("MOCKIDP_STAGING", "staging", 10);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MOCKIDP_STAGING_CLIENT_ID"] = "idempotent-client",
                    ["MOCKIDP_STAGING_CLIENT_SECRET"] = "secret",
                    ["MOCKIDP_STAGING_CLIENT_NAME"] = "IdempotentClient",
                    ["MOCKIDP_STAGING_REDIRECT_URI"] = "https://localhost/callback",
                    ["MOCKIDP_STAGING_USER_EMAIL"] = "idempotent@example.com",
                    ["MOCKIDP_STAGING_USER_PASSWORD"] = "password",
                })
                .Build();

            // Act
            await seeder.SeedAsync(_context, config, _mockLogger.Object);
            await seeder.SeedAsync(_context, config, _mockLogger.Object);

            // Assert
            var clients = await _context.Clients.IgnoreQueryFilters()
                .Where(c => c.ClientId == "idempotent-client")
                .ToListAsync();
            Assert.Single(clients);

            var users = await _context.Users.IgnoreQueryFilters()
                .Where(u => u.Email == "idempotent@example.com")
                .ToListAsync();
            Assert.Single(users);
        }

        [Fact]
        public async Task SeedAsync_ShouldSkip_WhenClientIdNotConfigured()
        {
            // Arrange
            var seeder = new EnvironmentClientUserSeeder("MOCKIDP_STAGING", "staging", 10);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            // Act
            await seeder.SeedAsync(_context, config, _mockLogger.Object);

            // Assert
            var clients = await _context.Clients.IgnoreQueryFilters().ToListAsync();
            Assert.Empty(clients);
        }

        [Fact]
        public async Task SeedAsync_ShouldSkip_WhenOrganizationNotFound()
        {
            // Arrange
            var seeder = new EnvironmentClientUserSeeder("MOCKIDP_NONEXISTENT", "nonexistent", 10);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MOCKIDP_NONEXISTENT_CLIENT_ID"] = "client-id",
                    ["MOCKIDP_NONEXISTENT_CLIENT_SECRET"] = "secret",
                    ["MOCKIDP_NONEXISTENT_REDIRECT_URI"] = "https://localhost/callback",
                    ["MOCKIDP_NONEXISTENT_USER_EMAIL"] = "user@example.com",
                    ["MOCKIDP_NONEXISTENT_USER_PASSWORD"] = "password",
                })
                .Build();

            // Act
            await seeder.SeedAsync(_context, config, _mockLogger.Object);

            // Assert
            var clients = await _context.Clients.IgnoreQueryFilters().ToListAsync();
            Assert.Empty(clients);
        }

        [Fact]
        public async Task SeedAsync_ProductionSeeder_ShouldCreateInProductionOrg()
        {
            // Arrange
            var seeder = new EnvironmentClientUserSeeder("MOCKIDP_PRODUCTION", "production", 20);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MOCKIDP_PRODUCTION_CLIENT_ID"] = "prod-client-id",
                    ["MOCKIDP_PRODUCTION_CLIENT_SECRET"] = "prod-secret",
                    ["MOCKIDP_PRODUCTION_CLIENT_NAME"] = "ProductionClient",
                    ["MOCKIDP_PRODUCTION_REDIRECT_URI"] = "https://production.example.com/callback",
                    ["MOCKIDP_PRODUCTION_USER_EMAIL"] = "prod@example.com",
                    ["MOCKIDP_PRODUCTION_USER_PASSWORD"] = "prod-password",
                })
                .Build();

            // Act
            await seeder.SeedAsync(_context, config, _mockLogger.Object);

            // Assert
            var prodOrg = await _context.Organizations.IgnoreQueryFilters()
                .FirstAsync(o => o.TenantName == "production");

            var client = await _context.Clients.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.ClientId == "prod-client-id");
            Assert.NotNull(client);
            Assert.Equal(prodOrg.Id, client.OrganizationId);

            var user = await _context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == "prod@example.com");
            Assert.NotNull(user);
            Assert.Equal(prodOrg.Id, user.OrganizationId);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
