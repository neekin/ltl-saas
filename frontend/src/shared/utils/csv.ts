/**
 * 前端 CSV 导出（Excel 兼容：UTF-8 BOM + CRLF）。
 * 报表数据量有限（分页聚合结果），前端生成即可；大数据量导出应走服务端流式接口。
 */
export function downloadCsv(
  filename: string,
  headers: string[],
  rows: (string | number | null | undefined)[][],
): void {
  const escape = (value: string | number | null | undefined) =>
    `"${String(value ?? '').replaceAll('"', '""')}"`;
  const lines = [headers, ...rows].map((row) => row.map(escape).join(','));
  const csv = `\uFEFF${lines.join('\r\n')}`;
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}
