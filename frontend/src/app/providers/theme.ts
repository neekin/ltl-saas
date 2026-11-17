import type { ThemeConfig } from 'antd';

/**
 * 全局视觉主题：深蓝专业配色。
 * 统一品牌色、语义色、圆角、字体，以及 Layout / Menu / Table 组件级 token。
 * 仅影响视觉呈现，不改变任何组件行为与业务规则（AGENTS.md §9.3）。
 */
export const appTheme: ThemeConfig = {
  token: {
    // 品牌与语义色
    colorPrimary: '#1D4FA0',
    colorInfo: '#1D4FA0',
    colorSuccess: '#2E8B57',
    colorWarning: '#C77700',
    colorError: '#C0392B',
    colorTextBase: '#1F2937',
    colorBgBase: '#FFFFFF',
    colorBgLayout: '#F5F7FA',
    colorLink: '#1D4FA0',

    // 圆角与字体
    borderRadius: 6,
    fontSize: 14,
    controlHeight: 32,
    fontFamily:
      "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', sans-serif",
  },
  components: {
    Layout: {
      bodyBg: '#F5F7FA',
      headerBg: '#FFFFFF',
      headerHeight: 56,
      siderBg: '#16233A',
      triggerBg: '#16233A',
    },
    Menu: {
      // 深色侧栏（menu theme="dark"）
      darkItemBg: '#16233A',
      darkSubMenuItemBg: '#111D33',
      darkItemSelectedBg: '#1D4FA0',
      darkItemColor: 'rgba(255, 255, 255, 0.68)',
      darkItemHoverBg: 'rgba(255, 255, 255, 0.08)',
      darkItemHoverColor: '#FFFFFF',
      darkItemSelectedColor: '#FFFFFF',
      darkGroupTitleColor: 'rgba(255, 255, 255, 0.42)',
      itemBorderRadius: 6,
      itemHeight: 40,
      itemMarginInline: 8,
    },
    Table: {
      headerBg: '#F7F9FC',
      headerColor: '#334155',
      headerSplitColor: '#E8EDF3',
      rowHoverBg: '#F0F5FC',
      borderColor: '#E8EDF3',
      cellPaddingBlock: 12,
      cellPaddingBlockMD: 10,
      cellPaddingBlockSM: 8,
    },
    Card: {
      borderRadiusLG: 8,
      headerFontSize: 15,
    },
    Button: {
      primaryShadow: 'none',
      defaultShadow: 'none',
      dangerShadow: 'none',
    },
    Tabs: {
      itemColor: '#5B6573',
      itemSelectedColor: '#1D4FA0',
      itemHoverColor: '#1D4FA0',
      inkBarColor: '#1D4FA0',
    },
  },
};
