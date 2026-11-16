namespace Ltl.Modules.Platform.Domain;

/// <summary>
/// 业务单据类型（branch_sequences.doc_type）。新单据类型追加枚举值即可扩展。
/// </summary>
public enum BusinessDocumentType
{
    /// <summary>运单。</summary>
    Waybill = 1,

    /// <summary>网点调拨单。</summary>
    BranchTransfer = 2,

    /// <summary>运输批次。</summary>
    TransportTrip = 3,
}
