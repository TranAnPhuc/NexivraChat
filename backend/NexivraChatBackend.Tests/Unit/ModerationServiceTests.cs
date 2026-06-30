using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using NexivraChatBackend.Data;

namespace NexivraChatBackend.Tests.Unit
{
    public class ModerationServiceTests
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ModerationRepository> _mockModerationRepository;
        private readonly Mock<AiService> _mockAiService;
        private readonly Mock<ILogger<ModerationService>> _mockLogger;

        public ModerationServiceTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            
            // Mock DapperContext
            var mockContext = new Mock<DapperContext>(new ConfigurationBuilder().Build());
            _mockModerationRepository = new Mock<ModerationRepository>(mockContext.Object);

            var mockConfig = new Mock<IConfiguration>();
            var mockLoggerAi = new Mock<ILogger<AiService>>();
            var mockHttpClient = new Mock<System.Net.Http.HttpClient>();
            _mockAiService = new Mock<AiService>(mockHttpClient.Object, mockConfig.Object, mockLoggerAi.Object);

            _mockLogger = new Mock<ILogger<ModerationService>>();

            // Setup service provider to return repository
            var serviceScope = new Mock<IServiceScope>();
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(x => x.GetService(typeof(ModerationRepository)))
                .Returns(_mockModerationRepository.Object);
            serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

            var serviceScopeFactory = new Mock<IServiceScopeFactory>();
            serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);

            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(serviceScopeFactory.Object);
        }

        private ModerationService CreateService(IConfiguration config)
        {
            return new ModerationService(
                _mockServiceProvider.Object,
                config,
                _mockLogger.Object,
                _mockAiService.Object
            );
        }

        [Theory]
        [InlineData("chửi", "chui")]
        [InlineData("ch0", "cho")]
        [InlineData("c h o", "cho")]
        [InlineData("c h h o o", "cho")]
        [InlineData("FUCK", "fuck")]
        [InlineData("f u c k", "fuck")]
        [InlineData("f u u u c k k", "fuck")]
        [InlineData("f!@$k", "fiask")] // leetspeak
        public void Normalize_ShouldWorkCorrectly(string input, string expected)
        {
            var result = ModerationService.Normalize(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task CheckAsync_WhenModerationDisabled_ShouldAllowImmediately()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Moderation:Enabled"] = "false"
                })
                .Build();

            _mockModerationRepository.Setup(r => r.GetAllBannedWordsAsync())
                .ReturnsAsync(new List<BannedWord>());

            var service = CreateService(config);

            var result = await service.CheckAsync("tin nhan toxic", "room");

            Assert.Equal("allow", result.Action);
            _mockAiService.Verify(a => a.ClassifyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CheckAsync_WhenHitMaskWord_ShouldMaskAndNotCallAI()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Moderation:Enabled"] = "true"
                })
                .Build();

            _mockModerationRepository.Setup(r => r.GetAllBannedWordsAsync())
                .ReturnsAsync(new List<BannedWord>
                {
                    new BannedWord { Word = "fuck", Tier = "mask" }
                });

            var service = CreateService(config);

            var result = await service.CheckAsync("con cho fuck", "room");

            Assert.Equal("mask", result.Action);
            Assert.Equal("con cho ***", result.MaskedText);
            _mockAiService.Verify(a => a.ClassifyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CheckAsync_WhenHitSuspectWord_AndAIClean_ShouldAllow()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Moderation:Enabled"] = "true"
                })
                .Build();

            _mockModerationRepository.Setup(r => r.GetAllBannedWordsAsync())
                .ReturnsAsync(new List<BannedWord>
                {
                    new BannedWord { Word = "chem", Tier = "suspect" }
                });

            _mockAiService.Setup(a => a.ClassifyAsync(It.IsAny<string>()))
                .ReturnsAsync(AiModerationVerdict.Clean);

            var service = CreateService(config);

            var result = await service.CheckAsync("chem nhau di", "room");

            Assert.Equal("allow", result.Action);
            _mockAiService.Verify(a => a.ClassifyAsync("chem nhau di"), Times.Once);
        }

        [Fact]
        public async Task CheckAsync_WhenHitSuspectWord_AndAIToxic_ShouldBlock()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Moderation:Enabled"] = "true"
                })
                .Build();

            _mockModerationRepository.Setup(r => r.GetAllBannedWordsAsync())
                .ReturnsAsync(new List<BannedWord>
                {
                    new BannedWord { Word = "chem", Tier = "suspect" }
                });

            _mockAiService.Setup(a => a.ClassifyAsync(It.IsAny<string>()))
                .ReturnsAsync(AiModerationVerdict.Toxic);

            var service = CreateService(config);

            var result = await service.CheckAsync("tao se chem may", "room");

            Assert.Equal("block", result.Action);
            Assert.Equal("Tin nhắn vi phạm chuẩn mực cộng đồng.", result.Reason);
            _mockAiService.Verify(a => a.ClassifyAsync("tao se chem may"), Times.Once);
        }

        [Fact]
        public async Task CheckAsync_WhenHitSuspectWord_AndAIUnavailable_ShouldBlock_FailClosed()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Moderation:Enabled"] = "true"
                })
                .Build();

            _mockModerationRepository.Setup(r => r.GetAllBannedWordsAsync())
                .ReturnsAsync(new List<BannedWord>
                {
                    new BannedWord { Word = "chem", Tier = "suspect" }
                });

            _mockAiService.Setup(a => a.ClassifyAsync(It.IsAny<string>()))
                .ReturnsAsync(AiModerationVerdict.Unavailable);

            var service = CreateService(config);

            var result = await service.CheckAsync("chem nhau di", "room");

            // Fail-closed -> Block
            Assert.Equal("block", result.Action);
            Assert.Equal("Hệ thống kiểm duyệt tạm bận, vui lòng thử lại.", result.Reason);
            _mockAiService.Verify(a => a.ClassifyAsync("chem nhau di"), Times.Once);
        }
    }
}
