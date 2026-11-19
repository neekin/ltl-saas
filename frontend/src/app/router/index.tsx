import { type ComponentType, Suspense, lazy } from 'react';
import { Navigate, createBrowserRouter } from 'react-router-dom';
import { Spin } from 'antd';
import { BasicLayout } from '../layouts/BasicLayout';
import { RequireAuth } from './RequireAuth';

function lazyNamed<T extends Record<string, unknown>, K extends keyof T>(
  loader: () => Promise<T>,
  exportName: K,
) {
  const LazyComp = lazy(async () => {
    const mod = await loader();
    return { default: mod[exportName] as ComponentType };
  });

  return (
    <Suspense
      fallback={
        <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
          <Spin />
        </div>
      }
    >
      <LazyComp />
    </Suspense>
  );
}

/** 路由表：登录页与查件页匿名；业务页面统一经 RequireAuth 守卫 + React.lazy 按路由分包。 */
export const router = createBrowserRouter([
  {
    path: '/login',
    element: lazyNamed(() => import('../../modules/auth/pages/LoginPage'), 'LoginPage'),
  },
  {
    path: '/track',
    element: lazyNamed(
      () => import('../../modules/waybills/pages/CustomerQueryPage'),
      'CustomerQueryPage',
    ),
  },
  // P10：独立移动作业应用，与 PC 后台 (/) 路由、布局及依赖（antd-mobile / @zxing）完全按需分包隔离。
  {
    path: '/m',
    element: <RequireAuth />,
    children: [
      {
        path: '/m',
        element: lazyNamed(() => import('../mobile/MobileLayout'), 'MobileLayout'),
        children: [
          {
            index: true,
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileHomePage'),
              'MobileHomePage',
            ),
          },
          {
            path: 'scan',
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileScanPage'),
              'MobileScanPage',
            ),
          },
          {
            path: 'receive',
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileReceivePage'),
              'MobileReceivePage',
            ),
          },
          {
            path: 'load',
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileLoadPage'),
              'MobileLoadPage',
            ),
          },
          {
            path: 'arrival',
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileArrivalPage'),
              'MobileArrivalPage',
            ),
          },
          {
            path: 'gps',
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileGpsPage'),
              'MobileGpsPage',
            ),
          },
          {
            path: 'dispatch',
            element: lazyNamed(
              () => import('../../modules/mobile/pages/MobileDispatchPage'),
              'MobileDispatchPage',
            ),
          },
        ],
      },
    ],
  },
  {
    path: '/',
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <BasicLayout />,
        children: [
          {
            index: true,
            element: lazyNamed(
              () => import('../../modules/reports/pages/DashboardPage'),
              'DashboardPage',
            ),
          },
          {
            path: 'reports/dimension',
            element: lazyNamed(
              () => import('../../modules/reports/pages/DimensionReportPage'),
              'DimensionReportPage',
            ),
          },
          {
            path: 'reports/usage',
            element: lazyNamed(
              () => import('../../modules/reports/pages/TenantUsagePage'),
              'TenantUsagePage',
            ),
          },
          {
            path: 'system/status',
            element: lazyNamed(
              () => import('../../modules/system/pages/SystemStatusPage'),
              'SystemStatusPage',
            ),
          },
          {
            path: 'system/notifications',
            element: lazyNamed(
              () => import('../../modules/system/pages/NotificationListPage'),
              'NotificationListPage',
            ),
          },

          // P2：经营基础资料
          {
            path: 'customers',
            element: lazyNamed(
              () => import('../../modules/customers/pages/CustomerListPage'),
              'CustomerListPage',
            ),
          },
          {
            path: 'organization/branches',
            element: lazyNamed(
              () => import('../../modules/organization/pages/BranchListPage'),
              'BranchListPage',
            ),
          },
          {
            path: 'organization/users',
            element: lazyNamed(
              () => import('../../modules/organization/pages/UserListPage'),
              'UserListPage',
            ),
          },
          {
            path: 'organization/roles',
            element: lazyNamed(
              () => import('../../modules/organization/pages/RoleListPage'),
              'RoleListPage',
            ),
          },
          {
            path: 'customers/:id',
            element: lazyNamed(
              () => import('../../modules/customers/pages/CustomerListPage'),
              'CustomerListPage',
            ),
          },
          {
            path: 'routes',
            element: lazyNamed(
              () => import('../../modules/routing/pages/RouteListPage'),
              'RouteListPage',
            ),
          },
          {
            path: 'routes/:id',
            element: lazyNamed(
              () => import('../../modules/routing/pages/RouteEditPage'),
              'RouteEditPage',
            ),
          },

          // P3：计费引擎
          {
            path: 'pricing/rule-sets',
            element: lazyNamed(
              () => import('../../modules/pricing/pages/RuleSetListPage'),
              'RuleSetListPage',
            ),
          },
          {
            path: 'pricing/rule-sets/:id/versions',
            element: lazyNamed(
              () => import('../../modules/pricing/pages/RuleSetVersionsPage'),
              'RuleSetVersionsPage',
            ),
          },
          {
            path: 'pricing/trial',
            element: lazyNamed(() => import('../../modules/pricing/pages/TrialPage'), 'TrialPage'),
          },

          // P4：运单与始发仓
          {
            path: 'waybills',
            element: lazyNamed(
              () => import('../../modules/waybills/pages/WaybillListPage'),
              'WaybillListPage',
            ),
          },
          {
            path: 'waybills/create',
            element: lazyNamed(
              () => import('../../modules/waybills/pages/WaybillCreatePage'),
              'WaybillCreatePage',
            ),
          },
          {
            path: 'waybills/:id',
            element: lazyNamed(
              () => import('../../modules/waybills/pages/WaybillDetailPage'),
              'WaybillDetailPage',
            ),
          },
          {
            path: 'waybills/:id/label',
            element: lazyNamed(
              () => import('../../modules/waybills/pages/WaybillLabelPage'),
              'WaybillLabelPage',
            ),
          },
          {
            path: 'warehouse/sorting',
            element: lazyNamed(
              () => import('../../modules/warehouse/pages/SortingTasksPage'),
              'SortingTasksPage',
            ),
          },

          // P5：调拨与运输
          {
            path: 'transport/vehicles',
            element: lazyNamed(
              () => import('../../modules/transport/pages/VehicleListPage'),
              'VehicleListPage',
            ),
          },
          {
            path: 'transport/drivers',
            element: lazyNamed(
              () => import('../../modules/transport/pages/DriverListPage'),
              'DriverListPage',
            ),
          },
          {
            path: 'transport/trips',
            element: lazyNamed(
              () => import('../../modules/transport/pages/TripListPage'),
              'TripListPage',
            ),
          },
          {
            path: 'transport/trips/:id',
            element: lazyNamed(
              () => import('../../modules/transport/pages/TripDetailPage'),
              'TripDetailPage',
            ),
          },
          {
            path: 'transfers',
            element: lazyNamed(
              () => import('../../modules/warehouse/pages/TransferListPage'),
              'TransferListPage',
            ),
          },
          {
            path: 'transfers/:id',
            element: lazyNamed(
              () => import('../../modules/warehouse/pages/TransferDetailPage'),
              'TransferDetailPage',
            ),
          },
          {
            path: 'arrivals',
            element: lazyNamed(
              () => import('../../modules/arrivals/pages/ArrivalPage'),
              'ArrivalPage',
            ),
          },
          {
            path: 'arrivals/differences',
            element: lazyNamed(
              () => import('../../modules/arrivals/pages/ArrivalDifferencePage'),
              'ArrivalDifferencePage',
            ),
          },
          {
            path: 'arrivals/pickup',
            element: lazyNamed(
              () => import('../../modules/arrivals/pages/PickupDeliveryPage'),
              'PickupDeliveryPage',
            ),
          },
          {
            path: 'arrivals/exceptions',
            element: lazyNamed(
              () => import('../../modules/arrivals/pages/ExceptionWorkbenchPage'),
              'ExceptionWorkbenchPage',
            ),
          },
          {
            path: 'finance/allocations',
            element: lazyNamed(
              () => import('../../modules/finance/pages/PaymentAllocationPage'),
              'PaymentAllocationPage',
            ),
          },
          {
            path: 'finance/statements',
            element: lazyNamed(
              () => import('../../modules/finance/pages/StatementPage'),
              'StatementPage',
            ),
          },
          {
            path: 'finance/cod/manage',
            element: lazyNamed(
              () => import('../../modules/finance/pages/CodManagementPage'),
              'CodManagementPage',
            ),
          },
          {
            path: 'deliveries',
            element: lazyNamed(
              () => import('../../modules/delivery/pages/DeliveryPage'),
              'DeliveryPage',
            ),
          },
          {
            path: 'finance',
            element: lazyNamed(
              () => import('../../modules/finance/pages/FinancePage'),
              'FinancePage',
            ),
          },
          {
            path: 'finance/branch-summary',
            element: lazyNamed(
              () => import('../../modules/finance/pages/BranchPaymentSummaryPage'),
              'BranchPaymentSummaryPage',
            ),
          },
          {
            path: 'finance/cod',
            element: lazyNamed(
              () => import('../../modules/finance/pages/CodWorkbenchPage'),
              'CodWorkbenchPage',
            ),
          },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
