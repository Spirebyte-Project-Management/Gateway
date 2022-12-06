using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Spirebyte.APIGateway.ServiceDiscovery;
using Spirebyte.Framework;
using Spirebyte.Framework.Contexts;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Spirebyte.APIGateway;

public class Program
{
    public static async Task Main(string[] args)
    {
        await CreateWebHostBuilder(args)
            .Build()
            .RunAsync();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args)
    {
        return WebHost.CreateDefaultBuilder(args)
            .AddSpirebyteFramework()
            .ConfigureServices((ctx, services) => services
                .AddConsulRouteMatching(ctx.Configuration)
                .AddReverseProxy()
                .LoadFromConfig(ctx.Configuration.GetSection("reverseProxy"))
                .AddTransforms(builderContext =>
                {
                    builderContext.AddRequestTransform(transformContext =>
                    {
                        var correlationId = transformContext.HttpContext.GetCorrelationId() ??
                                            Guid.NewGuid().ToString("N");
                        transformContext.ProxyRequest.Headers.Add("correlation-id", correlationId);
                        return ValueTask.CompletedTask;
                    });
                })
            )
            .Configure(app => app
                .UseSpirebyteFramework()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("",
                        ctx => ctx.Response.WriteAsync(ctx.RequestServices.GetService<AppInfo>()?.Name ?? string.Empty));
                    endpoints.MapGet("/ping", () => "pong");
                    endpoints.MapGet("/routes", context =>
                    {
                        var proxyConfigProvider = context.RequestServices.GetService<IProxyConfigProvider>();
                        return context.Response.WriteAsJsonAsync(proxyConfigProvider?.GetConfig().Routes);
                    });
                    endpoints.MapGet("/clusters", context =>
                    {
                        var proxyConfigProvider = context.RequestServices.GetService<IProxyConfigProvider>();
                        return context.Response.WriteAsJsonAsync(proxyConfigProvider?.GetConfig().Clusters);
                    });
                    endpoints.MapReverseProxy();
                })
            );
    }
}