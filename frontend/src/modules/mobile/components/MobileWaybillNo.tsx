import { useQuery } from '@tanstack/react-query';
import { getWaybill } from '../../waybills/api/waybills';

/** 配载记录只返回运单 ID；在移动端按需解析为便于核对的运单号。 */
export function MobileWaybillNo({ waybillId }: { waybillId: string }) {
  const waybillQuery = useQuery({
    queryKey: ['mobile-waybill-number', waybillId],
    queryFn: () => getWaybill(waybillId),
    staleTime: 60_000,
  });

  if (waybillQuery.isLoading) return <span className="mobile-muted">运单号加载中…</span>;
  if (waybillQuery.isError) return <span className="mobile-danger">运单信息加载失败</span>;
  return <span className="ltl-code">{waybillQuery.data?.waybillNo ?? '—'}</span>;
}
