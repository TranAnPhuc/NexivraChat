import React, { useState } from 'react';
import { RobotOutlined, TranslationOutlined } from '@ant-design/icons';
import type { Message, ReactionSummary } from '../views/ChatView';

interface MessageBubbleProps {
  msg: Message;
  currentUsername: string;
  translation: string | undefined;
  isTranslating: boolean;
  receiptStatus?: 'sent' | 'seen';
  reactions?: ReactionSummary[];
  onTranslate: (msgId: number, text: string) => void;
  onHideTranslation: (msgId: number) => void;
  onOpenSenderProfile: (senderName: string) => void;
  onToggleReaction: (msgId: number, emoji: string) => void;
}

const MessageBubbleComponent: React.FC<MessageBubbleProps> = ({
  msg,
  currentUsername,
  translation,
  isTranslating,
  receiptStatus,
  reactions,
  onTranslate,
  onHideTranslation,
  onOpenSenderProfile,
  onToggleReaction,
}) => {
  const isMe = msg.senderName === currentUsername;
  const isAi = msg.isAi;
  const [showPicker, setShowPicker] = useState(false);

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

      <div
        onMouseEnter={() => setShowPicker(true)}
        onMouseLeave={() => setShowPicker(false)}
        style={{ position: 'relative' }}
      >
        {showPicker && msg.id > 0 && (
          <div
            style={{
              position: 'absolute',
              top: '-30px',
              right: isMe ? '0' : 'auto',
              left: isMe ? 'auto' : '0',
              backgroundColor: 'var(--bg-surface)',
              border: '1px solid var(--border)',
              borderRadius: '16px',
              padding: '2px 8px',
              display: 'flex',
              gap: '6px',
              boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
              zIndex: 10,
              animation: 'fadeIn 0.15s ease-out',
            }}
          >
            {['👍', '❤️', '😂', '😮', '😢', '🙏'].map((emoji) => (
              <span
                key={emoji}
                onClick={() => onToggleReaction(msg.id, emoji)}
                style={{ cursor: 'pointer', fontSize: '15px', transition: 'transform 0.1s', padding: '0 2px' }}
                onMouseEnter={(e) => (e.currentTarget.style.transform = 'scale(1.3)')}
                onMouseLeave={(e) => (e.currentTarget.style.transform = 'scale(1)')}
              >
                {emoji}
              </span>
            ))}
          </div>
        )}

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
      </div>

      {reactions && reactions.length > 0 && (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px', marginTop: '4px', justifyContent: isMe ? 'flex-end' : 'flex-start' }}>
          {reactions.map((r) => (
            <button
              key={r.emoji}
              onClick={() => onToggleReaction(msg.id, r.emoji)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '3px',
                padding: '2px 7px',
                borderRadius: '12px',
                fontSize: '12px',
                backgroundColor: 'var(--bg-elevated)',
                border: r.mineReacted ? '1.5px solid #0D9488' : '1px solid var(--border)',
                color: r.mineReacted ? '#0D9488' : 'var(--text-primary)',
                cursor: 'pointer',
                transition: 'all 0.15s ease',
              }}
              title={r.mineReacted ? `Bạn và ${r.count - 1} người khác` : `${r.count} người đã thả`}
            >
              <span>{r.emoji}</span>
              <span style={{ fontSize: '11px', fontWeight: r.mineReacted ? 600 : 400 }}>{r.count}</span>
            </button>
          ))}
        </div>
      )}

      {receiptStatus && (
        <div style={{
          fontSize: '11px',
          marginTop: '3px',
          color: receiptStatus === 'seen' ? 'var(--primary)' : 'var(--text-muted)',
          fontWeight: receiptStatus === 'seen' ? 600 : 400,
          display: 'flex',
          alignItems: 'center',
          gap: '2px',
        }}>
          {receiptStatus === 'seen' ? '✓✓ Đã xem' : '✓ Đã gửi'}
        </div>
      )}
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
