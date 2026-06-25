using Dapper;
using System;
using System.Data;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class ProfileRepository
    {
        private readonly DapperContext _context;

        public ProfileRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<UserProfile?> GetByUserId(int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"SELECT user_id, bio, native_language, ai_analysis_json, last_analyzed_at,
                                     avatar_url,
                                     social_links AS social_links_json,
                                     interests    AS interests_json
                              FROM user_profiles WHERE user_id = @userId LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<UserProfile>(query, new { userId });
            }
        }

        public async Task Upsert(UserProfile profile)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO user_profiles (user_id, bio, native_language, ai_analysis_json, last_analyzed_at, avatar_url, social_links, interests)
                    VALUES (@UserId, @Bio, @NativeLanguage, @AiAnalysisJson::jsonb, @LastAnalyzedAt, @AvatarUrl, @SocialLinksJson::jsonb, @InterestsJson::jsonb)
                    ON CONFLICT (user_id)
                    DO UPDATE SET
                        bio = EXCLUDED.bio,
                        native_language = EXCLUDED.native_language,
                        ai_analysis_json = EXCLUDED.ai_analysis_json,
                        last_analyzed_at = EXCLUDED.last_analyzed_at,
                        avatar_url = EXCLUDED.avatar_url,
                        social_links = EXCLUDED.social_links,
                        interests = EXCLUDED.interests;";

                await connection.ExecuteAsync(query, profile);
            }
        }
    }
}
