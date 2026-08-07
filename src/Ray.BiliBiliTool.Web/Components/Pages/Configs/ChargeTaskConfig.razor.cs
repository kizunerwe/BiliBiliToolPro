using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Quartz;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.Web.Jobs;

namespace Ray.BiliBiliTool.Web.Components.Pages.Configs;

public partial class ChargeTaskConfig : BaseConfigComponent<ChargeTaskOptions>
{
    [Inject]
    private IOptionsMonitor<ChargeTaskOptions> ChargeTaskOptionsMonitor { get; set; } = null!;

    protected override IOptionsMonitor<ChargeTaskOptions> OptionsMonitor =>
        ChargeTaskOptionsMonitor;

    protected override JobKey GetJobKey() => ChargeJob.Key;
}
