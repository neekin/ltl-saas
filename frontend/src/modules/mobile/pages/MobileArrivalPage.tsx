import { useMutation, useQuery } from '@tanstack/react-query';
import { Button, Card, Input, Stepper, Toast } from 'antd-mobile';
import { useState } from 'react';
import { getArrivalByTrip, recordArrival } from '../../arrivals/api/arrivals';

interface ReceivedRow {
  waybillId: string;
  receivedPieces: number;
  brokenPieces: number;
  remark: string;
}

/** 移动到货登记（P10）：按批次查询到货记录，登记实收/破损件数。 */
export function MobileArrivalPage() {
  const [tripId, setTripId] = useState('');
  const [searchedTripId, setSearchedTripId] = useState('');
  const [branchId, setBranchId] = useState('');
  const [rows, setRows] = useState<ReceivedRow[]>([]);

  const arrivalQuery = useQuery({
    queryKey: ['mobile-arrival', searchedTripId],
    queryFn: () => getArrivalByTrip(searchedTripId),
    enabled: !!searchedTripId,
  });

  const arrival = arrivalQuery.data;
  const effectiveRows = rows.length
    ? rows
    : (arrival?.items ?? []).map((i) => ({
        waybillId: i.waybillId,
        receivedPieces: i.receivedPieces,
        brokenPieces: i.brokenPieces,
        remark: i.remark ?? '',
      }));

  const arrivalMutation = useMutation({
    mutationFn: () =>
      recordArrival({
        tripId: searchedTripId,
        branchId: branchId.trim(),
        items: effectiveRows.map((r) => ({
          waybillId: r.waybillId,
          receivedPieces: r.receivedPieces,
          brokenPieces: r.brokenPieces,
          remark: r.remark || undefined,
        })),
      }),
    onSuccess: () => {
      Toast.show({ content: '到货登记成功', position: 'bottom' });
      setRows([]);
      setBranchId('');
    },
    onError: (e: Error) => Toast.show({ content: e.message, position: 'bottom' }),
  });

  const search = () => {
    if (!tripId.trim()) {
      Toast.show({ content: '请输入运输批次 ID', position: 'bottom' });
      return;
    }
    setRows([]);
    setSearchedTripId(tripId.trim());
  };

  const updateRow = (waybillId: string, patch: Partial<ReceivedRow>) => {
    setRows(effectiveRows.map((r) => (r.waybillId === waybillId ? { ...r, ...patch } : r)));
  };

  return (
    <div>
      <Card title="查询批次到货记录">
        <div style={{ display: 'flex', gap: 8 }}>
          <Input placeholder="运输批次 ID" value={tripId} onChange={setTripId} clearable />
          <Button color="primary" onClick={search}>
            查询
          </Button>
        </div>
      </Card>

      {searchedTripId && !arrivalQuery.isLoading && !arrival && (
        <Card style={{ marginTop: 12 }}>
          <div style={{ color: '#999', textAlign: 'center', padding: 20 }}>该批次无到货记录</div>
        </Card>
      )}

      {arrival && (
        <Card title={`到货清单（批次 ${searchedTripId}）`} style={{ marginTop: 12 }}>
          <div style={{ fontSize: 12, color: '#999', marginBottom: 8 }}>
            到达网点：{arrival.branchId}
          </div>
          {effectiveRows.map((row, index) => {
            const loaded = arrival.items[index]?.loadedPieces ?? 0;
            const diff = row.receivedPieces - loaded;
            return (
              <div
                key={row.waybillId}
                style={{ borderTop: '1px solid #f0f0f0', padding: '10px 0' }}
              >
                <div style={{ fontSize: 12, display: 'flex', justifyContent: 'space-between' }}>
                  <span>{row.waybillId.slice(0, 12)}…</span>
                  <span style={{ color: diff === 0 ? '#52c41a' : '#ff4d4f' }}>
                    装车 {loaded} / 实收 {row.receivedPieces}
                  </span>
                </div>
                <div style={{ display: 'flex', gap: 12, marginTop: 8, alignItems: 'center' }}>
                  <div>
                    <div style={{ fontSize: 11, color: '#999' }}>实收</div>
                    <Stepper
                      min={0}
                      digits={0}
                      value={row.receivedPieces}
                      onChange={(v) => updateRow(row.waybillId, { receivedPieces: v })}
                    />
                  </div>
                  <div>
                    <div style={{ fontSize: 11, color: '#999' }}>破损</div>
                    <Stepper
                      min={0}
                      digits={0}
                      value={row.brokenPieces}
                      onChange={(v) => updateRow(row.waybillId, { brokenPieces: v })}
                    />
                  </div>
                </div>
              </div>
            );
          })}
          <div style={{ marginTop: 12 }}>
            <div style={{ fontSize: 13, marginBottom: 6 }}>到达网点 ID</div>
            <Input placeholder="输入到达网点 ID" value={branchId} onChange={setBranchId} />
          </div>
          <Button
            block
            color="primary"
            style={{ marginTop: 12 }}
            disabled={!branchId.trim()}
            loading={arrivalMutation.isPending}
            onClick={() => arrivalMutation.mutate()}
          >
            提交到货登记
          </Button>
        </Card>
      )}
    </div>
  );
}
