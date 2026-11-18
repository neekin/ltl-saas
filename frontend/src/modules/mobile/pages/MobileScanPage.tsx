import { useMutation } from '@tanstack/react-query';
import { Button, Card, ErrorBlock, Skeleton, Tag } from 'antd-mobile';
import { useState } from 'react';
import { formatStatus } from '../../../shared/components';
import { MobileWaybillSearch } from '../components/MobileWaybillSearch';
import { getWaybillByNo, type WaybillDetail } from '../../waybills/api/waybills';

/** 扫码查询运单（P10）：只显示与现场核对相关的信息，不再跳转 PC 后台。 */
export function MobileScanPage() {
  const [keyword, setKeyword] = useState('');
  const [waybill, setWaybill] = useState<WaybillDetail | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [lookupError, setLookupError] = useState<string | null>(null);

  const searchMutation = useMutation({
    mutationFn: getWaybillByNo,
    onMutate: () => {
      setWaybill(null);
      setNotFound(false);
      setLookupError(null);
    },
    onSuccess: (result) => {
      setWaybill(result);
      setNotFound(result === null);
    },
    onError: (error: Error) => setLookupError(error.message),
  });

  const reset = () => {
    setKeyword('');
    setWaybill(null);
    setNotFound(false);
    setLookupError(null);
    searchMutation.reset();
  };

  const declaredPieces = waybill?.packages.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  const declaredWeight =
    waybill?.packages.reduce((sum, item) => sum + item.weightKg * item.quantity, 0) ?? 0;

  return (
    <div className="mobile-stack">
      <Card title="定位运单">
        <p className="mobile-hint">请输入或扫描完整运单号，系统只返回精确匹配的单据。</p>
        <MobileWaybillSearch
          value={keyword}
          onChange={(value) => {
            setKeyword(value);
            setWaybill(null);
            setNotFound(false);
            setLookupError(null);
          }}
          onSearch={(waybillNo) => searchMutation.mutate(waybillNo)}
          loading={searchMutation.isPending}
        />
      </Card>

      {searchMutation.isPending && (
        <Card>
          <Skeleton.Title animated />
          <Skeleton.Paragraph lineCount={4} animated />
        </Card>
      )}

      {lookupError && (
        <ErrorBlock status="disconnected" title="查询失败" description={lookupError}>
          <Button color="primary" fill="outline" onClick={() => searchMutation.mutate(keyword)}>
            重新查询
          </Button>
        </ErrorBlock>
      )}

      {notFound && !searchMutation.isPending && (
        <ErrorBlock status="empty" title="未找到该运单" description="请核对运单号后重试" />
      )}

      {waybill && (
        <>
          <Card className="mobile-result-card">
            <div className="mobile-result-head">
              <div>
                <span className="mobile-field-label">运单号</span>
                <strong className="ltl-code">{waybill.waybillNo}</strong>
              </div>
              <Tag color="primary">{formatStatus(waybill.status, 'waybill')}</Tag>
            </div>
            <div className="mobile-progress">
              <span className="mobile-field-label">当前运输进度</span>
              <strong>{formatStatus(waybill.transportStatus)}</strong>
              <small>
                货物：{formatStatus(waybill.cargoStatus)} ｜ 交付：
                {formatStatus(waybill.deliveryStatus)}
              </small>
            </div>
            <div className="mobile-metrics">
              <div>
                <span>货物件数</span>
                <strong>{declaredPieces} 件</strong>
              </div>
              <div>
                <span>申报重量</span>
                <strong>{declaredWeight.toFixed(2)} kg</strong>
              </div>
              <div>
                <span>运费应收</span>
                <strong>
                  {waybill.totalAmount === null ? '未固化' : `¥ ${waybill.totalAmount.toFixed(2)}`}
                </strong>
              </div>
            </div>
          </Card>

          <Card title="收发货信息">
            <div className="mobile-info-row">
              <span>发货人</span>
              <strong>{waybill.senderName}</strong>
              <small>{waybill.senderPhone}</small>
            </div>
            <div className="mobile-info-row">
              <span>收货人</span>
              <strong>{waybill.receiverName}</strong>
              <small>{waybill.receiverPhone}</small>
            </div>
          </Card>

          <Card title={`货物件（${waybill.packages.length}）`}>
            {waybill.packages.map((item) => (
              <div key={item.id} className="mobile-package-row">
                <div>
                  <strong>
                    {item.packageNo}. {item.cargoName}
                  </strong>
                  <small>
                    {item.quantity} 件 ｜ {item.weightKg.toFixed(3)} kg ｜{' '}
                    {item.volumeM3.toFixed(4)} m³
                  </small>
                </div>
              </div>
            ))}
          </Card>

          <Button block fill="outline" onClick={reset}>
            查询下一票
          </Button>
        </>
      )}
    </div>
  );
}
