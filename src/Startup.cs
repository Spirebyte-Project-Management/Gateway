using System.Threading.Tasks;
using Convey;
using Convey.Auth;
using Convey.HTTP;
using Convey.MessageBrokers.RabbitMQ;
using Convey.Metrics.AppMetrics;
using Convey.Security;
using Convey.Tracing.Jaeger;
using Convey.Tracing.Jaeger.RabbitMQ;
using IdentityModel;
using IdentityModel.AspNetCore.AccessTokenValidation;
using IdentityModel.AspNetCore.OAuth2Introspection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTracing;
using Prometheus;
using Spirebyte.APIGateway.Correlation;
using Spirebyte.APIGateway.Identity;
using Spirebyte.APIGateway.Messaging;
using Spirebyte.APIGateway.Serialization;
using Spirebyte.APIGateway.ServiceDiscovery;
using Spirebyte.Shared.IdentityServer;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Spirebyte.APIGateway;

public class Startup
{
    private readonly string _appName;
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
        _appName = configuration["app:name"];
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddConvey()
            .AddIdentityServerAuthentication(withBasic: true)
            .AddJaeger()
            .AddMetrics()
            .AddRabbitMq(plugins: p => p.AddJaegerRabbitMqPlugin())
            .AddSecurity()
            .Build();

        services.AddScoped<LogContextMiddleware>();
        services.AddScoped<UserMiddleware>();
        services.AddScoped<MessagingMiddleware>();
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.AddSingleton<ICorrelationIdFactory, CorrelationIdFactory>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<ICorrelationContextBuilder, CorrelationContextBuilder>();
        services.AddSingleton<RouteMatcher>();
        services.Configure<MessagingOptions>(_configuration.GetSection("messaging"));

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiScope", policy =>
            {
                policy.RequireAuthenticatedUser();
            });
        });

        services.AddCors(cors =>
        {
            cors.AddPolicy("cors", x =>
            {
                x.WithOrigins("*")
                    .WithMethods("POST", "PUT", "DELETE")
                    .WithHeaders("Content-Type", "Authorization");
            });
        });

        services.AddReverseProxy()
            .LoadFromConfig(_configuration.GetSection("reverseProxy"))
            .AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(transformContext =>
                {
                    var correlationIdFactory = transformContext
                        .HttpContext
                        .RequestServices
                        .GetRequiredService<ICorrelationIdFactory>();

                    var correlationId = correlationIdFactory.Create();
                    transformContext.ProxyRequest.Headers.Add("x-correlation-id", correlationId);

                    var tracer = transformContext
                        .HttpContext
                        .RequestServices
                        .GetRequiredService<ITracer>();

                    var span = tracer?.ActiveSpan?.Context?.ToString();
                    if (!string.IsNullOrWhiteSpace(span))
                        transformContext.ProxyRequest.Headers.Add("uber-trace-id", span);

                    var correlationContextBuilder = transformContext
                        .HttpContext
                        .RequestServices
                        .GetRequiredService<ICorrelationContextBuilder>();

                    var jsonSerializer = transformContext
                        .HttpContext
                        .RequestServices
                        .GetRequiredService<IJsonSerializer>();

                    var correlationContext =
                        correlationContextBuilder.Build(transformContext.HttpContext, correlationId, span);
                    transformContext.ProxyRequest.Headers.Add("x-correlation-context",
                        jsonSerializer.Serialize(correlationContext));

                    return ValueTask.CompletedTask;
                });
            });

        services.AddConsul(_configuration);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

        app.UseJaeger();
        app.UseMiddleware<LogContextMiddleware>();
        app.UseCors("cors");
        app.UseConvey();
        app.UseAuthentication();
        app.UseRabbitMq();
        app.UseMiddleware<UserMiddleware>();
        app.UseMiddleware<MessagingMiddleware>();
        app.UseRouting();
        app.UseAuthorization();
        app.UseMetrics();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/", context => context.Response.WriteAsync(_appName));
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
        });
    }
}