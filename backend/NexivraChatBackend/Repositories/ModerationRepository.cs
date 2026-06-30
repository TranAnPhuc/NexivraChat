using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class ModerationRepository
    {
        private readonly DapperContext _context;

        public ModerationRepository(DapperContext context)
        {
            _context = context;
        }

        public virtual async Task LogAsync(ModerationLog log)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO moderation_logs (user_id, username, context_type, original_text, tier, ai_verdict, action, created_at)
                    VALUES (@UserId, @Username, @ContextType, @OriginalText, @Tier, @AiVerdict, @Action, @CreatedAt)
                    RETURNING id;";

                var id = await connection.ExecuteScalarAsync<int>(query, new
                {
                    UserId = log.UserId,
                    Username = log.Username,
                    ContextType = log.ContextType,
                    OriginalText = log.OriginalText,
                    Tier = log.Tier,
                    AiVerdict = log.AiVerdict,
                    Action = log.Action,
                    CreatedAt = DateTime.Now
                });

                log.Id = id;
            }
        }

        public virtual async Task<int> CountRecentViolationsAsync(int userId, TimeSpan window)
        {
            using (var connection = _context.CreateConnection())
            {
                var cutoff = DateTime.Now.Subtract(window);
                var query = @"
                    SELECT COUNT(*) 
                    FROM moderation_logs 
                    WHERE user_id = @userId AND action != 'allow' AND created_at >= @cutoff;";
                return await connection.ExecuteScalarAsync<int>(query, new { userId, cutoff });
            }
        }

        public virtual async Task IncrementStrikeAndMaybeMuteAsync(int userId, int strikeThreshold, TimeSpan window, TimeSpan muteDuration)
        {
            using (var connection = _context.CreateConnection())
            {
                // Tăng strike_count trong bảng users
                var updateStrikeQuery = "UPDATE users SET strike_count = strike_count + 1 WHERE id = @userId;";
                await connection.ExecuteAsync(updateStrikeQuery, new { userId });

                // Đếm số lượng vi phạm gần đây
                var violationsCount = await CountRecentViolationsAsync(userId, window);

                if (violationsCount >= strikeThreshold)
                {
                    var mutedUntil = DateTime.Now.Add(muteDuration);
                    var muteQuery = "UPDATE users SET muted_until = @mutedUntil WHERE id = @userId;";
                    await connection.ExecuteAsync(muteQuery, new { userId, mutedUntil });
                }
            }
        }

        public virtual async Task<DateTime?> GetMutedUntilAsync(int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT muted_until AS MutedUntil FROM users WHERE id = @userId LIMIT 1;";
                return await connection.QueryFirstOrDefaultAsync<DateTime?>(query, new { userId });
            }
        }

        public virtual async Task<List<BannedWord>> GetAllBannedWordsAsync()
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id AS Id, word AS Word, tier AS Tier, created_at AS CreatedAt FROM banned_words ORDER BY word ASC;";
                return (await connection.QueryAsync<BannedWord>(query)).ToList();
            }
        }

        public virtual async Task AddBannedWordAsync(BannedWord word)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO banned_words (word, tier, created_at)
                    VALUES (@Word, @Tier, @CreatedAt)
                    RETURNING id;";
                var id = await connection.ExecuteScalarAsync<int>(query, new
                {
                    Word = word.Word,
                    Tier = word.Tier,
                    CreatedAt = DateTime.Now
                });
                word.Id = id;
            }
        }

        public virtual async Task RemoveBannedWordAsync(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "DELETE FROM banned_words WHERE id = @id;";
                await connection.ExecuteAsync(query, new { id });
            }
        }

        public virtual async Task<BannedWord?> GetBannedWordByWordAsync(string word)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id AS Id, word AS Word, tier AS Tier, created_at AS CreatedAt FROM banned_words WHERE word = @word LIMIT 1;";
                return await connection.QueryFirstOrDefaultAsync<BannedWord>(query, new { word });
            }
        }

        public virtual async Task<List<ModerationLog>> GetLogsAsync(int limit, int offset)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT id AS Id, user_id AS UserId, username AS Username, context_type AS ContextType,
                           original_text AS OriginalText, tier AS Tier, ai_verdict AS AiVerdict,
                           action AS Action, created_at AS CreatedAt
                    FROM moderation_logs
                    ORDER BY id DESC
                    LIMIT @limit OFFSET @offset;";
                return (await connection.QueryAsync<ModerationLog>(query, new { limit, offset })).ToList();
            }
        }
    }
}
