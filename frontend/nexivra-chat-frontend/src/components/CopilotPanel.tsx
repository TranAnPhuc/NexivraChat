import React, { useState } from 'react';
import { Button, Input, Card, Divider } from 'antd';
import { RobotOutlined, BookOutlined, BulbOutlined, AlignLeftOutlined } from '@ant-design/icons';

interface CopilotPanelProps {
  onTriggerCommand: (command: string) => void;
  fullWidth?: boolean;
}

export const CopilotPanel: React.FC<CopilotPanelProps> = ({ onTriggerCommand, fullWidth = false }) => {
  const [term, setTerm] = useState('');

  const handleExplainTerm = () => {
    if (term.trim()) {
      onTriggerCommand(`@copilot Giải thích thuật ngữ "${term.trim()}" ngắn gọn trong 2 câu.`);
      setTerm('');
    }
  };

  return (
    <div style={{ width: fullWidth ? '100%' : '280px', boxSizing: 'border-box', backgroundColor: 'var(--bg-surface)', borderLeft: fullWidth ? 'none' : '1px solid var(--border)', padding: '16px', display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0, color: 'var(--text-primary)' }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
        <RobotOutlined style={{ color: 'var(--primary)', fontSize: '20px' }} />
        <span style={{ color: 'var(--text-primary)', fontWeight: 600, fontSize: '16px', fontFamily: "'Outfit', sans-serif" }}>
          Trợ lý AI
        </span>
      </div>

      <p style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.5', margin: 0 }}>
        Trợ lý AI đồng hành trong phòng chat. Bạn có thể tag @copilot trực tiếp hoặc dùng các nút nhanh dưới đây:
      </p>

      <Divider style={{ borderColor: 'var(--border)', margin: '12px 0' }} />

      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', flex: 1, minHeight: 0, overflowY: 'auto' }}>
        <Card size="small" style={{ backgroundColor: 'var(--bg-elevated)', borderColor: 'var(--border)', borderRadius: 8 }} styles={{ body: { padding: '12px' } }}>
          <div style={{ color: 'var(--text-primary)', fontSize: '13px', fontWeight: 600, marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <AlignLeftOutlined /> Tóm tắt phòng
          </div>
          <p style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            Tóm tắt nhanh các tin nhắn gần đây trong phòng.
          </p>
          <Button block size="small" onClick={() => onTriggerCommand('@copilot Tóm tắt ngắn gọn các cuộc trò chuyện gần đây trong phòng này.')}>
            Tóm tắt
          </Button>
        </Card>

        <Card size="small" style={{ backgroundColor: 'var(--bg-elevated)', borderColor: 'var(--border)', borderRadius: 8 }} styles={{ body: { padding: '12px' } }}>
          <div style={{ color: 'var(--text-primary)', fontSize: '13px', fontWeight: 600, marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <BulbOutlined /> Gợi ý chủ đề
          </div>
          <p style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            AI gợi ý các chủ đề thảo luận tiếp theo cho phòng.
          </p>
          <Button block size="small" onClick={() => onTriggerCommand('@copilot Hãy gợi ý 3 ý tưởng/chủ đề thảo luận tiếp theo cho phòng chat này.')}>
            Gợi ý
          </Button>
        </Card>

        <Card size="small" style={{ backgroundColor: 'var(--bg-elevated)', borderColor: 'var(--border)', borderRadius: 8 }} styles={{ body: { padding: '12px' } }}>
          <div style={{ color: 'var(--text-primary)', fontSize: '13px', fontWeight: 600, marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <BookOutlined /> Giải thích thuật ngữ
          </div>
          <p style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            Nhập thuật ngữ cần AI giải nghĩa.
          </p>
          <div style={{ display: 'flex', gap: '4px' }}>
            <Input size="small" value={term} onChange={(e) => setTerm(e.target.value)} placeholder="VD: SignalR" onPressEnter={handleExplainTerm} />
            <Button type="primary" size="small" onClick={handleExplainTerm}>Hỏi</Button>
          </div>
        </Card>
      </div>

      <div style={{ fontSize: '11px', color: 'var(--text-muted)', textAlign: 'center', marginTop: 'auto' }}>
        NexivraChat
      </div>
    </div>
  );
};
