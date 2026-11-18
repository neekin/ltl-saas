import { Navigate, createBrowserRouter } from 'react-router-dom';
import { BasicLayout } from '../layouts/BasicLayout';
import { DashboardPage } from '../../modules/reports/pages/DashboardPage';
import { BranchListPage } from '../../modules/organization/pages/BranchListPage';
import { UserListPage } from '../../modules/organization/pages/UserListPage';
import { RoleListPage } from '../../modules/organization/pages/RoleListPage';
import { DimensionReportPage } from '../../modules/reports/pages/DimensionReportPage';
import { TenantUsagePage } from '../../modules/reports/pages/TenantUsagePage';
import { SystemStatusPage } from '../../modules/system/pages/SystemStatusPage';
import { LoginPage } from '../../modules/auth/pages/LoginPage';
import { CustomerQueryPage } from '../../modules/waybills/pages/CustomerQueryPage';
import { RequireAuth } from './RequireAuth';
import { MobileLayout } from '../mobile/MobileLayout';
import { MobileHomePage } from '../../modules/mobile/pages/MobileHomePage';
import { MobileScanPage } from '../../modules/mobile/pages/MobileScanPage';
import { MobileReceivePage } from '../../modules/mobile/pages/MobileReceivePage';
import { MobileLoadPage } from '../../modules/mobile/pages/MobileLoadPage';
import { MobileArrivalPage } from '../../modules/mobile/pages/MobileArrivalPage';
import { CustomerListPage } from '../../modules/customers/pages/CustomerListPage';
import { RouteListPage } from '../../modules/routing/pages/RouteListPage';
import { RouteEditPage } from '../../modules/routing/pages/RouteEditPage';
import { RuleSetListPage } from '../../modules/pricing/pages/RuleSetListPage';
import { RuleSetVersionsPage } from '../../modules/pricing/pages/RuleSetVersionsPage';
import { TrialPage } from '../../modules/pricing/pages/TrialPage';
import { WaybillListPage } from '../../modules/waybills/pages/WaybillListPage';
import { WaybillCreatePage } from '../../modules/waybills/pages/WaybillCreatePage';
import { WaybillDetailPage } from '../../modules/waybills/pages/WaybillDetailPage';
import { WaybillLabelPage } from '../../modules/waybills/pages/WaybillLabelPage';
import { VehicleListPage } from '../../modules/transport/pages/VehicleListPage';
import { DriverListPage } from '../../modules/transport/pages/DriverListPage';
import { TripListPage } from '../../modules/transport/pages/TripListPage';
import { TripDetailPage } from '../../modules/transport/pages/TripDetailPage';
import { TransferListPage } from '../../modules/warehouse/pages/TransferListPage';
import { TransferDetailPage } from '../../modules/warehouse/pages/TransferDetailPage';
import { ArrivalPage } from '../../modules/arrivals/pages/ArrivalPage';
import { DeliveryPage } from '../../modules/delivery/pages/DeliveryPage';
import { FinancePage } from '../../modules/finance/pages/FinancePage';
import { CodWorkbenchPage } from '../../modules/finance/pages/CodWorkbenchPage';
import { CodManagementPage } from '../../modules/finance/pages/CodManagementPage';
import { ArrivalDifferencePage } from '../../modules/arrivals/pages/ArrivalDifferencePage';
import { ExceptionWorkbenchPage } from '../../modules/arrivals/pages/ExceptionWorkbenchPage';
import { PickupDeliveryPage } from '../../modules/arrivals/pages/PickupDeliveryPage';
import { StatementPage } from '../../modules/finance/pages/StatementPage';
import { PaymentAllocationPage } from '../../modules/finance/pages/PaymentAllocationPage';
import { SortingTasksPage } from '../../modules/warehouse/pages/SortingTasksPage';

/** 路由表：登录页匿名；业务页面统一经 RequireAuth 守卫（会话由 Zustand 持久化）。 */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/track', element: <CustomerQueryPage /> },
  // P10：独立移动作业应用，与 PC 后台 (/) 路由与布局隔离。
  {
    path: '/m',
    element: <RequireAuth />,
    children: [
      {
        path: '/m',
        element: <MobileLayout />,
        children: [
          { index: true, element: <MobileHomePage /> },
          { path: 'scan', element: <MobileScanPage /> },
          { path: 'receive', element: <MobileReceivePage /> },
          { path: 'load', element: <MobileLoadPage /> },
          { path: 'arrival', element: <MobileArrivalPage /> },
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
          { index: true, element: <DashboardPage /> },
          { path: 'reports/dimension', element: <DimensionReportPage /> },
          { path: 'reports/usage', element: <TenantUsagePage /> },
          { path: 'system/status', element: <SystemStatusPage /> },

          // P2：经营基础资料
          { path: 'customers', element: <CustomerListPage /> },
          { path: 'organization/branches', element: <BranchListPage /> },
          { path: 'organization/users', element: <UserListPage /> },
          { path: 'organization/roles', element: <RoleListPage /> },
          { path: 'customers/:id', element: <CustomerListPage /> },
          { path: 'routes', element: <RouteListPage /> },
          { path: 'routes/:id', element: <RouteEditPage /> },

          // P3：计费引擎
          { path: 'pricing/rule-sets', element: <RuleSetListPage /> },
          { path: 'pricing/rule-sets/:id/versions', element: <RuleSetVersionsPage /> },
          { path: 'pricing/trial', element: <TrialPage /> },

          // P4：运单与始发仓
          { path: 'waybills', element: <WaybillListPage /> },
          { path: 'waybills/create', element: <WaybillCreatePage /> },
          { path: 'waybills/:id', element: <WaybillDetailPage /> },
          { path: 'waybills/:id/label', element: <WaybillLabelPage /> },
          { path: 'warehouse/sorting', element: <SortingTasksPage /> },

          // P5：调拨与运输
          { path: 'transport/vehicles', element: <VehicleListPage /> },
          { path: 'transport/drivers', element: <DriverListPage /> },
          { path: 'transport/trips', element: <TripListPage /> },
          { path: 'transport/trips/:id', element: <TripDetailPage /> },
          { path: 'transfers', element: <TransferListPage /> },
          { path: 'transfers/:id', element: <TransferDetailPage /> },
          { path: 'arrivals', element: <ArrivalPage /> },
          { path: 'arrivals/differences', element: <ArrivalDifferencePage /> },
          { path: 'arrivals/pickup', element: <PickupDeliveryPage /> },
          { path: 'arrivals/exceptions', element: <ExceptionWorkbenchPage /> },
          { path: 'finance/allocations', element: <PaymentAllocationPage /> },
          { path: 'finance/statements', element: <StatementPage /> },
          { path: 'finance/cod/manage', element: <CodManagementPage /> },
          { path: 'deliveries', element: <DeliveryPage /> },
          { path: 'finance', element: <FinancePage /> },
          { path: 'finance/cod', element: <CodWorkbenchPage /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
