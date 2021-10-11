## Akka.Cluster.Discovery.Cloudmap

Akka.Net Cluster Plugin that allows to manage a set of Akka.NET cluster
seed nodes using AWS Cloudmap. 

This is based on [Horusiath](https://github.com/Horusiath)'s work with [Akka.Cluster.Discovery](https://github.com/Horusiath/Akka.Cluster.Discovery)

### Example

This example uses [AWS CloudMap](https://aws.amazon.com/cloud-map/) for cluster seed node discovery.

```csharp
using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Cluster.Discovery;

var config = ConfigurationFactory.Parse(@"
  akka {
    actor.provider = cluster
    cluster.discovery {
      provider = akka.cluster.discovery.cloudmap
      cloudmap {
        namespace = ""property-core-team""
        region = ""ap-southeast-2""
        services = [""u-covidtracer-admin-api-v0"", ""u-covidtracer-api-v0""]
        class = ""Akka.Cluster.Discovery.Cloudmap.CloudmapDiscoveryService, Akka.Cluster.Discovery.Cloudmap""
      }
    }
}");

using (var system = ActorSystem.Create())
{
	// this line triggers discovery service initialization
	// and will join or initialize current actor system to the cluster
	await ClusterDiscovery.JoinAsync(system);

	Console.ReadLine();
}
```

## Configuration

```hocon
# Cluster discovery namespace
akka.cluster.discovery {
	
	# Path to a provider configuration used in for cluster discovery. Example:
	# 1. akka.cluster.discovery.cloudmap
	provider = "akka.cluster.discovery.cloudmap"

	# A configuration used by cloudmap-based discovery service
	cloudmap {
		
		# A fully qualified type name with assembly name of a discovery service class 
		# used by the cluster discovery plugin.
		class = "Akka.Cluster.Discovery.Cloudmap.CloudmapDiscoveryService, Akka.Cluster.Discovery.Cloudmap"

		# Define a dispatcher type used by discovery service actor.
		dispatcher = "akka.actor.default-dispatcher"

		# Time interval in which a `alive` signal will be send by a discovery service
		# to fit the external service TTL (time to live) expectations. 
		alive-interval = 5s

		# Time to live given for a discovery service to be correctly acknowledged as
		# alive by external monitoring service. It must be higher than `alive-interval`. 
		alive-timeout = 1m

		# Interval in which current cluster node will reach for a discovery service
		# to retrieve data about registered node updates. Nodes, that have been detected
		# as "lost" from service discovery provider, will be downed and removed from the cluster. 
		refresh-interval = 1m

		# Maximum number of retries given for a discovery service to register itself
		# inside 3rd party provider before hitting hard failure. 
		join-retries = 3

		# An AWS region.
		region = ""

		# An AWS Cloudmap namespace.
		namespace = ""

		# A list of AWS Cloudmap services to participate in the cluster.
		services = []

		# A timeout configured for cloudmap to mark a time to live given for a node
		# before it will be marked as unhealthy. Must be greater than `alive-interval` and less than `alive-timeout`.
		service-check-ttl = 15s
		
		# An interval in which cloudmap client will be triggered for periodic restarts. 
		# If not provided or 0, client will never be restarted. 
		restart-interval = 5m
	}
}
```
