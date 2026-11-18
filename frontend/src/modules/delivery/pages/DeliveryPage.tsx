import { CameraOutlined, CheckCircleFilled } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, Input, Row, Select, Space, Typography, Upload } from 'antd';
import type { RcFile } from 'antd/es/upload';
import { useState } from 'react';
import { uploadFile } from '../../../shared/api/files';
import { PageHeader } from '../../../shared/components';
import { completeDelivery, createDelivery, rejectDelivery } from '../api/deliveries';

interface DeliveryOrder {
  id: string;
  waybillId: string;
  branchId: string;
  deliveryType: string;
  status: string;
  driverId: string | null;
  receiverName: string | null;
  completedAt: string | null;
  createdAt: string;
}

/** 交付确认页（P6 前端）：创建交付单/签收（含签收凭证照片）/拒收。 */
export function DeliveryPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [order, setOrder] = useState<DeliveryOrder | null>(null);
  const [receiverName, setReceiverName] = useState('');
  const [photoFileId, setPhotoFileId] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);

  const createMutation = useMutation({
    mutationFn: (values: {
      waybillId: string;
      branchId: string;
      deliveryType: string;
      driverId?: string;
    }) => createDelivery(values),
    onSuccess: (result) => {
      message.success('交付单已创建');
      setOrder(result);
      setReceiverName('');
      setPhotoFileId(null);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const completeMutation = useMutation({
    mutationFn: () => completeDelivery(order!.id, receiverName, photoFileId ?? undefined),
    onSuccess: () => {
      message.success('签收完成（凭证已随单保存）');
      setOrder(null);
      setReceiverName('');
      setPhotoFileId(null);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const rejectMutation = useMutation({
    mutationFn: () => rejectDelivery(order!.id),
    onSuccess: () => {
      message.success('已拒收');
      setOrder(null);
      setReceiverName('');
      setPhotoFileId(null);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const beforeUpload = async (file: RcFile) => {
    const isImage = file.type.startsWith('image/');
    if (!isImage) {
      message.error('签收凭证仅支持图片');
      return Upload.LIST_IGNORE;
    }
    setUploading(true);
    try {
      const uploaded = await uploadFile(file);
      setPhotoFileId(uploaded.id);
      message.success('签收凭证已上传');
    } catch (e) {
      message.error(e instanceof Error ? e.message : '凭证上传失败');
    } finally {
      setUploading(false);
    }
    // 手动管理上传，阻止 antd 默认上传行为
    return Upload.LIST_IGNORE;
  };

  return (
    <div>
      <PageHeader
        title="交付确认"
        description="创建交付单，登记签收或拒收结果，并可上传签收凭证照片。"
      />

      <Card title="创建交付单" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Row gutter={[16, 0]}>
            <Col xs={24} md={8}>
              <Form.Item name="waybillId" label="运单 ID" rules={[{ required: true }]}>
                <Input placeholder="输入运单 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="branchId" label="交付网点 ID" rules={[{ required: true }]}>
                <Input placeholder="输入网点 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={4}>
              <Form.Item
                name="deliveryType"
                label="类型"
                rules={[{ required: true }]}
                initialValue="SelfPickup"
              >
                <Select
                  options={[
                    { value: 'SelfPickup', label: '自提' },
                    { value: 'Delivery', label: '配送' },
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={4}>
              <Form.Item label=" ">
                <Button type="primary" htmlType="submit" block loading={createMutation.isPending}>
                  创建
                </Button>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {order && (
        <Card
          title={`交付单（${order.deliveryType === 'SelfPickup' ? '自提' : '配送'}）`}
          extra={<Typography.Text type="secondary">状态：{order.status}</Typography.Text>}
        >
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Space wrap>
              <Typography.Text>签收人：</Typography.Text>
              <Input
                placeholder="签收人姓名"
                style={{ width: 220 }}
                value={receiverName}
                onChange={(e) => setReceiverName(e.target.value)}
              />
            </Space>
            <div>
              <div style={{ marginBottom: 8 }}>
                <Typography.Text type="secondary">签收凭证（可选）：</Typography.Text>
              </div>
              <Space wrap align="center">
                <Upload
                  beforeUpload={beforeUpload}
                  showUploadList={false}
                  accept="image/*"
                  maxCount={1}
                >
                  <Button icon={<CameraOutlined />} loading={uploading}>
                    {uploading ? '上传中…' : '上传凭证照片'}
                  </Button>
                </Upload>
                {photoFileId && (
                  <Typography.Text type="success">
                    <CheckCircleFilled /> 凭证已上传
                  </Typography.Text>
                )}
              </Space>
            </div>
            <Space wrap>
              <Button
                type="primary"
                disabled={!receiverName.trim() || uploading}
                loading={completeMutation.isPending}
                onClick={() => completeMutation.mutate()}
              >
                签收
              </Button>
              <Button danger onClick={() => rejectMutation.mutate()}>
                拒收
              </Button>
            </Space>
          </Space>
        </Card>
      )}
    </div>
  );
}
