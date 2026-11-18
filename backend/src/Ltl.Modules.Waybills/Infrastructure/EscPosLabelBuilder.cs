using System.Globalization;
using System.Text;
using Ltl.Modules.Waybills.Application;

namespace Ltl.Modules.Waybills.Infrastructure;

/// <summary>
/// ESC/POS 热敏指令生成器（PRINT-001）。58mm 热敏小票风格面单（32 列文本宽），
/// 输出字节流为 `.prn` 文件，供打印代理/热敏打印机直接投递；
/// 指令集：ESC @ 初始化、ESC a 居中、GS ! 倍高、ESC d 走纸、切纸。
/// 真实设备通信（蓝牙/USB/网络）由本地打印代理完成——本层只负责协议指令生成。
/// </summary>
public static class EscPosLabelBuilder
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;
    private const int Columns = 32;

    public static byte[] Build(WaybillPrintDto view)
    {
        var sb = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;

        void Line(string text)
        {
            foreach (var chunk in Wrap(text, Columns))
            {
                sb.AppendLine(chunk);
            }
        }

        void TwoCol(string left, string right)
        {
            var pad = Math.Max(1, Columns - left.Length - right.Length);
            sb.AppendLine(left + new string(' ', pad) + right);
        }

        sb.Append(Esc).Append('@');                       // ESC @ 初始化
        sb.Append(Esc).Append('a').Append((char)1);       // ESC a n 居中
        sb.Append(Gs).Append('!').Append((char)0x30);     // GS ! 倍高倍宽
        sb.AppendLine("LTL 运单");
        sb.Append(Gs).Append('!').Append((char)0);
        sb.Append(Esc).Append('a').Append((char)0);       // 恢复左对齐
        sb.AppendLine(new string('=', Columns));

        Line($"运单号：{view.WaybillNo}");
        Line($"状态：{view.WaybillStatus} / {view.CargoStatus}");
        Line($"支付方式：{view.PaymentType}");
        TwoCol($"件数：{view.DeclaredPieces}", $"重量：{view.DeclaredWeightKg.ToString("0.###", culture)}kg");
        sb.AppendLine(new string('-', Columns));

        Line($"发货：{view.SenderName} {view.SenderPhone}");
        Line(view.SenderAddress);
        Line($"收货：{view.ReceiverName} {view.ReceiverPhone}");
        Line(view.ReceiverAddress);
        sb.AppendLine(new string('-', Columns));

        foreach (var package in view.Packages)
        {
            Line($"#{package.PackageNo} {package.CargoName} ×{package.Quantity} " +
                 $"{package.WeightKg.ToString("0.###", culture)}kg");
        }

        sb.AppendLine(new string('-', Columns));
        if (view.TotalAmount is { } amount)
        {
            TwoCol("运费应收", $"¥{amount.ToString("0.00", culture)}");
        }
        if (view.CodAmount is { } cod)
        {
            TwoCol("COD 代收", $"¥{cod.ToString("0.00", culture)}");
        }
        TwoCol("开单时间", view.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", culture));

        sb.AppendLine();
        sb.AppendLine("签字：____________");
        sb.Append(Esc).Append('d').Append((char)4);       // ESC d n 走纸 4 行
        sb.Append(Gs).Append('V').Append((char)66).Append((char)0); // GS V 切纸

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>按列宽换行（中英文混合按字符数近似处理，热敏打印对逐字符宽度容错）。</summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        for (var offset = 0; offset < text.Length; offset += width)
        {
            yield return text.Substring(offset, Math.Min(width, text.Length - offset));
        }
    }
}
