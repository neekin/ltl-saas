using Ltl.Modules.Waybills.Domain;

namespace Ltl.Modules.Waybills.Application;

/// <summary>
/// 开单命令（WB-002 设计 §2）。货物件汇总作为计价输入；操作人/操作网点为审计值
/// （认证交付后由服务端上下文解析）；业务日期决定运单号期间（界面按 Asia/Shanghai 取业务日）。
/// </summary>
public sealed record CreateWaybillCommand(
    Guid OriginBranchId,
    Guid DestBranchId,
    Guid CustomerId,
    WaybillPaymentType PaymentType,
    string SenderName,
    string SenderPhone,
    string SenderAddress,
    string ReceiverName,
    string ReceiverPhone,
    string ReceiverAddress,
    IReadOnlyList<WaybillPackageDraft> Packages,
    Guid RuleSetId,
    DateOnly BusinessDate,
    Guid? RouteId = null,
    decimal? CodAmount = null,
    string? Remark = null,
    Guid? IdempotencyKey = null,
    Guid? BranchId = null,
    Guid? OperatorId = null);
