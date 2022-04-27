using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;

namespace Spirebyte.APIGateway.ServiceDiscovery;

public class ConsulRouteMonitorWorker : BackgroundService, IProxyConfigProvider
{
    private readonly IConsulClient _consulClient;        
    private readonly IConfigValidator _proxyConfigValidator;
    private readonly ILogger<ConsulRouteMonitorWorker> _logger;
    private volatile ConsulProxyConfig _config;
    private const int DEFAULT_CONSUL_POLL_INTERVAL_MINS = 2;        

    public ConsulRouteMonitorWorker(IConsulClient consulClient, IConfigValidator proxyConfigValidator, ILogger<ConsulRouteMonitorWorker> logger)
    {
        _consulClient = consulClient;
        _config = new ConsulProxyConfig(null, null);            
        _proxyConfigValidator = proxyConfigValidator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var serviceResult = await _consulClient.Agent.Services(stoppingToken);

            if (serviceResult.StatusCode == HttpStatusCode.OK)
            {
                var clusters = await ExtractClusters(serviceResult);
                var routes = await ExtractRoutes(serviceResult);

                Update(routes, clusters);
            }

            await Task.Delay(TimeSpan.FromMinutes(DEFAULT_CONSUL_POLL_INTERVAL_MINS), stoppingToken);
        }
    }

    private async Task<List<ClusterConfig>> ExtractClusters(QueryResult<Dictionary<string, AgentService>> serviceResult)
    {
        var clusters = new Dictionary<string, ClusterConfig>();
        var serviceMapping = serviceResult.Response;
        foreach (var (key, svc) in serviceMapping)
        {
            var destinations = clusters.GetValueOrDefault(svc.Service)?.Destinations?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ??
                               new Dictionary<string, DestinationConfig>();

            destinations.Add(svc.ID, new DestinationConfig() { Address = $"{svc.Address}:{svc.Port}" });
            
            var clusterConfig = new ClusterConfig
            {
                ClusterId = svc.Service,
                LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
                Destinations = destinations
            };

            var clusterErrs = await _proxyConfigValidator.ValidateClusterAsync(clusterConfig);
            if (clusterErrs.Any())
            {
                _logger.LogError("Errors found when creating clusters for {Service}", svc.Service);
                foreach (var err in clusterErrs)
                {
                    _logger.LogError(err, "{svc.Service} cluster validation error", svc.Service);
                }
                continue;
            }

            clusters[svc.Service] = clusterConfig;
        }

        return clusters.Values.ToList();
    }

    private async Task<List<RouteConfig>> ExtractRoutes(QueryResult<Dictionary<string, AgentService>> serviceResult)
    {
        var serviceMapping = serviceResult.Response;
        var routes = new List<RouteConfig>();
        foreach (var (key, svc) in serviceMapping)
        {
            if (svc.Meta.TryGetValue("yarp", out string enableYarp) &&
                enableYarp.Equals("on", StringComparison.InvariantCulture))
            {
                if (routes.Any(r => r.ClusterId == svc.Service)) continue;

                var route = new RouteConfig
                {
                    ClusterId = svc.Service,
                    RouteId = $"{svc.Service}-route",
                    Match = new()
                    {
                        Path = svc.Meta.ContainsKey("yarp_path")?svc.Meta["yarp_path"] : default
                    },
                    Transforms = new List<IReadOnlyDictionary<string, string>>()
                    {
                        new Dictionary<string, string>(){ { "PathPattern", "{**catchall}" }}
                    }
                };

                var routeErrs = await _proxyConfigValidator.ValidateRouteAsync(route);
                if (routeErrs.Any())
                {
                    _logger.LogError("Errors found when trying to generate routes for {Service}", svc.Service);
                    foreach (var err in routeErrs)
                    {
                        _logger.LogError(err, "{svc.Service} route validation error", svc.Service);
                    }
                    continue;
                }
                routes.Add(route);
            }
        }
        return routes;
    }

   public IProxyConfig GetConfig() => _config;

    public virtual void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        var oldConfig = _config;
        _config = new ConsulProxyConfig(routes, clusters);
        oldConfig.SignalChange();
    }

    private sealed class ConsulProxyConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();
        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }

        public ConsulProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        internal void SignalChange()
        {
            _cts.Cancel();
        }
    }
}