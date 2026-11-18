import { useQuery } from '@tanstack/react-query';
import { Alert, Button, QRCode, Space, Spin, Typography } from 'antd';
import { useNavigate, useParams } from 'react-router-dom';
import { PageHeader, formatStatus } from '../../../shared/components';
import { useAuthStore } from '../../auth/store/authStore';
import { getWaybillPrintView } from '../api/waybills';

const paymentTypeText: Record<string, string> = {
  Cash: '现付',
  OnDelivery: '到付',
  Monthly: '月结',
};

/** 标准 A5 电子面单（P10）：专用打印模板，二维码指向客户查询入口。 */
export function WaybillLabelPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const tenantCode = useAuthStore((s) => s.user?.tenantCode);
  const printQuery = useQuery({
    queryKey: ['waybill-print', id],
    queryFn: () => getWaybillPrintView(id!),
    enabled: !!id,
  });

  const backToDetail = () => navigate(`/waybills/${id}`);
  const actions = (
    <Space>
      <Button onClick={() => window.print()} type="primary" disabled={!printQuery.data}>
        打印面单
      </Button>
      <Button onClick={backToDetail}>返回运单详情</Button>
    </Space>
  );

  if (printQuery.isLoading) {
    return (
      <>
        <PageHeader title="电子面单" onBack={backToDetail} actions={actions} />
        <Spin tip="加载面单数据…" />
      </>
    );
  }

  if (printQuery.isError || !printQuery.data) {
    return (
      <>
        <PageHeader title="电子面单" onBack={backToDetail} actions={actions} />
        <Alert
          type="error"
          showIcon
          message="面单数据加载失败"
          description={printQuery.error?.message ?? '请稍后重试'}
          action={
            <Button size="small" onClick={() => void printQuery.refetch()}>
              重新加载
            </Button>
          }
        />
      </>
    );
  }

  const d = printQuery.data;
  const trackUrl = new URL('/track', window.location.origin);
  trackUrl.searchParams.set('waybillNo', d.waybillNo);
  if (tenantCode) trackUrl.searchParams.set('tenantCode', tenantCode);

  return (
    <div>
      <PageHeader
        title="电子面单"
        description="浏览器打印时仅输出下方 A5 面单区域；货物件较多时会自动连续分页。"
        onBack={backToDetail}
        actions={actions}
      />
      <Typography.Paragraph type="secondary" className="ltl-no-print">
        面单二维码可直达客户查询页并预填运单号，客户仍需输入承运方提供的查询码。
      </Typography.Paragraph>

      <div
        className="ltl-label"
        style={{
          width: '148mm',
          minHeight: '210mm',
          margin: '0 auto',
          background: '#fff',
          padding: '8mm',
          boxSizing: 'border-box',
          color: '#000',
          border: '1px solid #999',
          fontFamily: 'Arial, "Microsoft YaHei", sans-serif',
        }}
      >
        <div
          className="ltl-label__block"
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'flex-start',
            borderBottom: '2px solid #000',
            paddingBottom: 6,
          }}
        >
          <div>
            <div style={{ fontSize: 20, fontWeight: 700 }}>LTL 零担物流</div>
            <div style={{ fontSize: 10 }}>专线运输 · 货物托运单</div>
          </div>
          <QRCode value={trackUrl.toString()} size={64} bordered={false} />
        </div>

        <div
          className="ltl-label__block"
          style={{
            margin: '8px 0',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'baseline',
          }}
        >
          <div>
            <span style={{ fontSize: 11 }}>运单号</span>
            <div
              style={{ fontSize: 20, fontWeight: 700, letterSpacing: 2, fontFamily: 'monospace' }}
            >
              {d.waybillNo}
            </div>
          </div>
          <div style={{ fontSize: 11, textAlign: 'right' }}>
            <div>开单时间：{new Date(d.createdAt).toLocaleString('zh-CN')}</div>
            <div>支付方式：{paymentTypeText[d.paymentType] ?? d.paymentType}</div>
          </div>
        </div>

        <table
          className="ltl-label__block"
          style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12, marginBottom: 8 }}
        >
          <tbody>
            <tr>
              <td style={{ border: '1px solid #000', padding: 6, width: '50%' }}>
                <div style={{ fontSize: 10, color: '#555' }}>发货人</div>
                <div style={{ fontWeight: 700, fontSize: 14 }}>{d.senderName}</div>
                <div>{d.senderPhone}</div>
                <div style={{ marginTop: 4 }}>{d.senderAddress}</div>
              </td>
              <td style={{ border: '1px solid #000', padding: 6, width: '50%' }}>
                <div style={{ fontSize: 10, color: '#555' }}>收货人</div>
                <div style={{ fontWeight: 700, fontSize: 14 }}>{d.receiverName}</div>
                <div>{d.receiverPhone}</div>
                <div style={{ marginTop: 4 }}>{d.receiverAddress}</div>
              </td>
            </tr>
          </tbody>
        </table>

        <table
          className="ltl-label__packages"
          style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11, marginBottom: 8 }}
        >
          <thead>
            <tr style={{ background: '#f0f0f0' }}>
              <th style={{ border: '1px solid #000', padding: 4 }}>件号</th>
              <th style={{ border: '1px solid #000', padding: 4 }}>货名</th>
              <th style={{ border: '1px solid #000', padding: 4 }}>件数</th>
              <th style={{ border: '1px solid #000', padding: 4 }}>重量(kg)</th>
              <th style={{ border: '1px solid #000', padding: 4 }}>体积(m³)</th>
            </tr>
          </thead>
          <tbody>
            {d.packages.map((p) => (
              <tr key={p.packageNo}>
                <td style={{ border: '1px solid #000', padding: 4, textAlign: 'center' }}>
                  {p.packageNo}
                </td>
                <td style={{ border: '1px solid #000', padding: 4 }}>{p.cargoName}</td>
                <td style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                  {p.quantity}
                </td>
                <td style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                  {p.weightKg.toFixed(3)}
                </td>
                <td style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                  {p.volumeM3.toFixed(4)}
                </td>
              </tr>
            ))}
            <tr style={{ fontWeight: 700 }}>
              <td colSpan={2} style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                合计
              </td>
              <td style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                {d.declaredPieces}
              </td>
              <td style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                {d.declaredWeightKg.toFixed(3)}
              </td>
              <td style={{ border: '1px solid #000', padding: 4, textAlign: 'right' }}>
                {d.declaredVolumeM3.toFixed(4)}
              </td>
            </tr>
          </tbody>
        </table>

        <table
          className="ltl-label__block"
          style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}
        >
          <tbody>
            <tr>
              <td style={{ border: '1px solid #000', padding: 6 }}>
                运费应收：<b>¥ {d.totalAmount === null ? '未固化' : d.totalAmount.toFixed(2)}</b>
              </td>
              <td style={{ border: '1px solid #000', padding: 6 }}>
                代收货款：{d.codAmount === null ? '无' : `¥ ${d.codAmount.toFixed(2)}`}
              </td>
            </tr>
            <tr>
              <td style={{ border: '1px solid #000', padding: 6 }} colSpan={2}>
                状态：运单 {formatStatus(d.waybillStatus, 'waybill')} ｜ 货物{' '}
                {formatStatus(d.cargoStatus)} ｜ 运输 {formatStatus(d.transportStatus)} ｜ 交付{' '}
                {formatStatus(d.deliveryStatus)} ｜ 财务 {formatStatus(d.freightStatus)}
              </td>
            </tr>
          </tbody>
        </table>

        <div style={{ marginTop: 12, fontSize: 10, color: '#555', textAlign: 'center' }}>
          请核对货物件数与外观；签收后视为验收合格。客户可凭承运方编码、运单号与查询码查询运输进度。
        </div>
      </div>
    </div>
  );
}
