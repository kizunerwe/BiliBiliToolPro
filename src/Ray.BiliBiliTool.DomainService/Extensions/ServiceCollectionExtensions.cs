using Microsoft.Extensions.DependencyInjection;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<RankingVideoCache>();
        services.AddSingleton<DonateCoinSelectionStateStore>();
        services.AddSingleton<VipBigPointAccessKeyStore>();

        services.Scan(scan =>
            scan.FromAssemblyOf<IAccountDomainService>()
                .AddClasses(classes => classes.AssignableTo<IDomainService>())
                .AsImplementedInterfaces()
                .WithTransientLifetime()
        );

        return services;
    }
}
