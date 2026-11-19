import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from './authStore';

describe('authStore.currentBranch（工作网点上下文）', () => {
  beforeEach(() => {
    useAuthStore.getState().clear();
  });

  it('默认无当前网点，可设置与切换', () => {
    expect(useAuthStore.getState().currentBranch).toBeNull();

    const gz = { id: 'gz-1', code: 'GZ01', name: '广州' };
    const wh = { id: 'wh-1', code: 'WH02', name: '武汉' };
    useAuthStore.getState().setCurrentBranch(gz);
    expect(useAuthStore.getState().currentBranch).toEqual(gz);

    useAuthStore.getState().setCurrentBranch(wh);
    expect(useAuthStore.getState().currentBranch).toEqual(wh);
  });

  it('退出登录清空当前网点', () => {
    useAuthStore.getState().setCurrentBranch({ id: 'gz-1', code: 'GZ01', name: '广州' });
    useAuthStore.getState().clear();
    expect(useAuthStore.getState().currentBranch).toBeNull();
  });
});
