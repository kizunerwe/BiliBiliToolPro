using System;
using System.IO;
using System.Text.Json;
using Ray.BiliBiliTool.Config;
using Ray.BiliBiliTool.Config.Options;
using Xunit;

namespace ConfigTest;

public sealed class ChargeConfigurationContractTest
{
    [Fact]
    public void ChargeConfiguration_ShouldUseDedicatedSectionAndBusinessTimezone()
    {
        Assert.Equal(
            "ChargeTaskConfig:AutoChargeUpId",
            Constants.CommandLineMappingsDic["--autoChargeUpId"]
        );
        Assert.Equal("Asia/Shanghai", new ChargeTaskOptions().BusinessTimeZoneId);
    }

    [Fact]
    public void ChargeConfiguration_ShouldRunAtNoonInConsoleAndWebDefaults()
    {
        var root = GetRepositoryRoot();
        Assert.Equal(
            "0 0 12 * * ?",
            ReadJson(root, "src", "Ray.BiliBiliTool.Console", "appsettings.json")
                .RootElement.GetProperty("ChargeTaskConfig")
                .GetProperty("Cron")
                .GetString()
        );
        Assert.Equal(
            "0 0 12 * * ?",
            ReadJson(root, "src", "Ray.BiliBiliTool.Web", "appsettings.json")
                .RootElement.GetProperty("ChargeTaskConfig")
                .GetProperty("Cron")
                .GetString()
        );
    }

    [Fact]
    public void ChargeConfiguration_ShouldNotCarryPersonalTargetsInDefaults()
    {
        var root = GetRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "Ray.BiliBiliTool.Console", "appsettings.json"),
            Path.Combine(root, "src", "Ray.BiliBiliTool.Console", "appsettings.Production.json"),
            Path.Combine(root, "src", "Ray.BiliBiliTool.Console", "appsettings.Development.json"),
            Path.Combine(root, "src", "Ray.BiliBiliTool.Web", "appsettings.json"),
        };

        foreach (var file in files)
        {
            var rootElement = ReadJson(file).RootElement;
            if (rootElement.TryGetProperty("DailyTaskConfig", out var daily))
            {
                Assert.False(daily.TryGetProperty("AutoChargeUpId", out _), file);
            }

            if (
                rootElement.TryGetProperty("ChargeTaskConfig", out var charge)
                && charge.TryGetProperty("AutoChargeUpId", out var target)
            )
            {
                Assert.True(string.IsNullOrWhiteSpace(target.GetString()), file);
            }
        }
    }

    [Fact]
    public void ChargeConfiguration_ShouldBeDisabledUntilAnExplicitTargetIsConfigured()
    {
        var root = GetRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "Ray.BiliBiliTool.Console", "appsettings.json"),
            Path.Combine(root, "src", "Ray.BiliBiliTool.Console", "appsettings.Production.json"),
            Path.Combine(root, "src", "Ray.BiliBiliTool.Console", "appsettings.Development.json"),
            Path.Combine(root, "src", "Ray.BiliBiliTool.Web", "appsettings.json"),
        };

        foreach (var file in files)
        {
            var rootElement = ReadJson(file).RootElement;
            if (rootElement.TryGetProperty("ChargeTaskConfig", out var charge))
            {
                Assert.False(charge.GetProperty("IsEnable").GetBoolean(), file);
            }
        }
    }

    private static JsonDocument ReadJson(params string[] pathParts)
    {
        return ReadJson(Path.Combine(pathParts));
    }

    private static JsonDocument ReadJson(string path)
    {
        return JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }
        );
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")
        );
    }
}
