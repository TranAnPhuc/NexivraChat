import { useState } from 'react';
import { ConfigProvider, theme as antdTheme } from 'antd';
import { LoginView } from './views/LoginView';
import { ChatView } from './views/ChatView';
import { ThemeProvider, useTheme } from './theme/ThemeContext';

function AppShell() {
  const { theme } = useTheme();
  const [token, setToken] = useState<string | null>(localStorage.getItem('token'));
  const [username, setUsername] = useState<string | null>(localStorage.getItem('username'));

  const handleLoginSuccess = (usr: string, tkn: string) => {
    setUsername(usr);
    setToken(tkn);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('username');
    setToken(null);
    setUsername(null);
  };

  return (
    <ConfigProvider
      theme={{
        algorithm: theme === 'dark' ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
        token: {
          colorPrimary: '#0D9488',
          borderRadius: 8,
          fontFamily: "'Inter', system-ui, sans-serif",
        },
      }}
    >
      {token && username ? (
        <ChatView username={username} token={token} onLogout={handleLogout} />
      ) : (
        <LoginView onLoginSuccess={handleLoginSuccess} />
      )}
    </ConfigProvider>
  );
}

function App() {
  return (
    <ThemeProvider>
      <AppShell />
    </ThemeProvider>
  );
}

export default App;
