using Ltl.Modules.Platform.Domain;

namespace Ltl.Modules.Platform.Application;

/// <summary>
/// 网点业务单号分配（事务安全：原子 upsert+RETURNING，禁止 SELECT MAX+1）。
/// 单号在租户内唯一（网点编码租户内唯一 + 同网点同日序号递增）。
/// </summary>
public interface IBranchSequenceService
{
    /// <summary>
    /// 分配下一个业务单号。按（租户、网点、单据类型、期间）原子递增；期间为 date 的 yyyyMMdd，按日重置。
    /// </summary>
    /// <param name="branchId">网点 Id（必须属于当前租户）。</param>
    /// <param name="docType">业务单据类型。</param>
    /// <param name="date">单号归属日期（业务发生日；界面按 Asia/Shanghai 取业务日，UTC 转换由调用方决定）。</param>
    /// <param name="branchCode">网点编码（单号组成部分，租户内唯一）。</param>
    Task<string> NextNumberAsync(Guid branchId, BusinessDocumentType docType, DateOnly date, string branchCode);
}
