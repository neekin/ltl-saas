import { useEffect, useRef, useState } from 'react';
import { Dialog } from 'antd-mobile';
import { BrowserMultiFormatReader, type IScannerControls } from '@zxing/browser';
import { BarcodeFormat, DecodeHintType } from '@zxing/library';

/**
 * 摄像头扫码组件：连续识别 QR 码，识别成功回调一次并停止。
 * 无摄像头 / 权限拒绝时展示错误提示，由调用方提供手动输入兜底。
 */
export interface QrScannerProps {
  onResult: (text: string) => void;
  onClose: () => void;
  title?: string;
}

export function QrScanner({ onResult, onClose, title = '扫码查询' }: QrScannerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const controlsRef = useRef<IScannerControls | null>(null);
  const firedRef = useRef(false);
  const onResultRef = useRef(onResult);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    onResultRef.current = onResult;
  }, [onResult]);

  useEffect(() => {
    let cancelled = false;
    firedRef.current = false;
    setError(null);
    const hints = new Map();
    hints.set(DecodeHintType.POSSIBLE_FORMATS, [BarcodeFormat.QR_CODE]);
    const reader = new BrowserMultiFormatReader(hints);

    reader
      .decodeFromVideoDevice(undefined, videoRef.current ?? undefined, (result, _err, controls) => {
        if (cancelled || firedRef.current) return;
        controlsRef.current = controls;
        if (result) {
          firedRef.current = true;
          controls?.stop();
          onResultRef.current(result.getText());
        }
      })
      .catch(() => {
        if (!cancelled) setError('无法访问摄像头，请检查浏览器权限，或关闭扫码后手动输入');
      });

    return () => {
      cancelled = true;
      controlsRef.current?.stop();
      controlsRef.current = null;
    };
  }, [retryKey]);

  return (
    <Dialog
      visible
      onClose={onClose}
      closeOnMaskClick
      title={title}
      content={
        <div className="mobile-scanner">
          <div className="mobile-scanner__frame">
            <video
              ref={videoRef}
              muted
              playsInline
              aria-label="二维码扫描画面"
              style={{ visibility: error ? 'hidden' : 'visible' }}
            />
            {error ? (
              <div className="mobile-scanner__error" role="alert">
                {error}
              </div>
            ) : (
              <span className="mobile-scanner__corner" aria-hidden="true" />
            )}
          </div>
          <div className="mobile-scanner__tip">
            {error ? '摄像头不可用时可重试，或改用手动输入' : '将完整二维码放入取景框内'}
          </div>
        </div>
      }
      closeOnAction={false}
      actions={[
        ...(error ? [{ key: 'retry', text: '重试', onClick: () => setRetryKey((v) => v + 1) }] : []),
        { key: 'cancel', text: error ? '手动输入' : '取消', onClick: onClose },
      ]}
    />
  );
}
