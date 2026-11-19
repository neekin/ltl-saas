import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, Modal, Select, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { listTrips, createTrip, type Trip } from '../api/trips';
import { listVehicles, listDrivers } from '../api/fleet';
import { http } from '../../../shared/api/http';

interface BranchOption {
  id: string;
  code: string;
  name: string;
}

/** 运输批次列表页（P5 前端）：批次创建/列表、配载量一览。 */
export function TripListPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm] = Form.useForm<{
    originBranchId: string;
    destBranchId: string;
    vehicleId: string;
    driverId?: string;
    note?: string;
  }>();

  const listQuery = useQuery({ queryKey: ['trips', page], queryFn: () => listTrips(page, 10) });
  const vehiclesQuery = useQuery({
    queryKey: ['vehicle-options'],
    queryFn: () => listVehicles(1, 100),
    enabled: createOpen,
  });
  const driversQuery = useQuery({
    queryKey: ['driver-options'],
    queryFn: () => listDrivers(1, 100),
    enabled: createOpen,
  });
  const branchesQuery = useQuery({
    queryKey: ['branch-options'],
    queryFn: async () => {
      const response = await http.get<{ items: BranchOption[] }>('/organization/branches', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
    enabled: createOpen,
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['trips'] });

  const createMutation = useMutation({
    mutationFn: (values: {
      originBranchId: string;
      destBranchId: string;
      vehicleId: string;
      driverId?: string;
      note?: string;
    }) =>
      createTrip({
        originBranchId: values.originBranchId,
        destBranchId: values.destBranchId,
        vehicleId: values.vehicleId,
        driverId: values.driverId || null,
        note: values.note || undefined,
      }),
    onSuccess: (trip) => {
      message.success('批次已创建');
      setCreateOpen(false);
      createForm.resetFields();
      invalidate();
      navigate(`/transport/trips/${trip.id}`);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const branchOptions = (branchesQuery.data ?? []).map((b) => ({
    value: b.id,
    label: `${b.code} ${b.name}`,
  }));
  const vehicleOptions = (vehiclesQuery.data?.items ?? [])
    .filter((v) => v.status === 'Active')
    .map((v) => ({ value: v.id, label: v.plateNo }));
  const driverOptions = (driversQuery.data?.items ?? [])
    .filter((d) => d.status === 'Active')
    .map((d) => ({ value: d.id, label: d.name }));

  const columns: ColumnsType<Trip> = [
    {
      title: '批次号',
      dataIndex: 'tripNo',
      key: 'tripNo',
      width: 180,
      render: (no: string) => <span className="ltl-code">{no}</span>,
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: (status: string) => <StatusTag status={status} category="trip" />,
    },
    {
      title: '配载运单',
      key: 'waybills',
      width: 130,
      render: (_, record) => `${record.waybills.filter((w) => w.active).length} 票在车`,
    },
    {
      title: '开单时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (v: string) => (
        <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text>
      ),
    },
    {
      title: '操作',
      key: 'actions',
      width: 130,
      fixed: 'right',
      render: (_, record) => (
        <Button type="link" size="small" onClick={() => navigate(`/transport/trips/${record.id}`)}>
          配载工作台
        </Button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="运输批次"
        description="管理发车批次与配载运单，进入工作台完成装车、发车与到达登记。"
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            新建批次
          </Button>
        }
      />
      <Card styles={{ body: { padding: 0 } }}>
        <Table<Trip>
          rowKey="id"
          size="middle"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          columns={columns}
          scroll={{ x: 800 }}
          locale={{ emptyText: <EmptyState description="暂无运输批次" /> }}
          pagination={{
            current: page,
            pageSize: 10,
            total: listQuery.data?.total ?? 0,
            onChange: setPage,
            showTotal: (total) => `共 ${total} 条`,
            showSizeChanger: false,
          }}
        />
      </Card>

      <Modal
        title="新建运输批次"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => createForm.submit()}
        confirmLoading={createMutation.isPending}
        destroyOnHidden
      >
        <Form
          form={createForm}
          layout="vertical"
          onFinish={(values) => createMutation.mutate(values)}
        >
          <Form.Item
            name="originBranchId"
            label="起点网点"
            rules={[{ required: true, message: '必选' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="选择起点网点"
              options={branchOptions}
            />
          </Form.Item>
          <Form.Item
            name="destBranchId"
            label="目的网点"
            rules={[{ required: true, message: '必选' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="选择目的网点"
              options={branchOptions}
            />
          </Form.Item>
          <Form.Item name="vehicleId" label="车辆" rules={[{ required: true, message: '必选' }]}>
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="选择车辆"
              options={vehicleOptions}
            />
          </Form.Item>
          <Form.Item name="driverId" label="司机（可选）">
            <Select
              showSearch
              optionFilterProp="label"
              allowClear
              placeholder="选择司机"
              options={driverOptions}
            />
          </Form.Item>
          <Form.Item name="note" label="备注">
            <Input.TextArea rows={2} placeholder="可选" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
