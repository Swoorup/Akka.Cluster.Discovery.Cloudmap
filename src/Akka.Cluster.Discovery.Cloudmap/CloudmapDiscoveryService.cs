using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Configuration;
using Amazon.ServiceDiscovery;
using Amazon.ServiceDiscovery.Model;
using Akka.Actor;

namespace Akka.Cluster.Discovery.Cloudmap
{
    public class CloudmapDiscoveryService : LocklessDiscoveryService
    {
        #region internal classes

        /// <summary>
        /// Message scheduled by <see cref="CloudmapDiscoveryService"/> for itself. 
        /// Used to trigger periodic restart of cloudmap client.
        /// </summary>
        public sealed class RestartClient
        {
            public static RestartClient Instance { get; } = new RestartClient();

            private RestartClient()
            {
            }
        }

        #endregion

        private readonly CloudmapSettings _settings;
        private IAmazonServiceDiscovery _serviceDiscovery;
        private readonly ICancelable _restartTask;
        private readonly string _protocol;
        private readonly string _actorSystemName;

        public CloudmapDiscoveryService(Config config) : this(new CloudmapSettings(config))
        {
            _protocol = ((ExtendedActorSystem)Context.System).Provider.DefaultAddress.Protocol;
            _actorSystemName = ((ExtendedActorSystem)Context.System).Provider.DefaultAddress.System;
        }

        public CloudmapDiscoveryService(CloudmapSettings settings)
            : this(CreateAmazonClient(settings), settings)
        {
        }

        public CloudmapDiscoveryService(IAmazonServiceDiscovery awsClient, CloudmapSettings settings) :
            base(settings)
        {
            _serviceDiscovery = awsClient;
            _settings = settings;
            var restartInterval = settings.RestartInterval;
            if (restartInterval.HasValue && restartInterval.Value != TimeSpan.Zero)
            {
                var scheduler = Context.System.Scheduler;
                _restartTask = scheduler.ScheduleTellRepeatedlyCancelable(restartInterval.Value, restartInterval.Value,
                    Self, RestartClient.Instance, Self);
            }
        }

        protected override void Ready()
        {
            base.Ready();
            Receive<RestartClient>(_ =>
            {
                Log.Debug("Restarting amazon service discovery client...");

                _serviceDiscovery = new AmazonServiceDiscoveryClient();
                _serviceDiscovery.Dispose();
                _serviceDiscovery = CreateAmazonClient(_settings);
            });
        }

        protected override async Task<IEnumerable<Address>> GetNodesAsync(bool onlyAlive)
        {
            var discoverInstancesTasks =
                _settings.Services
                    .Select(async service =>
                    {
                        try
                        {
                            var request = new DiscoverInstancesRequest
                            {
                                HealthStatus = onlyAlive ? HealthStatusFilter.HEALTHY : HealthStatusFilter.ALL,
                                NamespaceName = _settings.Namespace,
                                ServiceName = service
                            };
                            var response = await _serviceDiscovery.DiscoverInstancesAsync(request);
                            return response.Instances.AsEnumerable();
                        }
                        catch (Exception)
                        {
                            return Enumerable.Empty<HttpInstanceSummary>();
                        }
                    });

            var instances = await Task.WhenAll(discoverInstancesTasks);
            return
                instances
                    .SelectMany(summaries => summaries)
                    .Select(summary =>
                    {
                        var hostname = $"{summary.InstanceId}.{summary.ServiceName}.{summary.NamespaceName}";
                        var port = int.Parse(summary.Attributes["AWS_INSTANCE_PORT"]);
                        return new Address(_protocol, _actorSystemName, hostname, port);
                    })
                    .ToImmutableHashSet();
        }

        protected override Task RegisterNodeAsync(MemberEntry node) => Task.CompletedTask;

        protected override Task DeregisterNodeAsync(MemberEntry node) => Task.CompletedTask;

        protected override Task MarkAsAliveAsync(MemberEntry node) => Task.CompletedTask;

        protected override void PostStop()
        {
            base.PostStop();
            _restartTask?.Cancel();
            _serviceDiscovery.Dispose();
        }

        private static AmazonServiceDiscoveryClient CreateAmazonClient(CloudmapSettings settings) =>
            new AmazonServiceDiscoveryClient(settings.RegionEndpoint);
    }
}
