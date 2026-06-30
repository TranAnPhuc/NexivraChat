import React, { useState, useEffect, useRef } from 'react';
import { Modal, Button, Input, Select, Divider, Progress, Tag, Space, message, Spin, Tooltip } from 'antd';
import {
  GlobalOutlined, ThunderboltOutlined, BulbOutlined, EditOutlined, CheckOutlined,
  CameraOutlined, LinkOutlined, PlusOutlined, DeleteOutlined, HeartOutlined
} from '@ant-design/icons';
import api, { STATIC_BASE_URL } from '../services/api';
import { useIsMobile } from '../hooks/useIsMobile';

interface ProfileViewProps {
  userId: number;
  isOpen: boolean;
  onClose: () => void;
  currentUsername: string;
}

interface AIAnalysis {
  communicationStyle: string;
  keyTraits: string[];
  habits: string[];
  radarMetrics: {
    friendliness: number;
    responsiveness: number;
    clarity: number;
    creativity: number;
    professionalism: number;
  };
}

interface SocialLink {
  label: string;
  url: string;
}

interface UserProfileData {
  userId: number;
  username: string;
  bio: string;
  nativeLanguage: string;
  aiAnalysisJson: string | null;
  lastAnalyzedAt: string | null;
  avatarUrl: string | null;
  socialLinks: SocialLink[];
  interests: string[];
}

const MAX_SOCIAL_LINKS = 8;
const MAX_INTERESTS = 15;
const MAX_AVATAR_MB = 2;

