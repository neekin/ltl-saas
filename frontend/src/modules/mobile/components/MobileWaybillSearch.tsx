import { Button, Input, Toast } from 'antd-mobile';
import { ScanCodeOutline } from 'antd-mobile-icons';
import { useState } from 'react';
import { QrScanner } from './QrScanner';

/** 从二维码文本中提取运单号：支持纯运单号或含 waybillNo / waybills 路径的 URL。 */
function extractWaybillNo(text: string): string {
  const trimmed = text.trim();
  const match = trimmed.match(/(?:waybillNo=|waybills\/)([\w-]+)/i);
  return match?.[1] ?? trimmed;
}

interface MobileWaybillSearchProps {
  value: string;
  onChange: (value: string) => void;
  onSearch: (waybillNo: string) => void;
  loading?: boolean;
  submitText?: string;
  placeholder?: string;
  scanTitle?: string;
}

/** 移动端统一的运单定位控件：手动输入与摄像头扫码使用同一提交入口。 */
export function MobileWaybillSearch({
  value,
  onChange,
  onSearch,
  loading = false,
  submitText = '查询',
  placeholder = '输入完整运单号',
  scanTitle = '扫描运单二维码',
}: MobileWaybillSearchProps) {
  const [scannerOpen, setScannerOpen] = useState(false);

  const submit = (input: string) => {
    const waybillNo = extractWaybillNo(input);
    if (!waybillNo) {
      Toast.show({ content: '请输入或扫描运单号', position: 'bottom' });
      return;
    }
    onChange(waybillNo);
    onSearch(waybillNo);
  };

  return (
    <>
      <div className="mobile-lookup">
        <div className="mobile-lookup__input">
          <Input
            aria-label="运单号"
            placeholder={placeholder}
            value={value}
            onChange={onChange}
            onEnterPress={() => submit(value)}
            enterKeyHint="search"
            clearable
          />
        </div>
        <Button
          color="primary"
          loading={loading}
          disabled={!value.trim()}
          onClick={() => submit(value)}
        >
          {submitText}
        </Button>
      </div>
      <Button
        block
        fill="outline"
        className="mobile-scan-button"
        disabled={loading}
        onClick={() => setScannerOpen(true)}
      >
        <span className="mobile-inline-icon">
          <ScanCodeOutline /> 摄像头扫码
        </span>
      </Button>
      {scannerOpen && (
        <QrScanner
          title={scanTitle}
          onResult={(text) => {
            setScannerOpen(false);
            submit(text);
          }}
          onClose={() => setScannerOpen(false)}
        />
      )}
    </>
  );
}
