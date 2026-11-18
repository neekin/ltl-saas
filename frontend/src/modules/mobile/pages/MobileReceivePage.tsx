import { useMutation } from '@tanstack/react-query';
import { Button, Card, ErrorBlock, Input, Skeleton, Stepper, Toast } from 'antd-mobile';
import { CheckCircleOutline } from 'antd-mobile-icons';
import { useState } from 'react';
import { MobileWaybillSearch } from '../components/MobileWaybillSearch';
import {
  getWaybillByNo,
  recordReceipt,
  type CargoReceiptResult,
  type WaybillDetail,
} from '../../waybills/api/waybills';

interface ReceiveForm {
  pieces: number;
  weight: string;
  volume: string;
  note: string;
}

const emptyForm: ReceiveForm = { pieces: 0, weight: '', volume: '', note: '' };

function differenceText(value: number, unit: string): string {
  if (value === 0) return '一致';
  return `${value > 0 ? '多' : '短少'} ${Math.abs(value)} ${unit}`;
}

/** 移动收货登记（P10）：定位 → 对照申报值 → 录入实收 → 保留成功摘要。 */
export function MobileReceivePage() {
  const [keyword, setKeyword] = useState('');
  const [waybill, setWaybill] = useState<WaybillDetail | null>(null);
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [form, setForm] = useState<ReceiveForm>(emptyForm);
  const [receipt, setReceipt] = useState<CargoReceiptResult | null>(null);

  const locateMutation = useMutation({
    mutationFn: getWaybillByNo,
    onMutate: () => {
      setWaybill(null);
      setLookupError(null);
    },
    onSuccess: (result) => {
      setWaybill(result);
      if (!result) setLookupError('未找到该运单，请核对完整运单号');
      setForm(emptyForm);
    },
    onError: (error: Error) => setLookupError(error.message),
  });

  const declaredPieces = waybill?.packages.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  const declaredWeight =
    waybill?.packages.reduce((sum, item) => sum + item.weightKg * item.quantity, 0) ?? 0;
  const declaredVolume =
    waybill?.packages.reduce((sum, item) => sum + item.volumeM3 * item.quantity, 0) ?? 0;

  const receiveMutation = useMutation({
    mutationFn: () => {
      const weight = Number(form.weight);
      const volume = Number(form.volume);
      if (form.pieces < 1 || !Number.isFinite(weight) || weight <= 0) {
        throw new Error('请填写实收件数和有效的实收重量');
      }
      if (!Number.isFinite(volume) || volume < 0) {
        throw new Error('实收体积不能为负数');
      }
      return recordReceipt(waybill!.id, waybill!.originBranchId, {
        receivedPieces: form.pieces,
        receivedWeightKg: weight,
        receivedVolumeM3: volume,
        note: form.note.trim() || undefined,
      });
    },
    onSuccess: (result) => {
      Toast.show({ content: '收货登记成功', position: 'bottom' });
      setReceipt(result);
    },
    onError: (error: Error) => Toast.show({ content: error.message, position: 'bottom' }),
  });

  const reset = () => {
    setKeyword('');
    setWaybill(null);
    setLookupError(null);
    setForm(emptyForm);
    setReceipt(null);
    locateMutation.reset();
    receiveMutation.reset();
  };

  if (receipt) {
    return (
      <div className="mobile-stack">
        <Card className="mobile-success-card">
          <CheckCircleOutline className="mobile-success-icon" />
          <h2>收货登记完成</h2>
          <p className="ltl-code">{receipt.waybill.waybillNo}</p>
          <div className="mobile-summary">
            <div>
              <span>实收件数</span>
              <strong>{receipt.receipt.receivedPieces} 件</strong>
              <small>{differenceText(receipt.pieceDifference, '件')}</small>
            </div>
            <div>
              <span>实收重量</span>
              <strong>{receipt.receipt.receivedWeightKg.toFixed(2)} kg</strong>
              <small>{differenceText(receipt.weightDifferenceKg, 'kg')}</small>
            </div>
            <div>
              <span>实收体积</span>
              <strong>{receipt.receipt.receivedVolumeM3.toFixed(3)} m³</strong>
              <small>{differenceText(receipt.volumeDifferenceM3, 'm³')}</small>
            </div>
          </div>
          <Button block color="primary" onClick={reset}>
            继续登记下一票
          </Button>
        </Card>
      </div>
    );
  }

  return (
    <div className="mobile-stack">
      <Card title="1. 定位运单">
        <MobileWaybillSearch
          value={keyword}
          onChange={(value) => {
            setKeyword(value);
            setWaybill(null);
            setLookupError(null);
          }}
          onSearch={(waybillNo) => locateMutation.mutate(waybillNo)}
          loading={locateMutation.isPending}
          scanTitle="扫描待收货运单"
        />
      </Card>

      {locateMutation.isPending && (
        <Card>
          <Skeleton.Title animated />
          <Skeleton.Paragraph lineCount={3} animated />
        </Card>
      )}

      {lookupError && <ErrorBlock status="empty" title="无法继续登记" description={lookupError} />}

      {waybill && (
        <>
          <Card title={`2. 核对申报值 · ${waybill.waybillNo}`}>
            <div className="mobile-metrics">
              <div>
                <span>申报件数</span>
                <strong>{declaredPieces} 件</strong>
              </div>
              <div>
                <span>申报重量</span>
                <strong>{declaredWeight.toFixed(2)} kg</strong>
              </div>
              <div>
                <span>申报体积</span>
                <strong>{declaredVolume.toFixed(3)} m³</strong>
              </div>
            </div>
            <Button
              block
              fill="outline"
              className="mobile-copy-declared"
              onClick={() =>
                setForm({
                  ...form,
                  pieces: declaredPieces,
                  weight: String(declaredWeight),
                  volume: String(declaredVolume),
                })
              }
            >
              按申报值带入
            </Button>
          </Card>

          <Card title="3. 录入实收">
            <div className="mobile-form-field">
              <label>实收件数</label>
              <Stepper
                min={0}
                max={9999}
                digits={0}
                value={form.pieces}
                onChange={(value) => setForm({ ...form, pieces: value })}
              />
            </div>
            <div className="mobile-form-field">
              <label htmlFor="mobile-receive-weight">实收重量（kg）</label>
              <Input
                id="mobile-receive-weight"
                type="number"
                inputMode="decimal"
                placeholder="例如 12.5"
                value={form.weight}
                onChange={(value) => setForm({ ...form, weight: value })}
                clearable
              />
            </div>
            <div className="mobile-form-field">
              <label htmlFor="mobile-receive-volume">实收体积（m³）</label>
              <Input
                id="mobile-receive-volume"
                type="number"
                inputMode="decimal"
                placeholder="例如 0.120"
                value={form.volume}
                onChange={(value) => setForm({ ...form, volume: value })}
                clearable
              />
            </div>
            <div className="mobile-form-field">
              <label htmlFor="mobile-receive-note">备注（可选）</label>
              <Input
                id="mobile-receive-note"
                placeholder="破损、短少等情况说明"
                value={form.note}
                onChange={(value) => setForm({ ...form, note: value })}
                clearable
              />
            </div>
            <Button
              block
              color="primary"
              size="large"
              disabled={receiveMutation.isPending}
              loading={receiveMutation.isPending}
              onClick={() => receiveMutation.mutate()}
            >
              提交收货登记
            </Button>
          </Card>
        </>
      )}
    </div>
  );
}
