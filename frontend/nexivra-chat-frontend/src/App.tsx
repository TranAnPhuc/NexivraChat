import { useState } from 'react';
import { LoginView } from './views/LoginView';
import { ChatView } from './views/ChatView';

function App() {
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
    <>
      {token && username ? (
        <ChatView
          username={username}
          token={token}
          onLogout={handleLogout}
        />
      ) : (
        <LoginView onLoginSuccess={handleLoginSuccess} />
      )}
    </>
  );
}

export default App;
