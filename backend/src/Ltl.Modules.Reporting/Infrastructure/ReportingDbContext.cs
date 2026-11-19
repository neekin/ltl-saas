using Microsoft.EntityFrameworkCore;

namespace Ltl.Modules.Reporting.Infrastructure;

/// <summary>
/// 报表模块只读 DbContext（仅用于执行带显式 tenant_id 过滤的跨模块只读原生 SQL 聚合）。
/// 避免直接依赖其他模块的 Infrastructure / DbContext（AGENTS §4.2）。
/// </summary>
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
}
