import React, { useState } from 'react';
import { RobotOutlined, TranslationOutlined, RollbackOutlined, EditOutlined, DeleteOutlined, CheckOutlined, CloseOutlined } from '@ant-design/icons';
import { Input, Button, Modal } from 'antd';
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
  onReply: (msg: Message) => void;
  onEdit: (msgId: number, newContent: string) => void;
  onDelete: (msgId: number) => void;
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
  onReply,
  onEdit,
  onDelete,
}) => {
  const isMe = msg.senderName === currentUsername;
  const isAi = msg.isAi;
  const isDeleted = !!msg.deletedAt;
  const isMentionedMe = !isMe && !isDeleted && new RegExp(`@${currentUsername}\\b`, 'i').test(msg.content);
  const [showPicker, setShowPicker] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editText, setEditText] = useState(msg.content);

  const handleSaveEdit = () => {
    if (editText.trim() && editText.trim() !== msg.content) {
      onEdit(msg.id, editText.trim());
    }
    setIsEditing(false);
  };

  const handleConfirmDelete = () => {
    Modal.confirm({
      title: 'Thu hồi tin nhắn?',
      content: 'Bạn có chắc chắn muốn xóa tin nhắn này không? Tin nhắn đã xóa sẽ không thể khôi phục.',
      okText: 'Thu hồi',
      cancelText: 'Hủy',
      okButtonProps: { danger: true },
      onOk: () => onDelete(msg.id),
    });
  };

  const renderFormattedContent = (text: string) => {
    const parts = text.split(/(@[A-Za-z0-9_]+)/g);
    return parts.map((part, index) => {
      if (part.startsWith('@')) {
        const usernameTag = part.substring(1);
        const isSelfTag = usernameTag.toLowerCase() === currentUsername.toLowerCase();
        return (
          <span
            key={index}
            style={{
              color: '#0D9488',
              fontWeight: isSelfTag ? 700 : 600,
              backgroundColor: isSelfTag ? 'rgba(13, 148, 136, 0.15)' : 'transparent',
              padding: isSelfTag ? '1px 4px' : '0',
              borderRadius: isSelfTag ? '4px' : '0',
            }}
          >
            {part}
          </span>
        );
      }
      return part;
    });
  };

  return (
    <div id={`msg-${msg.id}`} style={{ display: 'flex', flexDirection: 'column', alignSelf: isMe ? 'flex-end' : 'flex-start', maxWidth: '74%', alignItems: isMe ? 'flex-end' : 'flex-start' }}>
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
        {msg.editedAt && !isDeleted && (
          <span style={{ fontStyle: 'italic', fontSize: '10px', opacity: 0.8 }}>(đã sửa)</span>
        )}
      </div>

      <div
        onMouseEnter={() => setShowPicker(true)}
        onMouseLeave={() => setShowPicker(false)}
        style={{ position: 'relative', width: isEditing ? '100%' : 'auto' }}
      >
        {showPicker && msg.id > 0 && !isDeleted && !isEditing && (
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
          backgroundColor: isDeleted ? 'var(--bg-elevated)' : isMe ? 'var(--bubble-me)' : isAi ? 'var(--bubble-ai-bg)' : 'var(--bubble-other)',
          color: isDeleted ? 'var(--text-muted)' : isMe ? 'var(--bubble-me-text)' : isAi ? 'var(--bubble-ai-text)' : 'var(--bubble-other-text)',
          border: isMentionedMe ? '2px solid #0D9488' : isDeleted ? '1px dashed var(--border)' : isAi ? '1px solid var(--bubble-ai-border)' : isMe ? 'none' : '1px solid var(--border)',
          boxShadow: isMentionedMe ? '0 0 8px rgba(13, 148, 136, 0.3)' : 'none',
          borderRadius: isMe ? '14px 14px 4px 14px' : '14px 14px 14px 4px',
          fontSize: '14px',
          lineHeight: '1.5',
          whiteSpace: 'pre-wrap',
          minWidth: isEditing ? '260px' : 'auto',
        }}>
          {msg.replyToId && !isDeleted && (
            <div
              onClick={() => {
                const el = document.getElementById(`msg-${msg.replyToId}`);
                if (el) {
                  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
              }}
              style={{
                marginBottom: '6px',
                padding: '4px 8px',
                backgroundColor: isMe ? 'rgba(255,255,255,0.15)' : 'rgba(13, 148, 136, 0.1)',
                borderLeft: '3px solid #0D9488',
                borderRadius: '4px',
                cursor: 'pointer',
                fontSize: '12px',
              }}
              title="Nhấp để chuyển đến tin nhắn gốc"
            >
              <div style={{ fontWeight: 600, color: '#0D9488' }}>
                {msg.replyToSenderName || 'Tin nhắn'}
              </div>
              <div style={{ color: isMe ? 'var(--bubble-me-text)' : 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', opacity: 0.9 }}>
                {msg.replyToContent || 'Tin nhắn đã bị xóa'}
              </div>
            </div>
          )}

          {isDeleted ? (
            <span style={{ fontStyle: 'italic', color: 'var(--text-muted)' }}>Tin đã bị xóa</span>
          ) : isEditing ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '4px' }}>
              <label htmlFor={`edit-input-${msg.id}`} style={{ fontSize: '11px', fontWeight: 600, color: isMe ? 'var(--bubble-me-text)' : 'var(--text-secondary)' }}>
                Chỉnh sửa tin nhắn:
              </label>
              <Input.TextArea
                id={`edit-input-${msg.id}`}
                value={editText}
                onChange={(e) => setEditText(e.target.value)}
                autoSize={{ minRows: 1, maxRows: 4 }}
                style={{ borderRadius: '6px', fontSize: '13px' }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    handleSaveEdit();
                  }
                }}
              />
              <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                <Button size="small" icon={<CloseOutlined />} onClick={() => setIsEditing(false)}>Hủy</Button>
                <Button size="small" type="primary" icon={<CheckOutlined />} onClick={handleSaveEdit}>Lưu</Button>
              </div>
            </div>
          ) : msg.content === '' && isAi ? (
            <span className="copilot-loading">Trợ lý AI đang phản hồi…</span>
          ) : (
            renderFormattedContent(msg.content)
          )}
        </div>
      </div>

      {!isDeleted && reactions && reactions.length > 0 && (
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

      {receiptStatus && !isDeleted && (
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

      {msg.id > 0 && !isDeleted && !isEditing && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginTop: '4px', fontSize: '11px' }}>
          <button
            onClick={() => onReply(msg)}
            style={{ background: 'none', border: 'none', padding: 0, color: 'var(--text-muted)', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '3px' }}
          >
            <RollbackOutlined /> Trả lời
          </button>

          {isMe && !isAi && (
            <>
              <button
                onClick={() => { setIsEditing(true); setEditText(msg.content); }}
                style={{ background: 'none', border: 'none', padding: 0, color: 'var(--text-muted)', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '3px' }}
              >
                <EditOutlined /> Sửa
              </button>
              <button
                onClick={handleConfirmDelete}
                style={{ background: 'none', border: 'none', padding: 0, color: '#ff4d4f', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '3px' }}
              >
                <DeleteOutlined /> Xóa
              </button>
            </>
          )}

          {!isMe && (
            <>
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
            </>
          )}
        </div>
      )}

      {translation && !isDeleted && (
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
