using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Convey;
using Convey.Logging;
using Convey.Metrics.AppMetrics;
using Convey.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ntrada;
using Ntrada.Extensions.RabbitMq;
using Ntrada.Hooks;
using Spirebyte.APIGateway.Infrastructure;

namespace Spirebyte.APIGateway
{
    public class Program
    {
        public static Task Main(string[] args)
            => CreateHostBuilder(args).Build().RunAsync();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureAppConfiguration(builder =>
                        {
                            const string extension = "yml";
                            var ntradaConfig = Environment.GetEnvironmentVariable("NTRADA_CONFIG");
                            var configPath = args?.FirstOrDefault() ?? ntradaConfig ?? $"ntrada.{extension}";
                            if (!configPath.EndsWith($".{extension}"))
                            {
                                configPath += $".{extension}";
                            }

                            builder.AddYamlFile(configPath, false);
                        })
                        .ConfigureServices(services => services.AddNtrada()
                            .AddSingleton<IContextBuilder, CorrelationContextBuilder>()
                            .AddSingleton<ISpanContextBuilder, SpanContextBuilder>()
                            .AddSingleton<IHttpRequestHook, HttpRequestHook>()
                            .AddConvey()
                            .AddMetrics()
                            .AddSecurity())
                        .Configure(app => app.UseNtrada())
                        .UseLogging();
                });
    }
}
