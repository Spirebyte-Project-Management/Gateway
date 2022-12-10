using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Spirebyte.APIGateway.ServiceDiscovery;

public static class Extensions
{
    public static IServiceCollection AddConsulRouteMatching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var consulClientConfig = configuration.GetSection("consul");

        if (consulClientConfig.GetValue<bool>("enabled"))
        {
            services.AddSingleton<ConsulRouteMonitorWorker>();
            services.AddSingleton<IProxyConfigProvider>(p => p.GetService<ConsulRouteMonitorWorker>());

            services.AddHostedService(p => p.GetService<ConsulRouteMonitorWorker>());
        }

        return services;
    }
}