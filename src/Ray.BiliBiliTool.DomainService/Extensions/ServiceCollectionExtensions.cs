using Microsoft.Extensions.DependencyInjection;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ChargeExecutionPolicy>();
        services.AddSingleton<RankingVideoCache>();
        services.AddSingleton<DonateCoinSelectionStateStore>();
        services.AddSingleton<VipBigPointAccessKeyStore>();
        services.AddSingleton<ITaskDelay, TaskDelay>();

        services.Scan(scan =>
            scan.FromAssemblyOf<IAccountDomainService>()
                .AddClasses(classes => classes.AssignableTo<IDomainService>())
                .AsImplementedInterfaces()
                .WithTransientLifetime()
        );

        return services;
    }
}
