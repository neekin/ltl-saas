import { Button, Card } from 'antd-mobile';
import {
  InformationCircleOutline,
  ReceiptOutline,
  RightOutline,
  ScanCodeOutline,
  TruckOutline,
  UnorderedListOutline,
} from 'antd-mobile-icons';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../auth/store/authStore';

const tasks = [
  { key: '/m/receive', title: '收货登记', desc: '核对实收件数、重量与体积', icon: <ReceiptOutline /> },
  { key: '/m/load', title: '装车配载', desc: '选择批次，扫描运单装车', icon: <TruckOutline /> },
  { key: '/m/arrival', title: '到货核验', desc: '按批次登记实收与破损', icon: <UnorderedListOutline /> },
];

/** 移动作业台（P10）：先给最高频的扫码入口，再给三项现场任务。 */
export function MobileHomePage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);

  return (
    <div className="mobile-stack">
      <Card className="mobile-hero">
        <span className="mobile-hero__eyebrow">今天从哪里开始</span>
        <h1>{user?.displayName ?? user?.username ?? '作业员'}，你好</h1>
        <p>扫描运单条码最快捷；不方便使用摄像头时，也可以手动输入运单号。</p>
        <Button block color="primary" size="large" onClick={() => navigate('/m/scan')}>
          <span className="mobile-inline-icon">
            <ScanCodeOutline /> 扫码查询运单
          </span>
        </Button>
      </Card>

      <section className="mobile-section" aria-labelledby="mobile-task-heading">
        <h2 id="mobile-task-heading">现场作业</h2>
        <div className="mobile-action-list">
          {tasks.map((task) => (
            <button key={task.key} type="button" onClick={() => navigate(task.key)}>
              <span className="mobile-action-list__icon">{task.icon}</span>
              <span className="mobile-action-list__text">
                <strong>{task.title}</strong>
                <small>{task.desc}</small>
              </span>
              <RightOutline className="mobile-action-list__arrow" />
            </button>
          ))}
        </div>
      </section>

      <div className="mobile-help">
        <InformationCircleOutline />
        <span>底部导航可随时切换任务；所有数据操作仍由服务端校验权限。</span>
      </div>
    </div>
  );
}
