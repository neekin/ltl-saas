import { useCallback, useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Button, Card, List, Toast } from 'antd-mobile';
import { listTrips, reportGps, type Trip } from '../../transport/api/trips';

/**
 * 位置上报页（GPS-001，P11 移动作业）：
 * 选择发车中的批次，用浏览器 Geolocation 上报当前位置（追加到批次轨迹）。
 */
export function MobileGpsPage() {
  const [tripId, setTripId] = useState<string>('');
  const [message, setMessage] = useState<string | null>(null);

  const tripsQuery = useQuery({
    queryKey: ['mobile-trips'],
    queryFn: () => listTrips(1, 50),
    retry: false,
  });

  // 仅 Departed 批次可上报
  const departedTrips = (tripsQuery.data?.items ?? []).filter((t: Trip) => t.status === 'Departed');

  useEffect(() => {
    if (!tripId && departedTrips.length > 0) {
      setTripId(departedTrips[0].id);
    }
  }, [departedTrips, tripId]);

  const reportMutation = useMutation({
    mutationFn: async () => {
      const position = await new Promise<GeolocationPosition>((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 10_000,
          maximumAge: 30_000,
        });
      });
      const lat = Number(position.coords.latitude.toFixed(6));
      const lng = Number(position.coords.longitude.toFixed(6));
      const acc = position.coords.accuracy != null ? Number(position.coords.accuracy.toFixed(1)) : null;
      await reportGps(tripId, { latitude: lat, longitude: lng, accuracyMeters: acc });
      return { lat, lng, acc };
    },
    onSuccess: ({ lat, lng, acc }) => {
      setMessage(`已上报：${lat}, ${lng}${acc != null ? `（精度 ${acc}m）` : ''}`);
      Toast.show({ content: '位置已上报', icon: 'success' });
    },
    onError: (e: Error) => {
      Toast.show({ content: e.message, icon: 'fail' });
    },
  });

  const report = useCallback(() => {
    if (!tripId) {
      Toast.show({ content: '请选择批次', icon: 'fail' });
      return;
    }
    reportMutation.mutate();
  }, [tripId, reportMutation]);

  return (
    <div style={{ padding: 12 }}>
      <Card title="位置上报" extra={<span style={{ fontSize: 12, color: '#8c96a5' }}>GPS-001</span>}>
        <p style={{ color: '#8c96a5', fontSize: 13 }}>
          仅「发车中」批次可上报；位置写入批次轨迹，用于运输过程追溯。
        </p>
        <select
          value={tripId}
          onChange={(e) => setTripId(e.target.value)}
          style={{
            width: '100%',
            padding: '10px 12px',
            borderRadius: 8,
            border: '1px solid #d9d9d9',
            fontSize: 15,
            marginBottom: 12,
          }}
        >
          {departedTrips.length === 0 && <option value="">无发车中的批次</option>}
          {departedTrips.map((t) => (
            <option key={t.id} value={t.id}>{t.tripNo}</option>
          ))}
        </select>
        <Button
          block
          color="primary"
          size="large"
          style={{ marginTop: 12 }}
          loading={reportMutation.isPending}
          onClick={report}
          disabled={departedTrips.length === 0}
        >
          上报当前位置
        </Button>
        {departedTrips.length === 0 && (
          <p style={{ color: '#e6a23c', fontSize: 13 }}>当前无发车中的批次。</p>
        )}
        {message && <p style={{ color: '#4caf6e', fontSize: 13 }}>{message}</p>}
        <List>
          <List.Item style={{ fontSize: 12, color: '#8c96a5' }}>
            浏览器可能请求定位权限，请允许；上报频率建议不低于 30 秒一次。
          </List.Item>
        </List>
      </Card>
    </div>
  );
}
