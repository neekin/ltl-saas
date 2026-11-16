import '@testing-library/jest-dom/vitest';

// jsdom 未实现 matchMedia，Ant Design 响应式组件（Layout/Menu 等）依赖它
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});
