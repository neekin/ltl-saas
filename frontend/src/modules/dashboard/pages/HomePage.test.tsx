import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { HomePage } from './HomePage';

describe('HomePage', () => {
  it('渲染概览页标题与占位统计', () => {
    render(
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>,
    );

    expect(screen.getByText('工作台')).toBeInTheDocument();
    expect(screen.getByText('今日运单')).toBeInTheDocument();
  });
});
