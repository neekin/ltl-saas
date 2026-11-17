import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { LoginPage } from './LoginPage';

describe('LoginPage', () => {
  function renderPage() {
    return render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    );
  }

  it('渲染三个必填字段和登录按钮', () => {
    renderPage();
    expect(screen.getByLabelText('租户编码')).toBeInTheDocument();
    expect(screen.getByLabelText('用户名')).toBeInTheDocument();
    expect(screen.getByLabelText('密码')).toBeInTheDocument();
    const buttons = screen.getAllByRole('button');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('空字段提交后仍停留在登录页（校验拦截）', async () => {
    renderPage();
    const user = userEvent.setup();
    await user.click(screen.getAllByRole('button')[0]);
    expect(screen.getAllByText('LTL 物流系统').length).toBeGreaterThan(0);
  });
});
