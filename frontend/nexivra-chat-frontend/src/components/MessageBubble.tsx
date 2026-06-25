import React from 'react';
import { RobotOutlined, TranslationOutlined } from '@ant-design/icons';
import type { Message } from '../views/ChatView';

interface MessageBubbleProps {
  msg: Message;
  currentUsername: string;
  translation: string | undefined;
  isTranslating: boolean;
  onTranslate: (msgId: number, text: string) => void;
  onHideTranslation: (msgId: number) => void;
  onOpenSenderProfile: (senderName: string) => void;
}

const MessageBubbleComponent: React.FC<MessageBubbleProps> = ({
  msg,
  currentUsername,
  translation,
  isTranslating,
  onTranslate,
  onHideTranslation,
  onOpenSenderProfile,
}) => {
  const isMe = msg.senderName === currentUsername;
  const isAi = msg.isAi;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignSelf: isMe ? 'flex-end' : 'flex-start', maxWidth: '74%', alignItems: isMe ? 'flex-end' : 'flex-start' }}>
      <div style={{ fontSize: '11px', color: isAi ? 'var(--primary)' : 'var(--text-muted)', marginBottom: '4px', display: 'flex', gap: '6px', alignItems: 'center' }}>
        {isAi && <RobotOutlined />}
        <span
          style={{ cursor: isAi ? 'default' : 'pointer', textDecoration: isAi ? 'none' : 'underline' }}
          onClick={() => {
            if (!isAi) {
              onOpenSenderProfile(msg.senderName);
            }
          }}
          title={isAi ? "Trợ lý AI" : `Xem hồ sơ của @${msg.senderName}`}
        >
          {isAi ? 'Trợ lý AI' : msg.senderName}
        </span>
        <span>{new Date(msg.createdAt).toLocaleTimeString()}</span>
      </div>
      <div style={{
        padding: '9px 13px',
        backgroundColor: isMe ? 'var(--bubble-me)' : isAi ? 'var(--bubble-ai-bg)' : 'var(--bubble-other)',
        color: isMe ? 'var(--bubble-me-text)' : isAi ? 'var(--bubble-ai-text)' : 'var(--bubble-other-text)',
        border: isAi ? '1px solid var(--bubble-ai-border)' : isMe ? 'none' : '1px solid var(--border)',
        borderRadius: isMe ? '14px 14px 4px 14px' : '14px 14px 14px 4px',
        fontSize: '14px',
        lineHeight: '1.5',
        whiteSpace: 'pre-wrap',
      }}>
        {msg.content === '' && isAi ? (
          <span className="copilot-loading">Trợ lý AI đang phản hồi…</span>
        ) : (
          msg.content
        )}
      </div>
      {!isMe && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px', fontSize: '11px' }}>
          {isTranslating ? (
            <span style={{ color: 'var(--primary)', fontStyle: 'italic' }}>Đang dịch...</span>
          ) : translation ? (
            <button
              onClick={() => onHideTranslation(msg.id)}
              style={{ background: 'none', border: 'none', padding: 0, color: 'var(--text-muted)', cursor: 'pointer', textDecoration: 'underline' }}
            >
              Ẩn bản dịch
            </button>
          ) : (
            <button
              onClick={() => onTranslate(msg.id, msg.content)}
              style={{ background: 'none', border: 'none', padding: 0, color: 'var(--primary)', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '3px' }}
            >
              <TranslationOutlined /> Dịch
            </button>
          )}
        </div>
      )}
      {translation && (
        <div style={{ marginTop: '6px', padding: '8px 12px', backgroundColor: 'var(--bg-elevated)', borderLeft: '3px solid var(--primary)', borderRadius: '4px 12px 12px 12px', fontSize: '13px', color: 'var(--text-secondary)', lineHeight: '1.4', maxWidth: '100%', animation: 'fadeIn 0.2s ease-out' }}>
          <div style={{ fontSize: '10px', color: 'var(--text-muted)', marginBottom: '3px', fontWeight: 600 }}>
            BẢN DỊCH
          </div>
          {translation}
        </div>
      )}
    </div>
  );
};

export const MessageBubble = React.memo(MessageBubbleComponent);
