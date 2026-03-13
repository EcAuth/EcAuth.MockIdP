using MockOpenIdProvider.Models;
using Microsoft.EntityFrameworkCore;

namespace MockOpenIdProvider.Data;

/// <summary>
/// データベース初期化処理を行うクラス。
/// 登録されたシーダーを順番に実行し、必要なシードデータを投入します。
/// </summary>
public class DbInitializer
{
    private readonly IEnumerable<IDbSeeder> _seeders;
    private readonly ILogger<DbInitializer> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public DbInitializer(
        IEnumerable<IDbSeeder> seeders,
        ILogger<DbInitializer> logger,
        ILoggerFactory loggerFactory)
    {
        _seeders = seeders;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// データベースに接続可能かどうかを確認します。
    /// </summary>
    protected virtual async Task<bool> CanConnectAsync(IdpDbContext context)
        => await context.Database.CanConnectAsync();

    /// <summary>
    /// 適用済みマイグレーションの一覧を取得します。
    /// </summary>
    protected virtual async Task<HashSet<string>> GetAppliedMigrationsAsync(IdpDbContext context)
        => (await context.Database.GetAppliedMigrationsAsync()).ToHashSet();

    /// <summary>
    /// データベースの初期化処理を実行します。
    /// </summary>
    public async Task InitializeAsync(
        IdpDbContext context,
        IConfiguration configuration)
    {
        if (!await CanConnectAsync(context))
        {
            throw new InvalidOperationException("DbInitializer: Database is not available. Cannot run seed.");
        }

        var appliedMigrations = await GetAppliedMigrationsAsync(context);

        _logger.LogInformation("DbInitializer: Found {Count} applied migrations", appliedMigrations.Count);

        var orderedSeeders = _seeders.OrderBy(s => s.Order);

        foreach (var seeder in orderedSeeders)
        {
            var seederType = seeder.GetType();
            var seederName = seederType.Name;

            if (appliedMigrations.Contains(seeder.RequiredMigration))
            {
                _logger.LogInformation("DbInitializer: Running {Seeder}", seederName);

                var seederLogger = _loggerFactory.CreateLogger(seederType);

                try
                {
                    await seeder.SeedAsync(context, configuration, seederLogger);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DbInitializer: Error running {Seeder}", seederName);
                    throw;
                }
            }
            else
            {
                _logger.LogInformation(
                    "DbInitializer: Skipping {Seeder} - migration {Migration} not applied yet",
                    seederName,
                    seeder.RequiredMigration);
            }
        }

        _logger.LogInformation("DbInitializer: Initialization completed");
    }
}
