using System;
using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Configuration;

namespace Spirebyte.APIGateway.ServiceDiscovery;

public static class Extensions
{
    public static IServiceCollection AddConsul(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var consulClientConfig = configuration.GetSection("consul");

        if (consulClientConfig.GetValue<bool>("enabled"))
        {
            services.TryAddSingleton<IConsulClient>(sp => new ConsulClient(opts =>
            {
                var url = consulClientConfig.GetValue<string>("url");
                opts.Address = new Uri(url);
            }));

            services.AddSingleton<ConsulRouteMonitorWorker>();
            services.AddSingleton<IProxyConfigProvider>(p => p.GetService<ConsulRouteMonitorWorker>());

            services.AddHostedService(p => p.GetService<ConsulRouteMonitorWorker>());
        }

        return services;
    }
}