import { useMutation, useQuery } from '@tanstack/react-query';
import { Button, Card, ErrorBlock, Input, Skeleton, Stepper, Tag } from 'antd-mobile';
import { CheckCircleOutline } from 'antd-mobile-icons';
import { useState } from 'react';
import { formatStatus } from '../../../shared/components';
import { getArrivalByTrip, recordArrival, type ArrivalDto } from '../../arrivals/api/arrivals';
import { getWaybill } from '../../waybills/api/waybills';
import { getTrip, listTrips, type Trip } from '../../transport/api/trips';

interface ArrivalRow {
  waybillId: string;
  waybillNo: string;
  loadedPieces: number;
  receivedPieces: number;
  brokenPieces: number;
  remark: string;
}

interface PreparedArrival {
  trip: Trip;
  arrival: ArrivalDto | null;
  rows: ArrivalRow[];
}

interface Completion {
  count: number;
  differences: number;
}

/** 移动到货核验（P10）：选择已发车批次，自动带出到货清单和到达网点。 */
export function MobileArrivalPage() {
  const [selectedTripId, setSelectedTripId] = useState<string | null>(null);
  const [prepared, setPrepared] = useState<PreparedArrival | null>(null);
  const [rows, setRows] = useState<ArrivalRow[]>([]);
  const [note, setNote] = useState('');
  const [completion, setCompletion] = useState<Completion | null>(null);

  const tripsQuery = useQuery({
    queryKey: ['mobile-arrival-trips'],
    queryFn: () => listTrips(1, 50),
  });

  const prepareMutation = useMutation({
    mutationFn: async (tripId: string): Promise<PreparedArrival> => {
      const [trip, arrival] = await Promise.all([getTrip(tripId), getArrivalByTrip(tripId)]);
      if (arrival) {
        const details = await Promise.all(
          arrival.items.map((item) => getWaybill(item.waybillId).catch(() => null)),
        );
        return {
          trip,
          arrival,
          rows: arrival.items.map((item, index) => ({
            waybillId: item.waybillId,
            waybillNo: details[index]?.waybillNo ?? '运单信息不可用',
            loadedPieces: item.loadedPieces,
            receivedPieces: item.receivedPieces,
            brokenPieces: item.brokenPieces,
            remark: item.remark ?? '',
          })),
        };
      }
      if (trip.status !== 'Departed') {
        throw new Error('仅已发车批次可首次登记到货');
      }
      const active = trip.waybills.filter((item) => item.active);
      if (active.length === 0) throw new Error('该批次没有在车运单，无法登记到货');
      const details = await Promise.all(active.map((item) => getWaybill(item.waybillId)));
      return {
        trip,
        arrival: null,
        rows: details.map((waybill) => {
          const loadedPieces = waybill.packages.reduce((sum, item) => sum + item.quantity, 0);
          if (loadedPieces < 1) throw new Error(`运单 ${waybill.waybillNo} 没有有效货物件`);
          return {
            waybillId: waybill.id,
            waybillNo: waybill.waybillNo,
            loadedPieces,
            receivedPieces: loadedPieces,
            brokenPieces: 0,
            remark: '',
          };
        }),
      };
    },
    onSuccess: (data) => {
      setPrepared(data);
      setRows(data.rows);
      setNote('');
      setCompletion(null);
    },
  });

  const submitMutation = useMutation({
    mutationFn: () => {
      const invalid = rows.find(
        (row) =>
          row.receivedPieces < 0 ||
          row.receivedPieces > row.loadedPieces ||
          row.brokenPieces < 0 ||
          row.brokenPieces > row.receivedPieces,
      );
      if (!prepared || invalid) throw new Error('请核实例收与破损件数后再提交');
      return recordArrival({
        tripId: prepared.trip.id,
        branchId: prepared.trip.destBranchId,
        items: rows.map((row) => ({
          waybillId: row.waybillId,
          receivedPieces: row.receivedPieces,
          brokenPieces: row.brokenPieces,
          remark: row.remark.trim() || undefined,
        })),
        note: note.trim() || undefined,
      });
    },
    onSuccess: (result) =>
      setCompletion({
        count: rows.length,
        differences: result.differences.filter((item) => item.pieceDifference !== 0).length,
      }),
  });

  const backToList = () => {
    setSelectedTripId(null);
    setPrepared(null);
    setRows([]);
    setNote('');
    setCompletion(null);
    prepareMutation.reset();
    submitMutation.reset();
  };

  const updateRow = (waybillId: string, patch: Partial<ArrivalRow>) => {
    setRows((current) =>
      current.map((row) => {
        if (row.waybillId !== waybillId) return row;
        const next = { ...row, ...patch };
        next.receivedPieces = Math.max(0, Math.min(next.receivedPieces, next.loadedPieces));
        next.brokenPieces = Math.max(0, Math.min(next.brokenPieces, next.receivedPieces));
        return next;
      }),
    );
  };

  const candidates = (tripsQuery.data?.items ?? []).filter((trip) => trip.status === 'Departed');

  if (completion) {
    return (
      <div className="mobile-stack">
        <Card className="mobile-success-card">
          <CheckCircleOutline className="mobile-success-icon" />
          <h2>到货登记完成</h2>
          <p>
            已登记 {completion.count} 票运单，其中 {completion.differences} 票存在件数差异。
          </p>
          <Button block color="primary" onClick={backToList}>
            继续处理其他批次
          </Button>
          <Button
            block
            fill="outline"
            onClick={() => {
              setCompletion(null);
              setPrepared(null);
              prepareMutation.mutate(selectedTripId!);
            }}
          >
            查看该批次登记结果
          </Button>
        </Card>
      </div>
    );
  }

  if (!selectedTripId) {
    return (
      <div className="mobile-stack">
        <Card
          title="1. 选择已发车批次"
          extra={
            <Button
              size="small"
              fill="none"
              color="primary"
              disabled={tripsQuery.isFetching}
              onClick={() => void tripsQuery.refetch()}
            >
              刷新
            </Button>
          }
        >
          {tripsQuery.isLoading && (
            <>
              <Skeleton.Title animated />
              <Skeleton.Paragraph lineCount={3} animated />
            </>
          )}
          {tripsQuery.isError && (
            <ErrorBlock status="disconnected" title="批次加载失败" description="请检查网络后重试">
              <Button color="primary" fill="outline" onClick={() => void tripsQuery.refetch()}>
                重新加载
              </Button>
            </ErrorBlock>
          )}
          {!tripsQuery.isLoading && !tripsQuery.isError && candidates.length === 0 && (
            <ErrorBlock status="empty" title="暂无待到货批次" description="仅展示已发车批次" />
          )}
          {!tripsQuery.isLoading &&
            !tripsQuery.isError &&
            candidates.map((trip) => (
              <button
                key={trip.id}
                type="button"
                className="mobile-trip-row"
                onClick={() => {
                  setSelectedTripId(trip.id);
                  prepareMutation.mutate(trip.id);
                }}
              >
                <span>
                  <strong className="ltl-code">{trip.tripNo}</strong>
                  <small>{trip.waybills.filter((item) => item.active).length} 票在车</small>
                </span>
                <Tag color="primary">{formatStatus(trip.status, 'trip')}</Tag>
              </button>
            ))}
        </Card>
      </div>
    );
  }

  return (
    <div className="mobile-stack">
      <div className="mobile-subnav">
        <Button fill="none" color="default" onClick={backToList}>
          更换批次
        </Button>
      </div>

      {prepareMutation.isPending && (
        <Card>
          <Skeleton.Title animated />
          <Skeleton.Paragraph lineCount={4} animated />
        </Card>
      )}

      {prepareMutation.isError && (
        <ErrorBlock
          status="default"
          title="无法加载到货清单"
          description={prepareMutation.error.message}
        >
          <Button
            color="primary"
            fill="outline"
            onClick={() => prepareMutation.mutate(selectedTripId)}
          >
            重新加载
          </Button>
        </ErrorBlock>
      )}

      {prepared && (
        <>
          <Card title={`2. 核对清单 · ${prepared.trip.tripNo}`}>
            <div className="mobile-result-head">
              <div>
                <span className="mobile-field-label">到达网点</span>
                <strong>按批次目的网点自动带入</strong>
              </div>
              <Tag color={prepared.arrival ? 'success' : 'primary'}>
                {prepared.arrival ? '已登记' : '待登记'}
              </Tag>
            </div>
            <p className="mobile-hint">
              {prepared.arrival
                ? '该批次已有到货记录，以下为历史结果，只读展示，避免重复登记。'
                : '已按在车运单带出装车件数，请逐票核对实收与破损。'}
            </p>
            {prepared.arrival && (
              <p className="mobile-hint">
                登记时间：{new Date(prepared.arrival.arrivedAt).toLocaleString('zh-CN')}
              </p>
            )}
          </Card>

          {prepared.arrival ? (
            rows.map((row) => (
              <Card key={row.waybillId} title={row.waybillNo}>
                <div className="mobile-summary">
                  <div>
                    <span>装车</span>
                    <strong>{row.loadedPieces} 件</strong>
                  </div>
                  <div>
                    <span>实收</span>
                    <strong>{row.receivedPieces} 件</strong>
                  </div>
                  <div>
                    <span>破损</span>
                    <strong>{row.brokenPieces} 件</strong>
                  </div>
                </div>
                {row.remark && <p className="mobile-hint">备注：{row.remark}</p>}
              </Card>
            ))
          ) : (
            <>
              {rows.map((row) => {
                const difference = row.receivedPieces - row.loadedPieces;
                return (
                  <Card key={row.waybillId} title={row.waybillNo}>
                    <div className={`mobile-difference ${difference < 0 ? 'mobile-danger' : ''}`}>
                      装车 {row.loadedPieces} 件 ｜{' '}
                      {difference === 0
                        ? '件数一致'
                        : difference < 0
                          ? `短少 ${Math.abs(difference)} 件`
                          : `多收 ${difference} 件`}
                    </div>
                    <div className="mobile-form-field">
                      <label>实收件数（最多 {row.loadedPieces} 件）</label>
                      <Stepper
                        min={0}
                        max={row.loadedPieces}
                        digits={0}
                        value={row.receivedPieces}
                        onChange={(value) =>
                          updateRow(row.waybillId, { receivedPieces: value ?? 0 })
                        }
                      />
                    </div>
                    <div className="mobile-form-field">
                      <label>破损件数</label>
                      <Stepper
                        min={0}
                        max={row.receivedPieces}
                        digits={0}
                        value={row.brokenPieces}
                        onChange={(value) => updateRow(row.waybillId, { brokenPieces: value ?? 0 })}
                      />
                    </div>
                    <div className="mobile-form-field">
                      <label htmlFor={`arrival-remark-${row.waybillId}`}>差异备注（可选）</label>
                      <Input
                        id={`arrival-remark-${row.waybillId}`}
                        placeholder="短少、破损等情况说明"
                        value={row.remark}
                        onChange={(value) => updateRow(row.waybillId, { remark: value })}
                        clearable
                      />
                    </div>
                  </Card>
                );
              })}
              <Card title="3. 提交登记">
                <div className="mobile-form-field">
                  <label htmlFor="arrival-note">整批备注（可选）</label>
                  <Input
                    id="arrival-note"
                    placeholder="整批到货情况说明"
                    value={note}
                    onChange={setNote}
                    clearable
                  />
                </div>
                {submitMutation.isError && (
                  <div className="mobile-danger mobile-hint" role="alert">
                    {submitMutation.error.message}
                  </div>
                )}
                <Button
                  block
                  color="primary"
                  size="large"
                  loading={submitMutation.isPending}
                  onClick={() => submitMutation.mutate()}
                >
                  提交到货登记
                </Button>
              </Card>
            </>
          )}
        </>
      )}
    </div>
  );
}
