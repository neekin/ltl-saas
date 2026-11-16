import { createBrowserRouter } from 'react-router-dom';
import { BasicLayout } from '../layouts/BasicLayout';
import { HomePage } from '../../modules/dashboard/pages/HomePage';
import { SystemStatusPage } from '../../modules/system/pages/SystemStatusPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <BasicLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'system/status', element: <SystemStatusPage /> },
    ],
  },
]);
