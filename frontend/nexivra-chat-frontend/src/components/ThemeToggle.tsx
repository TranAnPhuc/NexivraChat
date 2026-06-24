import React from 'react';
import { Button } from 'antd';
import { BulbOutlined, BulbFilled } from '@ant-design/icons';
import { useTheme } from '../theme/ThemeContext';

export const ThemeToggle: React.FC = () => {
  const { theme, toggleTheme } = useTheme();
  return (
    <Button
      type="text"
      aria-label="Chuyển giao diện sáng/tối"
      icon={theme === 'dark' ? <BulbFilled /> : <BulbOutlined />}
      onClick={toggleTheme}
      style={{ color: 'var(--text-secondary)' }}
    />
  );
};
