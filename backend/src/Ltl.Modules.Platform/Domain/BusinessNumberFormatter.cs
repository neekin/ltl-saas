namespace Ltl.Modules.Platform.Domain;

/// <summary>
/// 业务单号格式化（纯函数，ADR-0006）。
/// 格式：{前缀}{网点编码}{yyyyMMdd}{6位序号}，前缀建议 YB=运单、DB=调拨、PC=批次。
/// ⚠️ 格式为建议值，业务审定后仅修改本类与前缀映射，分配机制不受影响。
/// </summary>
public static class BusinessNumberFormatter
{
    /// <summary>序号位数（单网点单日上限 999,999）。</summary>
    public const int SequenceDigits = 6;

    /// <summary>单据类型前缀映射（审定可改）。</summary>
    public static string Prefix(BusinessDocumentType docType) => docType switch
    {
        BusinessDocumentType.Waybill => "YB",
        BusinessDocumentType.BranchTransfer => "DB",
        BusinessDocumentType.TransportTrip => "PC",
        _ => throw new ArgumentOutOfRangeException(nameof(docType), docType, "未知的业务单据类型。"),
    };

    /// <summary>期间字符串（按日重置）：yyyyMMdd。</summary>
    public static string Period(DateOnly date) => date.ToString("yyyyMMdd");

    /// <summary>
    /// 组装业务单号。序号超过容量上限时明确报错（不静默回绕，SEQ-001 设计文档第 4 节）。
    /// </summary>
    public static string Format(BusinessDocumentType docType, string branchCode, DateOnly date, long sequence)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "序号必须从 1 开始。");
        }

        if (string.IsNullOrWhiteSpace(branchCode))
        {
            throw new ArgumentException("网点编码不能为空。", nameof(branchCode));
        }

        var max = (long)Math.Pow(10, SequenceDigits) - 1;
        if (sequence > max)
        {
            throw new InvalidOperationException(
                $"单日序号超出容量上限（{max}）。需评估扩位后迁移，不得静默回绕。");
        }

        return $"{Prefix(docType)}{branchCode.Trim()}{Period(date)}{sequence.ToString($"D{SequenceDigits}")}";
    }
}
