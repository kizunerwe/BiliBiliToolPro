using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Relation;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.Video;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.DomainService;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace DomainServiceTest;

public sealed class DonateCoinSelectionBehaviorTest
{
    [Fact]
    public async Task AddCoinsForVideos_ShouldProcessConfiguredUpFromOldToNewAndPersistBlacklist()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                487417170,
                [
                    CreateVideo(102, "video-102"),
                    CreateVideo(101, "video-101"),
                    CreateVideo(100, "video-100"),
                ]
            );

            var videoApi = new FakeVideoApi();
            videoApi.SetDonatedCoins(100, 1);
            videoApi.SetDonatedCoins(101, 0);
            videoApi.SetDonatedCoins(102, 0);

            var domainService = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                supportUpIds: "487417170",
                numberOfCoins: 2
            );

            await domainService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains("【视频来源】配置UP", logger.Messages);
            Assert.Contains(
                logger.Messages,
                x => x.Contains("【配置UP】487417170：历史终态 0 / 当前视频 3")
            );
            Assert.Contains(logger.Messages, x => x.Contains("扫描进行中"));
            Assert.DoesNotContain(
                logger.Messages,
                message => message.Contains("跳过候选视频 Av100：已投过1枚硬币")
            );
            Assert.True(
                logger.Messages.IndexOf("【视频】video-101")
                    < logger.Messages.IndexOf("【视频】video-102")
            );

            var accountState = await stateStore.GetAccountStateAsync("10001");
            Assert.Contains(100, accountState.BlacklistedAids);
            Assert.Contains(101, accountState.BlacklistedAids);
            Assert.Contains(102, accountState.BlacklistedAids);
            Assert.Equal(3, accountState.ConfigUpProgressByUpId[487417170].RecordedVideoCount);
            Assert.Equal(3, accountState.ConfigUpProgressByUpId[487417170].RecordedAids.Count);
            Assert.DoesNotContain(
                logger.Messages,
                message => message.Contains("已在投币进度中记录")
            );
            Assert.Contains(logger.Messages, message => message.Contains("页汇总：本段接口检查"));
            var configUpProgressMessages = logger
                .Messages.Where(message => message.StartsWith("【配置UP】487417170：历史终态"))
                .ToList();
            Assert.Equal(
                configUpProgressMessages.Count,
                configUpProgressMessages.Distinct().Count()
            );
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldAggregateConfiguredUpTerminalVideosPerPage()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable
                    .Range(1, 30)
                    .Reverse()
                    .Select(aid => CreateVideo(aid, $"video-{aid}"))
                    .ToList()
            );
            var videoApi = new FakeVideoApi();
            foreach (var aid in Enumerable.Range(1, 30))
            {
                videoApi.SetDonatedCoins(aid, 1);
            }

            var service = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.DoesNotContain(
                logger.Messages,
                message => message.StartsWith("跳过候选视频 Av")
            );
            Assert.Contains(
                "【配置UP】1 第1页汇总：本段接口检查 30 个，历史跳过 0 个，新确认已投币 30 个，本页已完成",
                logger.Messages
            );
            Assert.Equal(
                2,
                logger.Messages.Count(message => message.StartsWith("【配置UP】1：历史终态"))
            );
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotCountHistoricalBlacklistAsNewStatusChecks()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            foreach (var aid in Enumerable.Range(1, 30))
            {
                await stateStore.MarkVideoAsBlacklistedAsync("10001", aid);
            }

            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable
                    .Range(1, 30)
                    .Reverse()
                    .Select(aid => CreateVideo(aid, $"video-{aid}"))
                    .ToList()
            );
            var service = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains(
                "【配置UP】1 第1页汇总：本段接口检查 0 个，历史跳过 30 个，新确认已投币 0 个，本页已完成",
                logger.Messages
            );
            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal(0, progress.RecordedVideoCount);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldPersistPagePositionAfterSuccessfulCoin()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videos = new List<UpVideoInfo>
            {
                CreateVideo(12, "video-12"),
                CreateVideo(11, "video-11"),
                CreateVideo(10, "video-10"),
            };
            var firstVideoService = new FakeVideoDomainService();
            firstVideoService.SetConfigUpVideos(1, videos);
            var firstLogger = new ListLogger<DonateCoinDomainService>();
            var firstService = CreateDomainService(
                firstLogger,
                stateStore,
                firstVideoService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await firstService.AddCoinsForVideos(CreateCookie("10001"));

            var afterFirstRun = (
                await stateStore.GetAccountStateAsync("10001")
            ).ConfigUpProgressByUpId[1];
            Assert.Equal(1, afterFirstRun.NextVideoIndex);

            var secondVideoService = new FakeVideoDomainService();
            secondVideoService.SetConfigUpVideos(1, videos);
            var secondLogger = new ListLogger<DonateCoinDomainService>();
            var secondService = CreateDomainService(
                secondLogger,
                stateStore,
                secondVideoService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await secondService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains("【视频】video-11", secondLogger.Messages);
            Assert.DoesNotContain(
                secondLogger.Messages,
                message => message.Contains("Av10：已在投币进度中记录")
            );
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldPersistPagePositionAfterTerminalBatchBeforeFailure()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable
                    .Range(10, 5)
                    .Reverse()
                    .Select(aid => CreateVideo(aid, $"video-{aid}"))
                    .ToList()
            );
            var videoApi = new FakeVideoApi { FailingCoinStatusAid = 14 };
            foreach (var aid in Enumerable.Range(10, 4))
            {
                videoApi.SetDonatedCoins(aid, 1);
            }
            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal(DonateCoinConfigUpScanStatus.RetryableFailure, progress.Status);
            Assert.Equal(4, progress.NextVideoIndex);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotRepeatConfirmedExhaustedUpWithinCurrentRun()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            await stateStore.MarkVideoAsBlacklistedAsync("10001", 10);
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(1, [CreateVideo(10, "video-10")]);
            videoDomainService.SetConfigUpVideos(
                2,
                [CreateVideo(21, "video-21"), CreateVideo(20, "video-20")]
            );
            var service = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1,2",
                2
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal(
                1,
                videoDomainService.RequestedConfigUpPageDetails.Count(request =>
                    request.UpId == 1 && request.PageNumber == 1
                )
            );
            Assert.Equal(
                1,
                logger.Messages.Count(message =>
                    message.Contains("【配置UP】1：") && message.Contains("已确认无可投视频")
                )
            );
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldRetryFollowingsMultipleTimesBeforeFallingBack()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetRandomVideos(
                90001,
                [
                    CreateVideo(200, "video-200"),
                    CreateVideo(201, "video-201"),
                    CreateVideo(202, "video-202"),
                ]
            );

            var relationApi = new FakeRelationApi();
            relationApi.SetFollowings([90001]);

            var videoApi = new FakeVideoApi();
            videoApi.SetDonatedCoins(200, 2);
            videoApi.SetDonatedCoins(201, 2);
            videoApi.SetDonatedCoins(202, 0);

            var domainService = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                relationApi,
                videoApi,
                supportUpIds: "",
                numberOfCoins: 1
            );

            await domainService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains("【视频来源】普通关注", logger.Messages);
            Assert.Contains("【视频】video-202", logger.Messages);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotLogGenericRankingMissAfterRiskWarning()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService
            {
                RankingException = new InvalidOperationException("B站返回 -352"),
            };

            var domainService = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                supportUpIds: "",
                numberOfCoins: 1
            );

            await domainService.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Contains(
                logger.Messages,
                x => x.Contains("【选源】排行榜：获取失败，可能触发风控或验证码，已跳过。")
            );
            Assert.DoesNotContain(logger.Messages, x => x.Contains("【选源】排行榜未找到可投视频"));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FollowingApiFailures_ShouldKeepBusinessCodesInSourceLogs()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var logger = new ListLogger<DonateCoinDomainService>();
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var relationApi = new FakeRelationApi
            {
                SpecialFollowingCode = 1001,
                FollowingCode = 1002,
            };
            var videoDomainService = new FakeVideoDomainService
            {
                RankingException = new InvalidOperationException("无排行榜候选"),
            };
            var service = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                relationApi,
                new FakeVideoApi(),
                supportUpIds: "",
                numberOfCoins: 1
            );

            Assert.Null(await service.TryGetCanDonatedVideo(CreateCookie("10001")));
            Assert.Contains(logger.Messages, x => x.Contains("获取特别关注列表失败：1001"));
            Assert.Contains(logger.Messages, x => x.Contains("获取关注列表失败：1002"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldSkipCoinWhenUpFriendlyWatchFails()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var videoDomainService = new FakeVideoDomainService { WatchResult = false };
            videoDomainService.SetConfigUpVideos(1, [CreateVideo(10, "video-10")]);
            var videoApi = new FakeVideoApi();
            var service = CreateDomainService(
                new ListLogger<DonateCoinDomainService>(),
                new DonateCoinSelectionStateStore(
                    NullLogger<DonateCoinSelectionStateStore>.Instance,
                    Path.Combine(tempDirectory, "state.json")
                ),
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                "1",
                1,
                true
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Empty(videoApi.AddedCoinAids);
            Assert.Contains(10, videoDomainService.WatchedAids);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotAdvancePage_WhenCoinStatusCheckFails()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(1, [CreateVideo(10, "video-10")]);
            var videoApi = new FakeVideoApi
            {
                CoinStatusException = new InvalidOperationException("状态检查失败"),
            };
            var logger = new ListLogger<DonateCoinDomainService>();
            var service = CreateDomainService(
                logger,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal([1], videoDomainService.RequestedConfigUpPages);
            Assert.Equal(1, progress.NextPageNumber);
            Assert.Equal(DonateCoinConfigUpScanStatus.RetryableFailure, progress.Status);
            Assert.NotNull(progress.FailureReason);
            Assert.DoesNotContain("可投视频不足，结束", logger.Messages);
            Assert.Contains(
                logger.Messages,
                message => message.Contains("视频扫描异常") && message.Contains("等待下次重试")
            );
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotAdvancePage_WhenExpectedPageIsEmpty()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService { ReturnEmptyConfigUpPages = true };
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 31).Select(aid => CreateVideo(aid, $"video-{aid}")).ToList()
            );
            var service = CreateDomainService(
                new ListLogger<DonateCoinDomainService>(),
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal([2], videoDomainService.RequestedConfigUpPages);
            Assert.Equal(2, progress.NextPageNumber);
            Assert.Equal(DonateCoinConfigUpScanStatus.RetryableFailure, progress.Status);
            Assert.NotNull(progress.FailureReason);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldNotAdvancePage_WhenPageRequestThrows()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService
            {
                ConfigUpPageException = new InvalidOperationException("取页失败"),
            };
            videoDomainService.SetConfigUpVideos(1, [CreateVideo(10, "video-10")]);
            var service = CreateDomainService(
                new ListLogger<DonateCoinDomainService>(),
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal([1], videoDomainService.RequestedConfigUpPages);
            Assert.Equal(1, progress.NextPageNumber);
            Assert.Equal(DonateCoinConfigUpScanStatus.RetryableFailure, progress.Status);
            Assert.NotNull(progress.FailureReason);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldConfirmExhausted_WhenAll126VideosAreBlacklisted()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService
            {
                RankingException = new InvalidOperationException("无排行榜候选"),
            };
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 126).Select(aid => CreateVideo(aid, $"video-{aid}")).ToList()
            );
            foreach (var aid in Enumerable.Range(1, 126))
            {
                await stateStore.MarkVideoAsBlacklistedAsync("10001", aid);
            }

            var service = CreateDomainService(
                new ListLogger<DonateCoinDomainService>(),
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal([5, 4, 3, 2, 1], videoDomainService.RequestedConfigUpPages);
            Assert.Equal(0, progress.NextPageNumber);
            Assert.Equal(DonateCoinConfigUpScanStatus.ConfirmedExhausted, progress.Status);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCoinsForVideos_ShouldRetryUnknownLegacyStateConservatively()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateFilePath = Path.Combine(tempDirectory, "state.json");
            await File.WriteAllTextAsync(
                stateFilePath,
                $$"""
                {
                  "accounts": {
                    "10001": {
                      "blacklistedAids": [10],
                      "configUpProgressByUpId": {
                        "1": {
                          "videoCount": 1,
                          "videoCountUpdatedOn": "{{DateOnly.FromDateTime(
                    DateTime.Now
                ):yyyy-MM-dd}}",
                          "nextPageNumber": 0,
                          "recordedVideoCount": 1
                        }
                      }
                    }
                  }
                }
                """
            );

            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                stateFilePath
            );
            var videoDomainService = new FakeVideoDomainService
            {
                RankingException = new InvalidOperationException("无排行榜候选"),
            };
            videoDomainService.SetConfigUpVideos(1, [CreateVideo(10, "video-10")]);
            var service = CreateDomainService(
                new ListLogger<DonateCoinDomainService>(),
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal([1], videoDomainService.RequestedConfigUpPages);
            Assert.Equal(DonateCoinConfigUpScanStatus.ConfirmedExhausted, progress.Status);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CrossDayConfirmedExhausted_ShouldOnlyScanNewVideoRange()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 130).Select(x => CreateVideo(x, $"video-{x}")).ToList()
            );
            await stateStore.UpdateConfigUpProgressAsync(
                "10001",
                1,
                new DonateCoinConfigUpProgressSnapshot(
                    126,
                    DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
                    0,
                    126,
                    DonateCoinConfigUpScanStatus.ConfirmedExhausted
                )
            );

            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal([1], videoDomainService.RequestedConfigUpPages);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CrossDayConfirmedExhausted_ShouldProbeNewestPage_WhenCountIsUnchanged()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 126).Select(x => CreateVideo(x, $"video-{x}")).ToList()
            );
            var recordedAids = Enumerable.Range(1, 126).Select(x => (long)x).ToHashSet();
            foreach (var aid in recordedAids)
            {
                await stateStore.MarkVideoAsBlacklistedAsync("10001", aid);
            }
            await stateStore.UpdateConfigUpProgressAsync(
                "10001",
                1,
                new DonateCoinConfigUpProgressSnapshot(
                    126,
                    DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
                    0,
                    126,
                    DonateCoinConfigUpScanStatus.ConfirmedExhausted,
                    RecordedAids: recordedAids
                )
            );

            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal([1], videoDomainService.RequestedConfigUpPages);
            var progress = (await stateStore.GetAccountStateAsync("10001")).ConfigUpProgressByUpId[
                1
            ];
            Assert.Equal(DateOnly.FromDateTime(DateTime.Now), progress.VideoCountUpdatedOn);
            Assert.Equal(DonateCoinConfigUpScanStatus.ConfirmedExhausted, progress.Status);
            Assert.Equal(0, progress.NextPageNumber);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CrossDayConfirmedExhausted_ShouldFindReplacementVideo_WhenCountIsUnchanged()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var previousAids = Enumerable.Range(1, 30).Select(x => (long)x).ToHashSet();
            foreach (var aid in previousAids)
            {
                await stateStore.MarkVideoAsBlacklistedAsync("10001", aid);
            }
            await stateStore.UpdateConfigUpProgressAsync(
                "10001",
                1,
                new DonateCoinConfigUpProgressSnapshot(
                    30,
                    DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
                    0,
                    30,
                    DonateCoinConfigUpScanStatus.ConfirmedExhausted,
                    RecordedAids: previousAids
                )
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                [
                    CreateVideo(31, "replacement-31"),
                    .. Enumerable.Range(2, 29).Select(x => CreateVideo(x, $"video-{x}")),
                ]
            );
            var videoApi = new FakeVideoApi();
            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                videoApi,
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal([1], videoDomainService.RequestedConfigUpPages);
            Assert.Contains(31, videoApi.AddedCoinAids);
            var account = await stateStore.GetAccountStateAsync("10001");
            Assert.Contains(31, account.BlacklistedAids);
            Assert.Contains(31, account.ConfigUpProgressByUpId[1].RecordedAids);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CrossDayConfirmedExhausted_ShouldRescanWhenVideoCountDecreases()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 125).Select(x => CreateVideo(x, $"video-{x}")).ToList()
            );
            await stateStore.UpdateConfigUpProgressAsync(
                "10001",
                1,
                new DonateCoinConfigUpProgressSnapshot(
                    126,
                    DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
                    0,
                    126,
                    DonateCoinConfigUpScanStatus.ConfirmedExhausted
                )
            );

            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal([5], videoDomainService.RequestedConfigUpPages);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(DonateCoinConfigUpScanStatus.Unknown)]
    [InlineData(DonateCoinConfigUpScanStatus.InProgress)]
    [InlineData(DonateCoinConfigUpScanStatus.RetryableFailure)]
    public async Task CrossDayNonTerminalState_ShouldUseFullCurrentRange(
        DonateCoinConfigUpScanStatus status
    )
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 126).Select(x => CreateVideo(x, $"video-{x}")).ToList()
            );
            foreach (var aid in Enumerable.Range(1, 126))
                await stateStore.MarkVideoAsBlacklistedAsync("10001", aid);
            await stateStore.UpdateConfigUpProgressAsync(
                "10001",
                1,
                new DonateCoinConfigUpProgressSnapshot(
                    126,
                    DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
                    1,
                    0,
                    status
                )
            );

            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal([5, 4, 3, 2, 1], videoDomainService.RequestedConfigUpPages);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(DonateCoinConfigUpScanStatus.InProgress)]
    [InlineData(DonateCoinConfigUpScanStatus.RetryableFailure)]
    public async Task SameDayNonTerminalZeroCursor_ShouldRestartCurrentRange(
        DonateCoinConfigUpScanStatus status
    )
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var stateStore = new DonateCoinSelectionStateStore(
                NullLogger<DonateCoinSelectionStateStore>.Instance,
                Path.Combine(tempDirectory, "donate-coin-state.json")
            );
            var videoDomainService = new FakeVideoDomainService();
            videoDomainService.SetConfigUpVideos(
                1,
                Enumerable.Range(1, 126).Select(x => CreateVideo(x, $"video-{x}")).ToList()
            );
            foreach (var aid in Enumerable.Range(1, 126))
                await stateStore.MarkVideoAsBlacklistedAsync("10001", aid);
            await stateStore.UpdateConfigUpProgressAsync(
                "10001",
                1,
                new DonateCoinConfigUpProgressSnapshot(
                    126,
                    DateOnly.FromDateTime(DateTime.Now),
                    0,
                    126,
                    status
                )
            );

            var service = CreateDomainService(
                NullLogger<DonateCoinDomainService>.Instance,
                stateStore,
                videoDomainService,
                new FakeRelationApi(),
                new FakeVideoApi(),
                "1",
                1
            );

            await service.AddCoinsForVideos(CreateCookie("10001"));

            Assert.Equal([5, 4, 3, 2, 1], videoDomainService.RequestedConfigUpPages);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static DonateCoinDomainService CreateDomainService(
        ILogger<DonateCoinDomainService> logger,
        DonateCoinSelectionStateStore stateStore,
        FakeVideoDomainService videoDomainService,
        FakeRelationApi relationApi,
        FakeVideoApi videoApi,
        string supportUpIds,
        int numberOfCoins,
        bool isUpFriendlyMode = false
    )
    {
        var options = new TestOptionsMonitor<DailyTaskOptions>(
            new DailyTaskOptions
            {
                NumberOfCoins = numberOfCoins,
                NumberOfProtectedCoins = 0,
                SelectLike = false,
                SupportUpIds = supportUpIds,
                IsUpFriendlyMode = isUpFriendlyMode,
            }
        );

        return new DonateCoinDomainService(
            logger,
            options,
            new FakeAccountApi(),
            new FakeCoinDomainService(),
            videoDomainService,
            new FakeFavoriteDomainService(),
            relationApi,
            videoApi,
            stateStore
        );
    }

    private static BiliCookie CreateCookie(string userId)
    {
        return new BiliCookie(
            new Dictionary<string, string>
            {
                ["DedeUserID"] = userId,
                ["SESSDATA"] = "sess",
                ["bili_jct"] = "csrf",
                ["buvid3"] = "buvid",
            }
        );
    }

    private static UpVideoInfo CreateVideo(long aid, string title)
    {
        return new UpVideoInfo
        {
            Aid = aid,
            Bvid = $"BV{aid}",
            Title = title,
            Length = "00:15",
        };
    }

    private sealed class FakeFavoriteDomainService : IFavoriteDomainService
    {
        public long? FolderId { get; set; } = 1;
        public bool AddResult { get; set; } = true;
        public List<long> AddedAids { get; } = [];

        public Task<long?> GetOrCreateFolderAsync(
            string folderName,
            BiliCookie cookie,
            long aid = 0
        ) => Task.FromResult(FolderId);

        public Task<bool> AddVideoAsync(
            long aid,
            long folderId,
            string fromSpmid,
            string spmid,
            string statistics,
            BiliCookie cookie
        )
        {
            AddedAids.Add(aid);
            return Task.FromResult(AddResult);
        }
    }

    private sealed class FakeCoinDomainService : ICoinDomainService
    {
        public Task<decimal> GetCoinBalance(BiliCookie ck) => Task.FromResult(10m);

        public Task<int> GetDonatedCoins(BiliCookie ck) => Task.FromResult(0);
    }

    private sealed class FakeVideoDomainService : IVideoDomainService
    {
        private readonly Dictionary<long, List<UpVideoInfo>> _configUpVideos = [];
        private readonly Dictionary<long, Queue<UpVideoInfo>> _randomVideos = [];

        public Exception? RankingException { get; set; }
        public Exception? ConfigUpPageException { get; set; }
        public bool ReturnEmptyConfigUpPages { get; set; }
        public bool WatchResult { get; set; } = true;
        public List<long> WatchedAids { get; } = [];
        public List<int> RequestedConfigUpPages { get; } = [];
        public List<(long UpId, int PageNumber)> RequestedConfigUpPageDetails { get; } = [];

        public void SetConfigUpVideos(long upId, List<UpVideoInfo> videos)
        {
            _configUpVideos[upId] = videos;
        }

        public void SetRandomVideos(long upId, List<UpVideoInfo> videos)
        {
            _randomVideos[upId] = new Queue<UpVideoInfo>(videos);
        }

        public Task<VideoDetail> GetVideoDetail(string aid)
        {
            return Task.FromResult(
                new VideoDetail
                {
                    Aid = long.Parse(aid),
                    Bvid = $"BV{aid}",
                    Title = $"video-{aid}",
                    Copyright = 1,
                }
            );
        }

        public Task<RankingInfo> GetRandomVideoOfRanking()
        {
            if (RankingException != null)
            {
                throw RankingException;
            }

            return Task.FromResult(
                new RankingInfo
                {
                    Aid = 300,
                    Bvid = "BV300",
                    Title = "ranking-300",
                }
            );
        }

        public Task<UpVideoInfo?> GetRandomVideoOfUp(long upId, int total, BiliCookie ck)
        {
            if (_randomVideos.TryGetValue(upId, out var queue) && queue.Count > 0)
            {
                return Task.FromResult<UpVideoInfo?>(queue.Dequeue());
            }

            return Task.FromResult<UpVideoInfo?>(null);
        }

        public Task<IReadOnlyList<UpVideoInfo>> GetVideosOfUp(
            long upId,
            int pageNumber,
            int pageSize,
            BiliCookie ck
        )
        {
            RequestedConfigUpPages.Add(pageNumber);
            RequestedConfigUpPageDetails.Add((upId, pageNumber));
            if (ConfigUpPageException != null)
            {
                throw ConfigUpPageException;
            }

            if (ReturnEmptyConfigUpPages)
            {
                return Task.FromResult<IReadOnlyList<UpVideoInfo>>([]);
            }

            if (!_configUpVideos.TryGetValue(upId, out var videos))
            {
                return Task.FromResult<IReadOnlyList<UpVideoInfo>>([]);
            }

            var skip = (pageNumber - 1) * pageSize;
            return Task.FromResult<IReadOnlyList<UpVideoInfo>>(
                videos.Skip(skip).Take(pageSize).ToList()
            );
        }

        public Task<int> GetVideoCountOfUp(long upId, BiliCookie ck)
        {
            if (_configUpVideos.TryGetValue(upId, out var configVideos))
            {
                return Task.FromResult(configVideos.Count);
            }

            if (_randomVideos.TryGetValue(upId, out var randomVideos))
            {
                return Task.FromResult(randomVideos.Count);
            }

            return Task.FromResult(0);
        }

        public Task<bool> WatchVideoForUpFriendlyMode(
            VideoDetail video,
            BiliCookie ck,
            CancellationToken cancellationToken
        )
        {
            WatchedAids.Add(video.Aid);
            return Task.FromResult(WatchResult);
        }

        public Task<TaskStepResult> WatchAndShareVideo(DailyTaskInfo dailyTaskStatus, BiliCookie ck)
        {
            throw new NotImplementedException();
        }

        public Task WatchVideo(VideoInfoDto videoInfo, BiliCookie ck)
        {
            throw new NotImplementedException();
        }

        public Task ShareVideo(VideoInfoDto videoInfo, BiliCookie ck)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeAccountApi : IAccountApi
    {
        public Task<BiliApiResponse<CoinBalance>> GetCoinBalanceAsync(string ck)
        {
            return Task.FromResult(
                new BiliApiResponse<CoinBalance>
                {
                    Code = 0,
                    Data = new CoinBalance { Money = 8m },
                }
            );
        }
    }

    private sealed class FakeRelationApi : IRelationApi
    {
        private readonly List<long> _followings = [];

        public int SpecialFollowingCode { get; set; }
        public int FollowingCode { get; set; }

        public void SetFollowings(List<long> followings)
        {
            _followings.Clear();
            _followings.AddRange(followings);
        }

        public Task<BiliApiResponse<GetFollowingsResponse>> GetFollowings(
            GetFollowingsRequest request,
            string ck
        )
        {
            return Task.FromResult(
                new BiliApiResponse<GetFollowingsResponse>
                {
                    Code = FollowingCode,
                    Data =
                        FollowingCode == 0
                            ? new GetFollowingsResponse
                            {
                                Total = _followings.Count,
                                List = _followings
                                    .Select(mid => new UpInfo { Mid = mid, Uname = $"up-{mid}" })
                                    .ToList(),
                            }
                            : null,
                }
            );
        }

        public Task<BiliApiResponse<List<UpInfo>>> GetFollowingsByTag(
            GetSpecialFollowingsRequest request,
            string ck
        )
        {
            return Task.FromResult(
                new BiliApiResponse<List<UpInfo>>
                {
                    Code = SpecialFollowingCode,
                    Data = SpecialFollowingCode == 0 ? [] : null,
                }
            );
        }

        public Task<BiliApiResponse<List<TagDto>>> GetTags(
            string ck,
            string referer = RelationApiConstant.GetTagsReferer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse<CreateTagResponse>> CreateTag(
            CreateTagRequest request,
            string ck,
            string referer = RelationApiConstant.GetTagsReferer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> CopyUpsToGroup(
            CopyUserToGroupRequest request,
            string ck,
            string referer = RelationApiConstant.CopyReferer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> ModifyRelation(
            ModifyRelationRequest request,
            string ck,
            string referer = RelationApiConstant.ModifyReferer
        )
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeVideoApi : IVideoApi
    {
        private readonly Dictionary<long, int> _donatedCoins = [];
        public List<long> AddedCoinAids { get; } = [];
        public Exception? CoinStatusException { get; set; }
        public long? FailingCoinStatusAid { get; set; }

        public void SetDonatedCoins(long aid, int multiply)
        {
            _donatedCoins[aid] = multiply;
        }

        public Task<BiliApiResponse> ShareVideo(
            ShareVideoRequest request,
            string ck,
            string referer
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> UploadVideoHeartbeat(
            UploadVideoHeartbeatRequest request,
            string ck
        )
        {
            throw new NotImplementedException();
        }

        public Task<BiliApiResponse> AddCoinForVideo(
            AddCoinRequest request,
            string ck,
            string refer =
                "https://www.bilibili.com/video/BV123456/?spm_id_from=333.1007.tianma.1-1-1.click&vd_source=80c1601a7003934e7a90709c18dfcffd"
        )
        {
            AddedCoinAids.Add(request.Aid);
            return Task.FromResult(new BiliApiResponse { Code = 0, Message = "0" });
        }

        public Task<BiliApiResponse<DonatedCoinsForVideo>> GetDonatedCoinsForVideo(
            GetAlreadyDonatedCoinsRequest request,
            string ck
        )
        {
            if (CoinStatusException != null)
            {
                throw CoinStatusException;
            }

            if (FailingCoinStatusAid == request.Aid)
            {
                throw new InvalidOperationException("指定视频状态检查失败");
            }

            var multiply = _donatedCoins.TryGetValue(request.Aid, out var value) ? value : 0;
            return Task.FromResult(
                new BiliApiResponse<DonatedCoinsForVideo>
                {
                    Code = 0,
                    Data = new DonatedCoinsForVideo { Multiply = multiply },
                }
            );
        }

        public Task<BiliApiResponse<SearchUpVideosResponse>> SearchVideosByUpId(
            SearchVideosByUpIdDto request,
            string ck
        )
        {
            throw new NotImplementedException();
        }

        public Task<GetBangumiBySsidResponse> GetBangumiBySsid(long ssid, string ck)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose() { }
        }
    }
}
