using Ltl.Modules.Tenancy.Domain;
using Ltl.Modules.Tenancy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ltl.IntegrationTests.Tenancy;

/// <summary>
/// TEN-001 数据层集成测试：真实 PostgreSQL 容器（Testcontainers，AGENTS.md §10）。
/// 覆盖：迁移可在空库执行、唯一约束、检查约束、xmin 乐观并发令牌。
/// </summary>
public sealed class TenancyPersistenceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public TenancyPersistenceTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private TenancyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TenancyDbContext(options);
    }

    [Fact]
    public async Task Migration_ShouldApplyOnEmptyDatabase()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        // tenants 表存在且可查询
        var count = await context.Tenants.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DuplicateTenantCode_ShouldBeRejectedByUniqueIndex()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.Tenants.Add(Tenant.Create("dup-code", "企业甲", DateTime.UtcNow));
        await context.SaveChangesAsync();

        context.Tenants.Add(Tenant.Create("dup-code", "企业乙", DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task InvalidStatus_ShouldBeRejectedByCheckConstraint()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        // 绕过领域模型直接写库，验证数据库层防线（AGENTS.md §7：约束必须在数据库层）
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => context.Database.ExecuteSqlRawAsync(
                "INSERT INTO tenants (id, code, name, status, created_at) VALUES ({0}, {1}, {2}, {3}, {4})",
                Guid.NewGuid(), "status-check", "检查约束企业", 5, DateTime.UtcNow));

        Assert.Contains("ck_tenants_status", exception.Message);
    }

    [Fact]
    public async Task ConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var tenant = Tenant.Create("concurrency", "并发企业", DateTime.UtcNow);
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        await using var contextB = CreateContext();
        await using var contextC = CreateContext();
        var loadedB = await contextB.Tenants.SingleAsync(x => x.Id == tenant.Id);
        var loadedC = await contextC.Tenants.SingleAsync(x => x.Id == tenant.Id);

        loadedB.Disable(DateTime.UtcNow);
        await contextB.SaveChangesAsync();

        loadedC.Disable(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextC.SaveChangesAsync());
    }
}
