import React, { useState } from 'react';
import { Button, Input, Card, Divider } from 'antd';
import { RobotOutlined, BookOutlined, BulbOutlined, AlignLeftOutlined } from '@ant-design/icons';

interface CopilotPanelProps {
  onTriggerCommand: (command: string) => void;
}

export const CopilotPanel: React.FC<CopilotPanelProps> = ({ onTriggerCommand }) => {
  const [term, setTerm] = useState('');

  const handleExplainTerm = () => {
    if (term.trim()) {
      onTriggerCommand(`@copilot Giải thích thuật ngữ "${term.trim()}" ngắn gọn trong 2 câu.`);
      setTerm('');
    }
  };

  return (
    <div style={{
      width: '280px',
      backgroundColor: '#0f172a',
      borderLeft: '1px solid #1e293b',
      padding: '16px',
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      fontFamily: 'monospace',
      color: '#fff'
    }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
        <RobotOutlined style={{ color: '#a3e635', fontSize: '20px' }} />
        <span style={{ color: '#a3e635', fontWeight: 'bold', fontSize: '15px' }}>
          // AI_COPILOT_HUD
        </span>
      </div>

      <p style={{ fontSize: '11px', color: '#64748b', lineHeight: '1.5' }}>
        Trợ lý AI Co-pilot hoạt động song song trong phòng chat. Bạn có thể tag @copilot trực tiếp hoặc sử dụng các lệnh phím tắt nhanh dưới đây:
      </p>

      <Divider style={{ borderColor: '#1e293b', margin: '12px 0' }} />

      {/* Quick Actions */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', flex: 1 }}>
        <Card
          size="small"
          style={{
            backgroundColor: '#0b0f19',
            borderColor: '#1e293b',
            borderRadius: 0,
          }}
          styles={{
            body: { padding: '12px' }
          }}
        >
          <div style={{ color: '#a3e635', fontSize: '12px', fontWeight: 'bold', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <AlignLeftOutlined /> TÓM TẮT PHÒNG CHAT
          </div>
          <p style={{ fontSize: '10px', color: '#64748b', marginBottom: '8px' }}>
            Tóm tắt 10 tin nhắn thảo luận gần nhất trong phòng chat.
          </p>
          <Button
            type="dashed"
            size="small"
            onClick={() => onTriggerCommand('@copilot Tóm tắt ngắn gọn các cuộc trò chuyện gần đây trong phòng này.')}
            style={{
              width: '100%',
              borderRadius: 0,
              borderColor: '#a3e635',
              color: '#a3e635',
              backgroundColor: 'transparent',
              fontFamily: 'monospace',
              fontSize: '11px'
            }}
          >
            RUN_SUMMARIZE
          </Button>
        </Card>

        <Card
          size="small"
          style={{
            backgroundColor: '#0b0f19',
            borderColor: '#1e293b',
            borderRadius: 0,
          }}
          styles={{
            body: { padding: '12px' }
          }}
        >
          <div style={{ color: '#a3e635', fontSize: '12px', fontWeight: 'bold', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <LightBulbOutlined /> GỢI Ý CHỦ ĐỀ CHAT
          </div>
          <p style={{ fontSize: '10px', color: '#64748b', marginBottom: '8px' }}>
            AI gợi ý các chủ đề thảo luận tiếp theo cho phòng chat.
          </p>
          <Button
            type="dashed"
            size="small"
            onClick={() => onTriggerCommand('@copilot Hãy gợi ý 3 ý tưởng/chủ đề thảo luận tiếp theo cho phòng chat này.')}
            style={{
              width: '100%',
              borderRadius: 0,
              borderColor: '#a3e635',
              color: '#a3e635',
              backgroundColor: 'transparent',
              fontFamily: 'monospace',
              fontSize: '11px'
            }}
          >
            BRAINSTORM_TOPICS
          </Button>
        </Card>

        <Card
          size="small"
          style={{
            backgroundColor: '#0b0f19',
            borderColor: '#1e293b',
            borderRadius: 0,
          }}
          styles={{
            body: { padding: '12px' }
          }}
        >
          <div style={{ color: '#a3e635', fontSize: '12px', fontWeight: 'bold', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <BookOutlined /> GIẢI THÍCH THUẬT NGỮ
          </div>
          <p style={{ fontSize: '10px', color: '#64748b', marginBottom: '8px' }}>
            Nhập thuật ngữ cần AI giải nghĩa.
          </p>
          <div style={{ display: 'flex', gap: '4px' }}>
            <Input
              size="small"
              value={term}
              onChange={(e) => setTerm(e.target.value)}
              placeholder="e.g. SignalR"
              onPressEnter={handleExplainTerm}
              style={{
                backgroundColor: '#1e293b',
                borderColor: '#475569',
                color: '#fff',
                borderRadius: 0,
                fontFamily: 'monospace'
              }}
            />
            <Button
              type="primary"
              size="small"
              onClick={handleExplainTerm}
              style={{
                backgroundColor: '#a3e635',
                borderColor: '#a3e635',
                color: '#000',
                borderRadius: 0,
                fontWeight: 'bold',
                fontFamily: 'monospace'
              }}
            >
              RUN
            </Button>
          </div>
        </Card>
      </div>

      <div style={{ fontSize: '9px', color: '#475569', textAlign: 'center', marginTop: 'auto' }}>
        SYSTEM_ACTIVE v1.0.0
      </div>
    </div>
  );
};
