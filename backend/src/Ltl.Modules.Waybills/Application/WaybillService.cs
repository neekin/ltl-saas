using Ltl.Modules.Pricing.Application;
using Ltl.Modules.Pricing.Domain;
using Ltl.Modules.Platform.Application;
using Ltl.Modules.Platform.Domain;
using Ltl.Modules.Waybills.Domain;
using Ltl.Modules.Waybills.Infrastructure;
using Ltl.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ltl.Modules.Waybills.Application;

/// <summary>
/// 运单命令服务实现（WB-002）。跨模块资料校验走原生 SQL + 显式租户参数（AGENTS §5.1，
/// CUS-001 先例）；计价经 Pricing 公开 Application 接口；运单号经 Platform 序号服务（SEQ-001）。
/// </summary>
public sealed class WaybillService(
    WaybillsDbContext db,
    IBranchSequenceService branchSequenceService,
    IPricingTrialService pricingTrialService,
    IPricingSnapshotSerializer snapshotSerializer) : IWaybillService
{
    public async Task<CreateWaybillResult> CreateWaybillAsync(CreateWaybillCommand command)
    {
        var tenantId = db.CurrentTenantId
            ?? throw new TenantContextMissingException();
        var now = DateTime.UtcNow;

        // 幂等：同键重复提交返回已存在运单（不重复开单/收费，AGENTS §8）
        if (command.IdempotencyKey is { } idempotencyKey)
        {
            var existing = await db.Waybills.SingleOrDefaultAsync(w => w.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                return await MapResultAsync(existing);
            }
        }

        // 跨模块资料校验（原生 SQL 显式租户参数）：网点存在且启用，并取得网点码（不信任客户端提交）
        var origin = await RequireActiveBranchAsync(tenantId, command.OriginBranchId);
        await RequireActiveBranchAsync(tenantId, command.DestBranchId);
        await RequireActiveCustomerAsync(tenantId, command.CustomerId);

        if (command.RouteId is { } routeId)
        {
            await RequireActiveRouteAsync(tenantId, routeId);
        }

        // 计价试算（失败明确报错）：输入由货物件汇总 + 命令上下文构成；先试算后取号，失败不消耗单号
        var input = new PricingQuoteInput
        {
            // 计费口径与前端汇总/看板报表一致：重量/体积按"每件 × 件数"合计（WB-002 修正：此前漏乘件数）
            WeightKg = command.Packages.Sum(p => p.WeightKg * p.Quantity),
            VolumeM3 = command.Packages.Sum(p => p.VolumeM3 * p.Quantity),
            PieceCount = command.Packages.Sum(p => p.Quantity),
            DeclaredValue = command.DeclaredValue,
            CustomerId = command.CustomerId,
            RouteId = command.RouteId,
            OriginBranchId = command.OriginBranchId,
            DestBranchId = command.DestBranchId,
        };
        var trial = await pricingTrialService.TrialCalculateAsync(command.RuleSetId, input);

        // 套餐配额前置校验（TEN-002）：原生 SQL 读租户配额（同一物理库，AGENTS §5.1 模式）
        await EnsureWaybillQuotaAsync(tenantId);

        // 信用额度检查（月结客户；ROADMAP P7 逾期和信用额度检查）
        if (command.PaymentType == Ltl.Modules.Waybills.Domain.WaybillPaymentType.Monthly)
        {
            await CheckCreditLimitAsync(tenantId, command.CustomerId);
        }

        // 取运单号（SEQ-001：YB + 始发网点码 + 业务日期间 + 6 位序号）
        var waybillNo = await branchSequenceService.NextNumberAsync(
            command.OriginBranchId, BusinessDocumentType.Waybill, command.BusinessDate, origin.Code);

        var waybill = Waybill.Create(
            tenantId, waybillNo, command.OriginBranchId, command.DestBranchId, command.CustomerId,
            command.PaymentType, command.SenderName, command.SenderPhone, command.SenderAddress,
            command.ReceiverName, command.ReceiverPhone, command.ReceiverAddress,
            command.Packages, now,
            codAmount: command.CodAmount, routeId: command.RouteId, remark: command.Remark,
            declaredValue: command.DeclaredValue,
            // 保价费由计价引擎按规则（PerDeclaredValue 附加费）计算，不再手填（AGENTS §6.4 计费可配置）
            insuranceFee: trial.Quote.InsuranceFee,
            createdBy: command.OperatorId, idempotencyKey: command.IdempotencyKey);
        waybill.ApplyPricing(trial.Quote.Total, now, command.OperatorId);

        // P10：生成客户查询码（明文仅返回，库内存哈希），客户凭运单号+查询码匿名查本票。
        var queryCode = Waybill.GenerateQueryCode();
        waybill.SetQueryCode(queryCode, now, command.OperatorId);

        db.Waybills.Add(waybill);
        db.PricingSnapshots.Add(WaybillPricingSnapshot.Create(
            tenantId, waybill.Id, trial.RuleSetId, trial.RuleSetCode, trial.VersionId, trial.VersionNumber,
            snapshotSerializer.SerializeInput(trial.Input), snapshotSerializer.SerializeQuote(trial.Quote),
            now, command.OperatorId));
        db.Events.Add(WaybillEvent.Create(
            tenantId, waybill.Id, WaybillEventType.Created, null, WaybillStatus.Draft, now,
            command.BranchId, command.OperatorId,
            $"开单计价 {trial.Quote.Total} 元（规则集 {trial.RuleSetCode} v{trial.VersionNumber}）",
            command.OperatorId));

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsIdempotencyConflict(ex))
        {
            // 并发同键竞态：唯一索引兜底 → 回查返回已存在运单（结果一致）
            var winner = await db.Waybills
                .SingleAsync(w => w.IdempotencyKey == command.IdempotencyKey);
            return await MapResultAsync(winner);
        }

        return new CreateWaybillResult(
            waybill.Id, waybill.WaybillNo, waybill.Status, trial.Quote.Total, waybill.InsuranceFee,
            waybill.CodAmount, trial.RuleSetCode, trial.VersionNumber, queryCode);
    }

    public async Task<Waybill> ConfirmWaybillAsync(Guid waybillId, Guid? branchId = null, Guid? operatorId = null)
    {
        var tenantId = RequireTenantId();
        var now = DateTime.UtcNow;

        var waybill = await RequireWaybillAsync(tenantId, waybillId);
        var from = waybill.Status;
        waybill.Confirm(now, operatorId);

        db.Events.Add(WaybillEvent.Create(
            tenantId, waybill.Id, WaybillEventType.Confirmed, from, waybill.Status, now,
            branchId, operatorId, detail: null, createdBy: operatorId));
        await db.SaveChangesAsync();
        return waybill;
    }

    public async Task<Waybill> CancelWaybillAsync(
        Guid waybillId, string? reason = null, Guid? branchId = null, Guid? operatorId = null)
    {
        var tenantId = RequireTenantId();
        var now = DateTime.UtcNow;

        var waybill = await RequireWaybillAsync(tenantId, waybillId);
        var from = waybill.Status;
        waybill.Cancel(now, operatorId);

        db.Events.Add(WaybillEvent.Create(
            tenantId, waybill.Id, WaybillEventType.Cancelled, from, waybill.Status, now,
            branchId, operatorId,
            string.IsNullOrWhiteSpace(reason) ? null : $"取消原因：{reason.Trim()}",
            createdBy: operatorId));
        await db.SaveChangesAsync();
        return waybill;
    }

    public async Task<IReadOnlyList<WaybillEvent>> GetEventsAsync(Guid waybillId)
    {
        var tenantId = RequireTenantId();
        await RequireWaybillAsync(tenantId, waybillId);

        return await db.Events
            .Where(e => e.WaybillId == waybillId)
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.EventType)
            .ToListAsync();
    }

    // ---------- 信用额度检查（月结客户，AGENTS §6.5 / ROADMAP P7） ----------

    /// <summary>
    /// 月结客户信用额度检查：汇总该客户全部未收应收，与新运单金额累加后与额度比对。
    /// 超出 → ConflictException（409）。
    /// </summary>
    private async Task CheckCreditLimitAsync(Guid tenantId, Guid customerId, decimal newWaybillAmount = 0)
    {
        // 读取客户信用额度（原生 SQL，Finance 无 Customers DbSet）
        var creditLimit = (await db.Database
                .SqlQuery<CreditLimitRow>(
                    $"SELECT credit_limit AS \"CreditLimit\" FROM customers WHERE tenant_id = {tenantId} AND id = {customerId}")
                .ToListAsync())
            .SingleOrDefault()?.CreditLimit;

        // 未设置信用额度 → 不检查
        if (creditLimit is not { } limit)
            return;

        // 汇总该客户全部未收应收（原生 SQL 跨模块聚合；按 customer_id 精确过滤，不跨客户汇总）
        var outstanding = (await db.Database
                .SqlQuery<OutstandingRow>(
                    $"""
                    SELECT COALESCE(SUM(r.amount - r.settled_amount), 0) AS "Outstanding"
                    FROM receivables r
                    INNER JOIN waybills w ON w.id = r.waybill_id AND w.tenant_id = r.tenant_id
                    WHERE r.tenant_id = {tenantId} AND w.customer_id = {customerId} AND r.status != 3
                    """)
                .ToListAsync())
            .SingleOrDefault()?.Outstanding ?? 0;

        if (outstanding + newWaybillAmount > limit)
        {
            throw new ConflictException(
                $"月结客户信用额度不足：当前未收 {outstanding} 元 + 本单 {newWaybillAmount} 元 > 信用额度 {limit} 元。");
        }
    }

    // ---------- 跨模块资料校验（原生 SQL + 显式租户参数，AGENTS §5.1） ----------

    private sealed record BranchInfoRow(string Code, short Status);

    private sealed record StatusRow(short Status);

    private sealed record TenantQuotaRow(int? MaxWaybills);

    /// <summary>运单配额前置校验（TEN-002）：max_waybills 非空且已达上限 → 409。</summary>
    private async Task EnsureWaybillQuotaAsync(Guid tenantId)
    {
        var rows = await db.Database
            .SqlQuery<TenantQuotaRow>(
                $"SELECT max_waybills AS \"MaxWaybills\" FROM tenants WHERE id = {tenantId}")
            .ToListAsync();
        var max = rows.SingleOrDefault()?.MaxWaybills;
        if (max is { } limit)
        {
            var current = await db.Waybills.IgnoreQueryFilters().CountAsync(w => w.TenantId == tenantId);
            if (current >= limit)
            {
                throw new ConflictException($"套餐运单配额已用满（{current}/{limit}），请升级套餐后重试。");
            }
        }
    }

    private sealed record CreditLimitRow(decimal? CreditLimit);

    private sealed record OutstandingRow(decimal? Outstanding);

    /// <summary>网点存在且启用；返回编码（运单号组成）。停用 → 领域异常；不存在/跨租户 → 404 语义。</summary>
    private async Task<BranchInfoRow> RequireActiveBranchAsync(Guid tenantId, Guid branchId)
    {
        var branch = (await db.Database
                .SqlQuery<BranchInfoRow>(
                    $"""SELECT code AS "Code", status AS "Status" FROM branches WHERE tenant_id = {tenantId} AND id = {branchId}""")
                .ToListAsync())
            .SingleOrDefault()
            ?? throw new EntityNotFoundException("网点", branchId);

        if (branch.Status != 0) // BranchStatus.Active
        {
            throw new WaybillDomainException($"网点（{branchId}）已停用，不能用于开单。");
        }

        return branch;
    }

    private async Task RequireActiveCustomerAsync(Guid tenantId, Guid customerId)
    {
        var customer = (await db.Database
                .SqlQuery<StatusRow>(
                    $"""SELECT status AS "Status" FROM customers WHERE tenant_id = {tenantId} AND id = {customerId}""")
                .ToListAsync())
            .SingleOrDefault()
            ?? throw new EntityNotFoundException("客户", customerId);

        if (customer.Status != 0) // CustomerStatus.Active
        {
            throw new WaybillDomainException($"客户（{customerId}）已停用，不能开单。");
        }
    }

    private async Task RequireActiveRouteAsync(Guid tenantId, Guid routeId)
    {
        var route = (await db.Database
                .SqlQuery<StatusRow>(
                    $"""SELECT status AS "Status" FROM routes WHERE tenant_id = {tenantId} AND id = {routeId}""")
                .ToListAsync())
            .SingleOrDefault()
            ?? throw new EntityNotFoundException("线路", routeId);

        if (route.Status != 0) // RouteStatus.Active
        {
            throw new WaybillDomainException($"线路（{routeId}）已停用，不能开单。");
        }
    }

    // ---------- 辅助 ----------

    private Guid RequireTenantId() =>
        db.CurrentTenantId ?? throw new TenantContextMissingException();

    private async Task<Waybill> RequireWaybillAsync(Guid tenantId, Guid waybillId) =>
        await db.Waybills.FindAsync([waybillId])
        ?? throw new EntityNotFoundException("运单", waybillId);

    /// <summary>幂等重放：回查最新计价快照填充规则集/版本引用（与首次调用结果一致）。</summary>
    private async Task<CreateWaybillResult> MapResultAsync(Waybill waybill)
    {
        var snapshot = await db.PricingSnapshots
            .Where(s => s.WaybillId == waybill.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        return new CreateWaybillResult(
            waybill.Id, waybill.WaybillNo, waybill.Status, waybill.TotalAmount ?? 0,
            waybill.InsuranceFee, waybill.CodAmount,
            snapshot?.RuleSetCode ?? string.Empty, snapshot?.VersionNumber ?? 0);
    }

    private static bool IsIdempotencyConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == "ux_waybills_tenant_idempotency";
}
