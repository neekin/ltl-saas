import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Dialog, ErrorBlock, Skeleton, Tag, Toast } from 'antd-mobile';
import { LeftOutline } from 'antd-mobile-icons';
import { useState } from 'react';
import { formatStatus } from '../../../shared/components';
import { MobileWaybillNo } from '../components/MobileWaybillNo';
import { MobileWaybillSearch } from '../components/MobileWaybillSearch';
import { getWaybillByNo } from '../../waybills/api/waybills';
import { getTrip, listTrips, loadWaybill, removeWaybill } from '../../transport/api/trips';

/** 移动装车配载（P10）：先选择可配载批次，再精确扫描运单号装车。 */
export function MobileLoadPage() {
  const queryClient = useQueryClient();
  const [activeTripId, setActiveTripId] = useState<string | null>(null);
  const [keyword, setKeyword] = useState('');

  const tripsQuery = useQuery({ queryKey: ['mobile-trips'], queryFn: () => listTrips(1, 50) });
  const tripQuery = useQuery({
    queryKey: ['mobile-trip', activeTripId],
    queryFn: () => getTrip(activeTripId!),
    enabled: !!activeTripId,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['mobile-trip', activeTripId] });
    void queryClient.invalidateQueries({ queryKey: ['mobile-trips'] });
  };

  const loadMutation = useMutation({
    mutationFn: async ({ waybillNo }: { waybillNo: string }) => {
      const waybill = await getWaybillByNo(waybillNo);
      if (!waybill) throw new Error('未找到该运单号，请核对后重试');
      await loadWaybill(activeTripId!, waybill.id);
      return waybill.waybillNo;
    },
    onSuccess: (waybillNo) => {
      Toast.show({ content: `${waybillNo} 已装车`, position: 'bottom' });
      setKeyword('');
      invalidate();
    },
    onError: (error: Error) => Toast.show({ content: error.message, position: 'bottom' }),
  });

  const removeMutation = useMutation({
    mutationFn: (assignmentId: string) => removeWaybill(activeTripId!, assignmentId),
    onSuccess: () => {
      Toast.show({ content: '已撤销装车', position: 'bottom' });
      invalidate();
    },
    onError: (error: Error) => Toast.show({ content: error.message, position: 'bottom' }),
  });

  const planned = (tripsQuery.data?.items ?? []).filter((trip) => trip.status === 'Planned');

  if (!activeTripId) {
    return (
      <div className="mobile-stack">
        <Card
          title="1. 选择计划批次"
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
          {!tripsQuery.isLoading && !tripsQuery.isError && planned.length === 0 && (
            <ErrorBlock
              status="empty"
              title="暂无计划批次"
              description="请先在 PC 后台创建运输批次"
            />
          )}
          {!tripsQuery.isLoading &&
            !tripsQuery.isError &&
            planned.map((trip) => (
              <button
                key={trip.id}
                type="button"
                className="mobile-trip-row"
                onClick={() => {
                  setActiveTripId(trip.id);
                  setKeyword('');
                }}
              >
                <span>
                  <strong className="ltl-code">{trip.tripNo}</strong>
                  <small>{trip.waybills.filter((item) => item.active).length} 票在车</small>
                </span>
                <Tag color="warning">已计划</Tag>
              </button>
            ))}
        </Card>
      </div>
    );
  }

  const trip = tripQuery.data;

  return (
    <div className="mobile-stack">
      <div className="mobile-subnav">
        <Button
          fill="none"
          color="default"
          onClick={() => {
            setActiveTripId(null);
            setKeyword('');
            loadMutation.reset();
          }}
        >
          <span className="mobile-inline-icon">
            <LeftOutline /> 更换批次
          </span>
        </Button>
      </div>

      {tripQuery.isLoading && !trip && (
        <Card>
          <Skeleton.Title animated />
          <Skeleton.Paragraph lineCount={3} animated />
        </Card>
      )}

      {tripQuery.isError && !trip && (
        <ErrorBlock status="disconnected" title="批次详情加载失败" description="请检查网络后重试">
          <Button color="primary" fill="outline" onClick={() => void tripQuery.refetch()}>
            重新加载
          </Button>
        </ErrorBlock>
      )}

      {trip && (
        <>
          <Card title="2. 扫描装车">
            <div className="mobile-result-head">
              <div>
                <span className="mobile-field-label">当前批次</span>
                <strong className="ltl-code">{trip.tripNo}</strong>
              </div>
              <Tag color="warning">{formatStatus(trip.status, 'trip')}</Tag>
            </div>
            <p className="mobile-hint">请扫描或输入完整运单号；不会按模糊结果自动装车。</p>
            <MobileWaybillSearch
              value={keyword}
              onChange={setKeyword}
              onSearch={(waybillNo) => loadMutation.mutate({ waybillNo })}
              loading={loadMutation.isPending}
              submitText="装车"
              placeholder="输入完整运单号"
              scanTitle="扫描待装运单"
            />
          </Card>

          <Card title={`3. 在车清单（${trip.waybills.filter((item) => item.active).length}）`}>
            {trip.waybills.length === 0 && (
              <div className="mobile-empty">尚未配载运单，扫描后可加入本批次。</div>
            )}
            {[...trip.waybills]
              .sort((a, b) => Number(b.active) - Number(a.active))
              .map((item) => (
                <div key={item.id} className="mobile-assignment-row">
                  <div className="mobile-assignment-row__head">
                    <MobileWaybillNo waybillId={item.waybillId} />
                    <Tag color={item.active ? 'success' : 'default'}>
                      {item.active ? '在车' : '已卸'}
                    </Tag>
                  </div>
                  {item.active && (
                    <Button
                      block
                      size="small"
                      fill="outline"
                      color="danger"
                      loading={removeMutation.isPending}
                      onClick={() =>
                        void Dialog.confirm({
                          title: '撤销装车',
                          content: '撤销后该运单将不在本批次在车清单中，确认继续？',
                          confirmText: '确认撤销',
                          cancelText: '返回',
                          onConfirm: () => {
                            removeMutation.mutate(item.id);
                          },
                        })
                      }
                    >
                      撤销装车
                    </Button>
                  )}
                </div>
              ))}
          </Card>
        </>
      )}
    </div>
  );
}
