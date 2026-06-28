using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReactionsController : ControllerBase
    {
        private readonly ReactionRepository _reactionRepository;

        public ReactionsController(ReactionRepository reactionRepository)
        {
            _reactionRepository = reactionRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetReactions([FromQuery] List<int> messageIds)
        {
            if (messageIds == null || !messageIds.Any())
            {
                return Ok(new List<ReactionSummary>());
            }

            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var reactions = await _reactionRepository.GetReactionsForMessages(messageIds, currentUserId);
            return Ok(reactions);
        }
    }
}