export const ProfileView: React.FC<ProfileViewProps> = ({ userId, isOpen, onClose, currentUsername }) => {
  const isMobile = useIsMobile();
  const [profile, setProfile] = useState<UserProfileData | null>(null);
  const [loading, setLoading] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editedBio, setEditedBio] = useState('');
  const [editedLang, setEditedLang] = useState('Vietnamese');
  const [editedLinks, setEditedLinks] = useState<SocialLink[]>([]);
  const [editedInterests, setEditedInterests] = useState<string[]>([]);
  const [imgError, setImgError] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const isOwnProfile = profile?.username === currentUsername;

  const syncEditState = (data: UserProfileData) => {
    setEditedBio(data.bio || '');
    setEditedLang(data.nativeLanguage || 'Vietnamese');
    setEditedLinks(data.socialLinks || []);
    setEditedInterests(data.interests || []);
  };

  const fetchProfile = async () => {
    setLoading(true);
    try {
      const url = userId === 0 ? '/profile' : `/profile/${userId}`;
      const response = await api.get(url);
      setProfile(response.data);
      setImgError(false);
      syncEditState(response.data);
    } catch (err) {
      message.error('Không thể tải thông tin hồ sơ.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen && userId !== null) {
      fetchProfile();
      setIsEditing(false);
    }
  }, [isOpen, userId]);

  const handleUpdateProfile = async () => {
    try {
      const cleanLinks = editedLinks.filter((l) => l.url && l.url.trim());
      const response = await api.put('/profile', {
        bio: editedBio,
        nativeLanguage: editedLang,
        socialLinks: cleanLinks,
        interests: editedInterests,
      });
      setProfile(response.data);
      syncEditState(response.data);
      setIsEditing(false);
      message.success('Cập nhật hồ sơ thành công!');
    } catch (err: any) {
      message.error(err.response?.data || 'Không thể cập nhật hồ sơ.');
    }
  };

  const handleAvatarPick = () => fileInputRef.current?.click();

  const handleAvatarUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ''; // cho phép chọn lại cùng file
    if (!file) return;
    if (file.size > MAX_AVATAR_MB * 1024 * 1024) {
      message.error(`Ảnh tối đa ${MAX_AVATAR_MB} MB.`);
      return;
    }
    setUploading(true);
    try {
      const form = new FormData();
      form.append('file', file);
      const response = await api.post('/profile/avatar', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      setProfile(response.data);
      setImgError(false);
      message.success('Cập nhật ảnh đại diện thành công!');
    } catch (err: any) {
      message.error(err.response?.data || 'Không tải được ảnh.');
    } finally {
      setUploading(false);
    }
  };

  const handleRemoveAvatar = async () => {
    setUploading(true);
    try {
      const response = await api.delete('/profile/avatar');
      setProfile(response.data);
      setImgError(false);
      message.success('Đã gỡ ảnh đại diện.');
    } catch (err: any) {
      message.error(err.response?.data || 'Không gỡ được ảnh.');
    } finally {
      setUploading(false);
    }
  };

  const handleRunAnalysis = async () => {
    setAnalyzing(true);
    try {
      const response = await api.post('/profile/analyze');
      setProfile(response.data);
      syncEditState(response.data);
      message.success('Phân tích phong cách chat bằng AI thành công!');
    } catch (err: any) {
      const errMsg = err.response?.data || 'Không thể chạy phân tích. Hãy đảm bảo bạn đã gửi ít nhất 3 tin nhắn.';
      message.error(errMsg);
    } finally {
      setAnalyzing(false);
    }
  };

  const addLinkRow = () => {
    if (editedLinks.length >= MAX_SOCIAL_LINKS) return;
    setEditedLinks([...editedLinks, { label: '', url: '' }]);
  };
  const updateLinkRow = (idx: number, field: keyof SocialLink, value: string) => {
    setEditedLinks(editedLinks.map((l, i) => (i === idx ? { ...l, [field]: value } : l)));
  };
  const removeLinkRow = (idx: number) => {
    setEditedLinks(editedLinks.filter((_, i) => i !== idx));
  };

  if (!isOpen) return null;

  let aiAnalysis: AIAnalysis | null = null;
  if (profile?.aiAnalysisJson) {
    try {
      aiAnalysis = JSON.parse(profile.aiAnalysisJson);
    } catch (e) {
      console.error('Lỗi parse AI analysis JSON', e);
    }
  }

  const initials = profile?.username?.slice(0, 2).toUpperCase() || 'U';
  const avatarSrc = profile?.avatarUrl ? `${STATIC_BASE_URL}${profile.avatarUrl}` : null;
  const showImage = !!avatarSrc && !imgError;

  const languages = [
    { value: 'Vietnamese', label: 'Tiếng Việt' },
    { value: 'English', label: 'Tiếng Anh' },
    { value: 'Japanese', label: 'Tiếng Nhật' },
    { value: 'Korean', label: 'Tiếng Hàn' },
    { value: 'Chinese', label: 'Tiếng Trung' }
  ];

  const sectionLabelStyle: React.CSSProperties = {
    fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '8px'
  };

  // ── Khối avatar (img hoặc initials) + overlay đổi ảnh khi own-profile ──
  const avatarBlock = (
    <div
      style={{ position: 'relative', width: 64, height: 64, flexShrink: 0 }}
      className={isOwnProfile ? 'avatar-editable' : undefined}
    >
      <div style={{
        width: 64, height: 64, borderRadius: '50%',
        backgroundColor: 'var(--primary-soft)', color: 'var(--primary)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 22, fontWeight: 700, overflow: 'hidden',
        border: '2px solid var(--border)'
      }}>
        {showImage ? (
          <img
            src={avatarSrc!}
            alt={`Ảnh đại diện của @${profile?.username || ''}`}
            onError={() => setImgError(true)}
            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
          />
        ) : (
          initials
        )}
      </div>

      {uploading && (
        <div style={{
          position: 'absolute', inset: 0, borderRadius: '50%',
          background: 'rgba(0,0,0,0.45)', display: 'flex',
          alignItems: 'center', justifyContent: 'center'
        }}>
          <Spin size="small" />
        </div>
      )}

      {isOwnProfile && !uploading && (
        <Tooltip title="Đổi ảnh đại diện">
          <button
            type="button"
            onClick={handleAvatarPick}
            aria-label="Đổi ảnh đại diện"
            className="avatar-overlay"
            style={{
              position: 'absolute', inset: 0, borderRadius: '50%',
              border: 'none', cursor: 'pointer', color: '#fff',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              background: 'rgba(0,0,0,0.45)'
            }}
          >
            <CameraOutlined style={{ fontSize: 18 }} />
          </button>
        </Tooltip>
      )}
    </div>
  );

  return (
    <Modal
      title={
        <span style={{ fontFamily: "'Outfit', sans-serif", fontSize: '18px', fontWeight: 600, color: 'var(--text-primary)' }}>
          {isOwnProfile ? 'Hồ sơ của bạn' : `Hồ sơ của @${profile?.username || ''}`}
        </span>
      }
      open={isOpen}
      onCancel={onClose}
      footer={null}
      width={isMobile ? '100%' : 540}
      style={{ maxWidth: isMobile ? 'calc(100vw - 16px)' : 'calc(100vw - 32px)', margin: isMobile ? '8px auto' : undefined }}
      centered
      styles={{
        body: {
          backgroundColor: 'var(--bg-surface)',
          padding: '10px 0 20px 0',
          color: 'var(--text-primary)'
        }
      }}
    >
      {/* Style cục bộ cho overlay avatar (chỉ hiện khi hover trên desktop). */}
      <style>{`
        .avatar-editable .avatar-overlay { opacity: 0; transition: opacity .15s ease; }
        .avatar-editable:hover .avatar-overlay { opacity: 1; }
        @media (hover: none) { .avatar-editable .avatar-overlay { opacity: 1; } }
      `}</style>

      <input
        ref={fileInputRef}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        style={{ display: 'none' }}
        onChange={handleAvatarUpload}
      />

      <Spin spinning={loading}>
        {profile && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
            {/* ── 1. DANH TÍNH ── */}
            <div style={{ display: 'flex', flexDirection: isMobile ? 'column' : 'row', gap: '20px', alignItems: isMobile ? 'center' : 'flex-start', padding: isMobile ? '0 16px' : '0 24px', textAlign: isMobile ? 'center' : 'left' }}>
              {avatarBlock}
              <div style={{ flex: 1, minWidth: 0, width: '100%' }}>
                <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text-primary)', fontFamily: "'Outfit', sans-serif", display: 'flex', alignItems: 'center', justifyContent: isMobile ? 'center' : 'flex-start', gap: '10px' }}>
                  {profile.username}
                  {isOwnProfile && !isEditing && (
                    <Button type="text" size="small" icon={<EditOutlined style={{ color: 'var(--primary)' }} />} onClick={() => setIsEditing(true)} aria-label="Chỉnh sửa hồ sơ" />
                  )}
                </div>

                {isEditing ? (
                  <div style={{ marginTop: '8px', display: 'flex', flexDirection: 'column', gap: '8px', alignItems: isMobile ? 'stretch' : 'flex-start', textAlign: 'left' }}>
                    <Input.TextArea
                      value={editedBio}
                      onChange={(e) => setEditedBio(e.target.value)}
                      placeholder="Viết vài dòng giới thiệu bản thân..."
                      rows={2}
                      maxLength={255}
                      showCount
                      style={{ width: '100%' }}
                    />
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px', width: '100%', justifyContent: isMobile ? 'center' : 'flex-start' }}>
                      <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Ngôn ngữ:</span>
                      <Select
                         value={editedLang}
                         onChange={(val) => setEditedLang(val)}
                         options={languages}
                         size="small"
                         style={{ width: 120 }}
                      />
                    </div>
                    <div style={{ display: 'flex', gap: '8px', width: '100%', justifyContent: isMobile ? 'center' : 'flex-start' }}>
                      <Button size="small" icon={<CameraOutlined />} onClick={handleAvatarPick} loading={uploading}>Tải ảnh</Button>
                      {profile.avatarUrl && (
                        <Button size="small" danger icon={<DeleteOutlined />} onClick={handleRemoveAvatar} loading={uploading}>Gỡ ảnh</Button>
                      )}
                    </div>
                  </div>
                ) : (
                  <div style={{ marginTop: '4px' }}>
                    <p style={{ fontSize: '14px', color: 'var(--text-secondary)', margin: 0, fontStyle: profile.bio ? 'normal' : 'italic' }}>
                      {profile.bio || 'Chưa có tiểu sử.'}
                    </p>
                    <div style={{ marginTop: '8px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-muted)', justifyContent: isMobile ? 'center' : 'flex-start' }}>
                      <GlobalOutlined />
                      <span>Ngôn ngữ bản địa: {languages.find(l => l.value === profile.nativeLanguage)?.label || profile.nativeLanguage}</span>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* ── 2. SỞ THÍCH ── */}
            <div style={{ padding: isMobile ? '0 16px' : '0 24px' }}>
              <div style={sectionLabelStyle}>
                <HeartOutlined style={{ color: 'var(--primary)', marginRight: 6 }} />
                Sở thích
              </div>
              {isEditing ? (
                <Select
                  mode="tags"
                  value={editedInterests}
                  onChange={(vals) => setEditedInterests(vals.slice(0, MAX_INTERESTS))}
                  placeholder="Gõ sở thích rồi nhấn Enter (vd: âm nhạc, bóng đá...)"
                  style={{ width: '100%' }}
                  tokenSeparators={[',']}
                  maxTagCount="responsive"
                  aria-label="Sở thích"
                />
              ) : profile.interests && profile.interests.length > 0 ? (
                <Space size={[8, 8]} wrap>
                  {profile.interests.map((tag, idx) => (
                    <Tag key={idx} style={{ borderRadius: '20px', padding: '3px 12px', fontSize: '12px', backgroundColor: 'var(--primary-soft)', border: '1px solid var(--primary)', color: 'var(--primary)', fontWeight: 500 }}>
                      {tag}
                    </Tag>
                  ))}
                </Space>
              ) : (
                <span style={{ fontSize: '13px', color: 'var(--text-muted)', fontStyle: 'italic' }}>
                  {isOwnProfile ? 'Chưa có sở thích. Bấm ✎ để thêm.' : 'Chưa có sở thích.'}
                </span>
              )}
            </div>

            {/* ── 3. SOCIAL LINKS ── */}
            <div style={{ padding: isMobile ? '0 16px' : '0 24px' }}>
              <div style={sectionLabelStyle}>
                <LinkOutlined style={{ color: 'var(--primary)', marginRight: 6 }} />
                Liên kết mạng xã hội
              </div>
              {isEditing ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  {editedLinks.map((link, idx) => (
                    <div key={idx} style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                      <Input
                        value={link.label}
                        onChange={(e) => updateLinkRow(idx, 'label', e.target.value)}
                        placeholder="Nhãn"
                        size="small"
                        style={{ width: isMobile ? 80 : 120 }}
                        aria-label={`Nhãn liên kết ${idx + 1}`}
                      />
                      <Input
                        value={link.url}
                        onChange={(e) => updateLinkRow(idx, 'url', e.target.value)}
                        placeholder="https://..."
                        size="small"
                        style={{ flex: 1 }}
                        aria-label={`Đường dẫn liên kết ${idx + 1}`}
                      />
                      <Button size="small" type="text" danger icon={<DeleteOutlined />} onClick={() => removeLinkRow(idx)} aria-label="Xoá liên kết" />
                    </div>
                  ))}
                  {editedLinks.length < MAX_SOCIAL_LINKS && (
                    <Button size="small" type="dashed" icon={<PlusOutlined />} onClick={addLinkRow}>Thêm liên kết</Button>
                  )}
                </div>
              ) : profile.socialLinks && profile.socialLinks.length > 0 ? (
                <Space direction="vertical" size={6} style={{ width: '100%' }}>
                  {profile.socialLinks.map((link, idx) => (
                    <a
                      key={idx}
                      href={link.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="social-link-row"
                      style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-secondary)' }}
                    >
                      <LinkOutlined style={{ color: 'var(--text-muted)' }} />
                      <span>{link.label || link.url}</span>
                    </a>
                  ))}
                </Space>
              ) : (
                <span style={{ fontSize: '13px', color: 'var(--text-muted)', fontStyle: 'italic' }}>
                  {isOwnProfile ? 'Chưa có liên kết. Bấm ✎ để thêm.' : 'Chưa có liên kết.'}
                </span>
              )}
            </div>

            {isEditing && (
              <div style={{ padding: isMobile ? '0 16px' : '0 24px', display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                <Button size="small" onClick={() => { syncEditState(profile); setIsEditing(false); }}>Hủy</Button>
                <Button size="small" type="primary" icon={<CheckOutlined />} onClick={handleUpdateProfile}>Lưu</Button>
              </div>
            )}

            <Divider style={{ margin: '4px 0' }} />

            {/* ── 4. PHÂN TÍCH AI ── */}
            <div style={{ padding: isMobile ? '0 16px' : '0 24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                <span style={{ fontFamily: "'Outfit', sans-serif", fontSize: '15px', fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <ThunderboltOutlined style={{ color: 'var(--accent)' }} />
                  Phân tích phong cách chat bằng AI
                </span>

                {isOwnProfile && (
                  <Button
                    type="primary"
                    size="small"
                    loading={analyzing}
                    onClick={handleRunAnalysis}
                    style={{ fontSize: '12px', fontWeight: 500 }}
                  >
                    {profile.lastAnalyzedAt ? 'Phân tích lại' : 'Bắt đầu phân tích'}
                  </Button>
                )}
              </div>

              {analyzing ? (
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '30px 0', gap: '12px' }}>
                  <Spin size="large" />
                  <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontStyle: 'italic' }}>
                    AI đang phân tích các tin nhắn gần nhất của bạn...
                  </span>
                </div>
              ) : aiAnalysis ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  <div style={{ backgroundColor: 'var(--bg-elevated)', padding: '12px 14px', borderRadius: '10px', border: '1px solid var(--border)' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <BulbOutlined style={{ color: 'var(--primary)' }} /> Phong cách giao tiếp
                    </div>
                    <p style={{ fontSize: '13px', lineHeight: '1.5', color: 'var(--text-secondary)', margin: 0 }}>
                      {aiAnalysis.communicationStyle}
                    </p>
                  </div>

                  <div>
                    <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '8px' }}>
                      Đặc điểm nổi bật
                    </div>
                    <Space size={[8, 8]} wrap>
                      {aiAnalysis.keyTraits?.map((trait, idx) => (
                        <Tag
                          key={idx}
                          style={{
                            borderRadius: '20px',
                            padding: '3px 12px',
                            fontSize: '12px',
                            backgroundColor: 'var(--primary-soft)',
                            border: '1px solid var(--primary)',
                            color: 'var(--primary)',
                            fontWeight: 500
                          }}
                        >
                          {trait}
                        </Tag>
                      ))}
                    </Space>
                  </div>

                  {aiAnalysis.habits && aiAnalysis.habits.length > 0 && (
                    <div>
                      <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px' }}>
                        Thói quen giao tiếp
                      </div>
                      <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13px', color: 'var(--text-secondary)', lineHeight: '1.6' }}>
                        {aiAnalysis.habits.map((habit, idx) => (
                          <li key={idx}>{habit}</li>
                        ))}
                      </ul>
                    </div>
                  )}

                  <div>
                    <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '10px' }}>
                      Chỉ số giao tiếp AI (Radar Metrics)
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                      <RadarRow label="Mức độ thân thiện (Friendliness)" value={aiAnalysis.radarMetrics?.friendliness} />
                      <RadarRow label="Tốc độ phản hồi (Responsiveness)" value={aiAnalysis.radarMetrics?.responsiveness} />
                      <RadarRow label="Độ mạch lạc, rõ ràng (Clarity)" value={aiAnalysis.radarMetrics?.clarity} />
                      <RadarRow label="Tính sáng tạo (Creativity)" value={aiAnalysis.radarMetrics?.creativity} />
                      <RadarRow label="Sự chuyên nghiệp (Professionalism)" value={aiAnalysis.radarMetrics?.professionalism} />
                    </div>
                  </div>

                  {profile.lastAnalyzedAt && (
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', textAlign: 'right', marginTop: '4px' }}>
                      Cập nhật lần cuối: {new Date(profile.lastAnalyzedAt).toLocaleString()}
                    </div>
                  )}
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '24px 0', border: '1px dashed var(--border)', borderRadius: '10px', gap: '8px' }}>
                  <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    {isOwnProfile
                      ? 'Bạn chưa thực hiện phân tích phong cách giao tiếp nào.'
                      : 'Người dùng này chưa thực hiện phân tích phong cách.'}
                  </span>
                  {isOwnProfile && (
                    <Button type="dashed" size="small" onClick={handleRunAnalysis}>
                      Bắt đầu phân tích ngay
                    </Button>
                  )}
                </div>
              )}
            </div>
          </div>
        )}
      </Spin>
    </Modal>
  );
};

const RadarRow: React.FC<{ label: string; value?: number }> = ({ label, value = 0 }) => (
  <div>
    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '3px' }}>
      <span>{label}</span>
      <span style={{ fontWeight: 600 }}>{value}%</span>
    </div>
    <Progress percent={value} strokeColor="var(--primary)" showInfo={false} size="small" />
  </div>
);
