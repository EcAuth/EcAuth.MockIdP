using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MockOpenIdProvider.Data;
using MockOpenIdProvider.Data.Seeders;
using MockOpenIdProvider.Models;
using MockOpenIdProvider.Services;
using MockOpenIdProvider.Middlewares;
using System.Configuration;

var builder = WebApplication.CreateBuilder(args);

// OrganizationService をScoped サービスとして登録
builder.Services.AddScoped<IOrganizationService, OrganizationService>();

// Add services to the container.
builder.Services.AddDbContext<IdpDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration["ConnectionStrings:MockIdpDbContext"],
        sqlOptions => sqlOptions.CommandTimeout(180) // タイムアウトを3分に設定
    );

    // EF Core 9のマイグレーション時の自動トランザクション管理警告を無視
    // 参照: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes
    // これにより、マイグレーション内でDbContextを作成する既存のパターンが動作します
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MigrationsUserTransactionWarning));
});

// データベース初期化（シーダー）
builder.Services.AddScoped<IDbSeeder>(sp =>
    new EnvironmentClientUserSeeder("MOCKIDP_STAGING", "staging", 10));
builder.Services.AddScoped<IDbSeeder>(sp =>
    new EnvironmentClientUserSeeder("MOCKIDP_PRODUCTION", "production", 20));
builder.Services.AddScoped<DbInitializer>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOptions<MockIdP>().BindConfiguration(nameof(MockIdP)).ValidateDataAnnotations();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdpDbContext>(tags: new[] { "ready" });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

// OrganizationMiddleware を認証・認可の前に配置
app.UseMiddleware<OrganizationMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Health Check Endpoints
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/healthz/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false  // データベースチェック不要（プロセス生存のみ）
});

// データベース初期化（シーダー実行）
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    var context = scope.ServiceProvider.GetRequiredService<IdpDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await dbInitializer.InitializeAsync(context, configuration);
    }
    catch (Exception ex)
    {
        if (app.Environment.IsProduction())
        {
            logger.LogError(ex, "DbInitializer failed. Continuing startup in Production environment.");
        }
        else
        {
            logger.LogError(ex, "DbInitializer failed. Stopping application.");
            throw;
        }
    }
}

app.Run();
