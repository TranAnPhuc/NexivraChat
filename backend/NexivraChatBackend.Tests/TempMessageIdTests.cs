using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Services;
using Xunit;

namespace NexivraChatBackend.Tests
{
    public class TempMessageIdTests
    {
        [Fact]
        public void Next_ReturnsNegativeValue()
        {
            Assert.True(TempMessageId.Next() < 0);
        }

        [Fact]
        public void Next_IsUniqueAcrossConcurrentCalls()
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<int>();
            Parallel.For(0, 1000, _ => results.Add(TempMessageId.Next()));
            Assert.Equal(1000, results.Distinct().Count());
        }
    }
}
