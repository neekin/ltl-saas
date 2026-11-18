import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Dialog, Input, Tag, Toast } from 'antd-mobile';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { http } from '../../../shared/api/http';
import {
  getTrip,
  listTrips,
  loadWaybill,
  removeWaybill,
  type Trip,
} from '../../transport/api/trips';

/** 移动装车配载（P10）：选择计划批次，按运单号装车/撤销，查看在车清单。 */
export function MobileLoadPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [activeTripId, setActiveTripId] = useState<string | null>(null);
  const [waybillNo, setWaybillNo] = useState('');

  const tripsQuery = useQuery({ queryKey: ['mobile-trips'], queryFn: () => listTrips(1, 50) });
  const tripQuery = useQuery({
    queryKey: ['mobile-trip', activeTripId],
    queryFn: () => getTrip(activeTripId!),
    enabled: !!activeTripId,
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['mobile-trip', activeTripId] });
    queryClient.invalidateQueries({ queryKey: ['mobile-trips'] });
  };

  const loadMutation = useMutation({
    mutationFn: async () => {
      const response = await http.get<{ items: { id: string; waybillNo: string }[] }>('/waybills', {
        params: { waybillNo: waybillNo.trim(), pageSize: 1 },
      });
      const waybill = response.data.items[0];
      if (!waybill) throw new Error('未找到该运单号');
      await loadWaybill(activeTripId!, waybill.id);
    },
    onSuccess: () => {
      Toast.show({ content: '装车成功', position: 'bottom' });
      setWaybillNo('');
      invalidate();
    },
    onError: (e: Error) => Toast.show({ content: e.message, position: 'bottom' }),
  });

  const removeMutation = useMutation({
    mutationFn: (assignmentId: string) => removeWaybill(activeTripId!, assignmentId),
    onSuccess: () => {
      Toast.show({ content: '已撤销配载', position: 'bottom' });
      invalidate();
    },
    onError: (e: Error) => Toast.show({ content: e.message, position: 'bottom' }),
  });

  const planned = (tripsQuery.data?.items ?? []).filter((t) => t.status === 'Planned');

  if (!activeTripId) {
    return (
      <div>
        <Card title="选择计划批次">
          {planned.length === 0 && (
            <div style={{ color: '#999', textAlign: 'center', padding: 20 }}>暂无计划批次</div>
          )}
          {planned.map((trip: Trip) => (
            <div
              key={trip.id}
              onClick={() => setActiveTripId(trip.id)}
              style={{
                borderTop: '1px solid #f0f0f0',
                padding: '12px 0',
                display: 'flex',
                justifyContent: 'space-between',
              }}
            >
              <span>{trip.tripNo}</span>
              <span style={{ color: '#999', fontSize: 12 }}>
                {trip.waybills.filter((w) => w.active).length} 票在车 ›
              </span>
            </div>
          ))}
        </Card>
      </div>
    );
  }

  const trip = tripQuery.data;

  return (
    <div>
      <Card title={trip ? `批次 ${trip.tripNo}` : '加载中'}>
        <Button block onClick={() => setActiveTripId(null)} style={{ marginBottom: 12 }}>
          ‹ 返回批次列表
        </Button>
        <div style={{ display: 'flex', gap: 8 }}>
          <Input placeholder="输入运单号装车" value={waybillNo} onChange={setWaybillNo} clearable />
          <Button
            color="primary"
            loading={loadMutation.isPending}
            onClick={() => loadMutation.mutate()}
          >
            装车
          </Button>
        </div>
      </Card>

      <Card title="在车清单" style={{ marginTop: 12 }}>
        {(trip?.waybills ?? []).length === 0 && (
          <div style={{ color: '#999', textAlign: 'center', padding: 16 }}>尚未配载运单</div>
        )}
        {(trip?.waybills ?? []).map((w) => (
          <div key={w.id} style={{ borderTop: '1px solid #f0f0f0', padding: '10px 0' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span className="mono" style={{ fontSize: 12 }}>
                {w.waybillId}
              </span>
              <Tag color={w.active ? 'success' : 'default'}>{w.active ? '在车' : '已卸'}</Tag>
            </div>
            {w.active && (
              <Button
                fill="none"
                color="danger"
                size="mini"
                style={{ marginTop: 6 }}
                onClick={() =>
                  void Dialog.confirm({
                    content: '撤销该运单装车？',
                    onConfirm: () => {
                      removeMutation.mutate(w.id);
                    },
                  })
                }
              >
                撤销
              </Button>
            )}
          </div>
        ))}
      </Card>

      <Button
        block
        color="primary"
        style={{ marginTop: 12 }}
        onClick={() => navigate(`/transport/trips/${activeTripId}`)}
      >
        在 PC 后台打开配载工作台
      </Button>
    </div>
  );
}
