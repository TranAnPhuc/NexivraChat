import React, { useState, useEffect } from 'react';
import { Modal, Button, Input, Select, Divider, Progress, Tag, Space, message, Spin } from 'antd';
import { GlobalOutlined, ThunderboltOutlined, BulbOutlined, EditOutlined, CheckOutlined } from '@ant-design/icons';
import api from '../services/api';

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

interface UserProfileData {
  userId: number;
  username: string;
  bio: string;
  nativeLanguage: string;
  aiAnalysisJson: string | null;
  lastAnalyzedAt: string | null;
}

export const ProfileView: React.FC<ProfileViewProps> = ({ userId, isOpen, onClose, currentUsername }) => {
  const [profile, setProfile] = useState<UserProfileData | null>(null);
  const [loading, setLoading] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editedBio, setEditedBio] = useState('');
  const [editedLang, setEditedLang] = useState('Vietnamese');

  const isOwnProfile = profile?.username === currentUsername;

  const fetchProfile = async () => {
    setLoading(true);
    try {
      const url = userId === 0 ? '/profile' : `/profile/${userId}`;
      const response = await api.get(url);
      setProfile(response.data);
      setEditedBio(response.data.bio || '');
      setEditedLang(response.data.nativeLanguage || 'Vietnamese');
    } catch (err) {
      message.error('Không thể tải thông tin hồ sơ.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen && userId) {
      fetchProfile();
      setIsEditing(false);
    }
  }, [isOpen, userId]);

  const handleUpdateProfile = async () => {
    try {
      const response = await api.put('/profile', { bio: editedBio, nativeLanguage: editedLang });
      setProfile(response.data);
      setIsEditing(false);
      message.success('Cập nhật hồ sơ thành công!');
    } catch (err) {
      message.error('Không thể cập nhật hồ sơ.');
    }
  };

  const handleRunAnalysis = async () => {
    setAnalyzing(true);
    try {
      const response = await api.post('/profile/analyze');
      setProfile(response.data);
      message.success('Phân tích phong cách chat bằng AI thành công!');
    } catch (err: any) {
      const errMsg = err.response?.data || 'Không thể chạy phân tích. Hãy đảm bảo bạn đã gửi ít nhất 3 tin nhắn.';
      message.error(errMsg);
    } finally {
      setAnalyzing(false);
    }
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

  const languages = [
    { value: 'Vietnamese', label: 'Tiếng Việt' },
    { value: 'English', label: 'Tiếng Anh' },
    { value: 'Japanese', label: 'Tiếng Nhật' },
    { value: 'Korean', label: 'Tiếng Hàn' },
    { value: 'Chinese', label: 'Tiếng Trung' }
  ];

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
      width={540}
      centered
      styles={{
        body: {
          backgroundColor: 'var(--bg-surface)',
          padding: '10px 0 20px 0',
          color: 'var(--text-primary)'
        }
      }}
    >
      <Spin spinning={loading}>
        {profile && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
            <div style={{ display: 'flex', gap: '20px', alignItems: 'flex-start', padding: '0 24px' }}>
              <div style={{
                width: 64,
                height: 64,
                borderRadius: '50%',
                backgroundColor: 'var(--primary-soft)',
                color: 'var(--primary)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 22,
                fontWeight: 700,
                flexShrink: 0,
                border: '2px solid var(--border)'
              }}>
                {initials}
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text-primary)', fontFamily: "'Outfit', sans-serif", display: 'flex', alignItems: 'center', gap: '10px' }}>
                  {profile.username}
                  {isOwnProfile && !isEditing && (
                    <Button type="text" size="small" icon={<EditOutlined style={{ color: 'var(--primary)' }} />} onClick={() => setIsEditing(true)} />
                  )}
                </div>
                
                {isEditing ? (
                  <div style={{ marginTop: '8px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    <Input.TextArea
                      value={editedBio}
                      onChange={(e) => setEditedBio(e.target.value)}
                      placeholder="Viết vài dòng giới thiệu bản thân..."
                      rows={2}
                      maxLength={150}
                    />
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Ngôn ngữ:</span>
                        <Select
                          value={editedLang}
                          onChange={(val) => setEditedLang(val)}
                          options={languages}
                          size="small"
                          style={{ width: 110 }}
                        />
                      </div>
                      <Space>
                        <Button size="small" onClick={() => setIsEditing(false)}>Hủy</Button>
                        <Button size="small" type="primary" icon={<CheckOutlined />} onClick={handleUpdateProfile}>Lưu</Button>
                      </Space>
                    </div>
                  </div>
                ) : (
                  <div style={{ marginTop: '4px' }}>
                    <p style={{ fontSize: '14px', color: 'var(--text-secondary)', margin: 0, fontStyle: profile.bio ? 'normal' : 'italic' }}>
                      {profile.bio || 'Chưa có tiểu sử.'}
                    </p>
                    <div style={{ marginTop: '8px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-muted)' }}>
                      <GlobalOutlined />
                      <span>Ngôn ngữ bản địa: {languages.find(l => l.value === profile.nativeLanguage)?.label || profile.nativeLanguage}</span>
                    </div>
                  </div>
                )}
              </div>
            </div>

            <Divider style={{ margin: '8px 0' }} />

            <div style={{ padding: '0 24px' }}>
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
                      <div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '3px' }}>
                          <span>Mức độ thân thiện (Friendliness)</span>
                          <span style={{ fontWeight: 600 }}>{aiAnalysis.radarMetrics?.friendliness || 0}%</span>
                        </div>
                        <Progress percent={aiAnalysis.radarMetrics?.friendliness || 0} strokeColor="var(--primary)" showInfo={false} size="small" />
                      </div>
                      <div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '3px' }}>
                          <span>Tốc độ phản hồi (Responsiveness)</span>
                          <span style={{ fontWeight: 600 }}>{aiAnalysis.radarMetrics?.responsiveness || 0}%</span>
                        </div>
                        <Progress percent={aiAnalysis.radarMetrics?.responsiveness || 0} strokeColor="var(--primary)" showInfo={false} size="small" />
                      </div>
                      <div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '3px' }}>
                          <span>Độ mạch lạc, rõ ràng (Clarity)</span>
                          <span style={{ fontWeight: 600 }}>{aiAnalysis.radarMetrics?.clarity || 0}%</span>
                        </div>
                        <Progress percent={aiAnalysis.radarMetrics?.clarity || 0} strokeColor="var(--primary)" showInfo={false} size="small" />
                      </div>
                      <div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '3px' }}>
                          <span>Tính sáng tạo (Creativity)</span>
                          <span style={{ fontWeight: 600 }}>{aiAnalysis.radarMetrics?.creativity || 0}%</span>
                        </div>
                        <Progress percent={aiAnalysis.radarMetrics?.creativity || 0} strokeColor="var(--primary)" showInfo={false} size="small" />
                      </div>
                      <div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '3px' }}>
                          <span>Sự chuyên nghiệp (Professionalism)</span>
                          <span style={{ fontWeight: 600 }}>{aiAnalysis.radarMetrics?.professionalism || 0}%</span>
                        </div>
                        <Progress percent={aiAnalysis.radarMetrics?.professionalism || 0} strokeColor="var(--primary)" showInfo={false} size="small" />
                      </div>
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
