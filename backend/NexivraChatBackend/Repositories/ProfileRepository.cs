using Dapper;
using System;
using System.Data;
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

        public UserProfile? GetByUserId(int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT * FROM user_profiles WHERE user_id = @userId LIMIT 1";
                return connection.QueryFirstOrDefault<UserProfile>(query, new { userId });
            }
        }

        public void Upsert(UserProfile profile)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO user_profiles (user_id, bio, native_language, ai_analysis_json, last_analyzed_at)
                    VALUES (@UserId, @Bio, @NativeLanguage, @AiAnalysisJson::jsonb, @LastAnalyzedAt)
                    ON CONFLICT (user_id)
                    DO UPDATE SET
                        bio = EXCLUDED.bio,
                        native_language = EXCLUDED.native_language,
                        ai_analysis_json = EXCLUDED.ai_analysis_json,
                        last_analyzed_at = EXCLUDED.last_analyzed_at;";
                
                connection.Execute(query, profile);
            }
        }
    }
}
