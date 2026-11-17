import { Navigate, createBrowserRouter } from 'react-router-dom';
import { BasicLayout } from '../layouts/BasicLayout';
import { HomePage } from '../../modules/dashboard/pages/HomePage';
import { SystemStatusPage } from '../../modules/system/pages/SystemStatusPage';
import { LoginPage } from '../../modules/auth/pages/LoginPage';
import { RequireAuth } from './RequireAuth';
import { CustomerListPage } from '../../modules/customers/pages/CustomerListPage';
import { RouteListPage } from '../../modules/routing/pages/RouteListPage';
import { RouteEditPage } from '../../modules/routing/pages/RouteEditPage';
import { RuleSetListPage } from '../../modules/pricing/pages/RuleSetListPage';
import { RuleSetVersionsPage } from '../../modules/pricing/pages/RuleSetVersionsPage';
import { TrialPage } from '../../modules/pricing/pages/TrialPage';
import { WaybillListPage } from '../../modules/waybills/pages/WaybillListPage';
import { WaybillCreatePage } from '../../modules/waybills/pages/WaybillCreatePage';
import { WaybillDetailPage } from '../../modules/waybills/pages/WaybillDetailPage';
import { VehicleListPage } from '../../modules/transport/pages/VehicleListPage';
import { DriverListPage } from '../../modules/transport/pages/DriverListPage';
import { TripListPage } from '../../modules/transport/pages/TripListPage';
import { TripDetailPage } from '../../modules/transport/pages/TripDetailPage';
import { TransferListPage } from '../../modules/warehouse/pages/TransferListPage';
import { TransferDetailPage } from '../../modules/warehouse/pages/TransferDetailPage';
import { SortingTasksPage } from '../../modules/warehouse/pages/SortingTasksPage';

/** 路由表：登录页匿名；业务页面统一经 RequireAuth 守卫（会话由 Zustand 持久化）。 */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <BasicLayout />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'system/status', element: <SystemStatusPage /> },

          // P2：经营基础资料
          { path: 'customers', element: <CustomerListPage /> },
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
          { path: 'warehouse/sorting', element: <SortingTasksPage /> },

          // P5：调拨与运输
          { path: 'transport/vehicles', element: <VehicleListPage /> },
          { path: 'transport/drivers', element: <DriverListPage /> },
          { path: 'transport/trips', element: <TripListPage /> },
          { path: 'transport/trips/:id', element: <TripDetailPage /> },
          { path: 'transfers', element: <TransferListPage /> },
          { path: 'transfers/:id', element: <TransferDetailPage /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
