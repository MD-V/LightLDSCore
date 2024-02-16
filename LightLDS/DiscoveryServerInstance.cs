using Opc.Ua;
using Opc.Ua.Bindings;
using System.Runtime.Caching;

namespace LightLDS;

/// <summary>
/// The server instance
/// </summary>
public class DiscoveryServerInstance : DiscoveryServerBase
{
    private const string _CacheName = "LocalDiscoveryServerCache";
    private readonly MemoryCache _Cache = new MemoryCache(_CacheName);
    private readonly CacheItemPolicy _CacheItemPolicy;

    private readonly object _ServerLock = new object();

    /// <summary>
    /// Ctor
    /// </summary>
    public DiscoveryServerInstance()
    {
        // Set the server timeout
        _CacheItemPolicy = new CacheItemPolicy()
        {
            SlidingExpiration = TimeSpan.FromSeconds(60)
        };
    }

    #region IDiscovery
    public override ResponseHeader FindServers(RequestHeader requestHeader, string endpointUrl, StringCollection localeIds, StringCollection serverUris, out ApplicationDescriptionCollection servers)
    {
        lock (_ServerLock)
        {
            var appDescriptions = _Cache.Where(x => x.Value is ApplicationDescription).Select(a=> a.Value as ApplicationDescription);

            servers = new ApplicationDescriptionCollection(appDescriptions);

            ResponseHeader responseHeader = new ResponseHeader
            {
                Timestamp = DateTime.UtcNow,
                RequestHandle = requestHeader.RequestHandle,
                ServiceResult = new StatusCode(StatusCodes.Good)
            };

            return responseHeader;
        }
    }

    public override ResponseHeader FindServersOnNetwork(RequestHeader requestHeader, uint startingRecordId, uint maxRecordsToReturn, StringCollection serverCapabilityFilter, out DateTime lastCounterResetTime, out ServerOnNetworkCollection servers)
    {
        return base.FindServersOnNetwork(requestHeader, startingRecordId, maxRecordsToReturn, serverCapabilityFilter, out lastCounterResetTime, out servers);
    }

    public override EndpointDescriptionCollection GetEndpoints()
    {
        return base.GetEndpoints();
    }

    public override ResponseHeader GetEndpoints(RequestHeader requestHeader, string endpointUrl, StringCollection localeIds, StringCollection profileUris, out EndpointDescriptionCollection endpoints)
    {
        // filter by profile.
        var baseAddresses = FilterByProfile(profileUris, BaseAddresses);

        // get the descriptions.
        endpoints = GetEndpointDescriptions(
            endpointUrl,
            baseAddresses,
            localeIds);

        return CreateResponse(requestHeader, StatusCodes.Good);
    }

    protected override void ValidateRequest(RequestHeader requestHeader)
    {
        base.ValidateRequest(requestHeader);
    }


    public override ResponseHeader RegisterServer2(RequestHeader requestHeader, RegisteredServer server, ExtensionObjectCollection discoveryConfiguration, out StatusCodeCollection configurationResults, out DiagnosticInfoCollection diagnosticInfos)
    {
        diagnosticInfos = new DiagnosticInfoCollection();

        configurationResults = new StatusCodeCollection();
        
        lock (_ServerLock)
        {
            //Validation
            if (server == null || string.IsNullOrEmpty(server.ServerUri))
            {
                configurationResults.Add(StatusCodes.Bad);
                return CreateResponse(requestHeader, StatusCodes.Bad);
            }

            if (server.IsOnline)
            {
                var newServer = new ApplicationDescription
                {
                    ApplicationName = server.ServerNames?[0],
                    ApplicationUri = server.ServerUri,
                    ApplicationType = server.ServerType,
                    ProductUri = server.ProductUri,
                    DiscoveryUrls = server.DiscoveryUrls
                };

                var cacheItem = new CacheItem(server.ServerUri, newServer);
                _Cache.Set(cacheItem, _CacheItemPolicy);

                Console.WriteLine($"RegisteredServer: {server.ServerUri}");
            }
            else
            {
                _Cache.Remove(server.ServerUri);
            }
        }

        configurationResults.Add(StatusCodes.Good);
        return CreateResponse(requestHeader, StatusCodes.Good);
    }

    public override ResponseHeader RegisterServer(RequestHeader requestHeader, RegisteredServer server)
    {
        lock (_ServerLock)
        {
            //Validation
            if(server == null || string.IsNullOrEmpty(server.ServerUri))
            {
                return CreateResponse(requestHeader, StatusCodes.Bad);
            }

            if (server.IsOnline)
            {
                var newServer = new ApplicationDescription
                {
                    ApplicationName = server.ServerNames?[0],
                    ApplicationUri = server.ServerUri,
                    ApplicationType = server.ServerType,
                    ProductUri = server.ProductUri,
                    DiscoveryUrls = server.DiscoveryUrls
                };

                var cacheItem = new CacheItem(server.ServerUri, newServer);
                _Cache.Set(cacheItem, _CacheItemPolicy);

                Console.WriteLine($"RegisteredServer: {server.ServerUri}");
            }
            else
            {
                _Cache.Remove(server.ServerUri);
            }
        }

        return CreateResponse(requestHeader, StatusCodes.Good);
    }

    /// <summary>
    /// Returns the endpoints that match the base addresss and endpoint url.
    /// </summary>
    protected EndpointDescriptionCollection GetEndpointDescriptions(
        string endpointUrl,
        IList<BaseAddress> baseAddresses,
        StringCollection localeIds)
    {
        EndpointDescriptionCollection endpoints = new EndpointDescriptionCollection();

        // parse the url provided by the client.
        Uri parsedEndpointUrl = Utils.ParseUri(endpointUrl);

        if (parsedEndpointUrl != null)
        {
            baseAddresses = FilterByEndpointUrl(parsedEndpointUrl, baseAddresses);
        }

        // check if nothing to do.
        if (baseAddresses.Count != 0)
        {
            // localize the application name if requested.
            LocalizedText applicationName = this.ServerDescription.ApplicationName;

            // translate the application description.
            ApplicationDescription application = TranslateApplicationDescription(
                parsedEndpointUrl,
                ServerDescription,
                baseAddresses,
                applicationName);

            // translate the endpoint descriptions.
            endpoints = TranslateEndpointDescriptions(
                parsedEndpointUrl,
                baseAddresses,
                Endpoints,
                application);
        }

        return endpoints;
    }


    #endregion

    /// <summary>
    /// Creates the endpoints and creates the hosts.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bindingFactory">The binding factory.</param>
    /// <param name="serverDescription">The server description.</param>
    /// <param name="endpoints">The endpoints.</param>
    /// <returns>
    /// Returns IList of a host for a UA service which type is <seealso cref="ServiceHost"/>.
    /// </returns>
    protected override IList <ServiceHost> InitializeServiceHosts(
    ApplicationConfiguration configuration,
    TransportListenerBindings bindingFactory,
            out ApplicationDescription serverDescription,
            out EndpointDescriptionCollection endpoints)
    {
        serverDescription = null;
        endpoints = null;

        var hosts = new Dictionary<string, ServiceHost>();

        // ensure at least one security policy exists.
        if (configuration.ServerConfiguration.SecurityPolicies.Count == 0)
        {
            configuration.ServerConfiguration.SecurityPolicies.Add(new ServerSecurityPolicy());
        }

        // ensure at least one user token policy exists.
        if (configuration.ServerConfiguration.UserTokenPolicies.Count == 0)
        {
            UserTokenPolicy userTokenPolicy = new UserTokenPolicy();

            userTokenPolicy.TokenType = UserTokenType.Anonymous;
            userTokenPolicy.PolicyId = userTokenPolicy.TokenType.ToString();

            configuration.ServerConfiguration.UserTokenPolicies.Add(userTokenPolicy);
        }

        // set server description.
        serverDescription = new ApplicationDescription
        {
            ApplicationUri = configuration.ApplicationUri,
            ApplicationName = new LocalizedText("en-US", configuration.ApplicationName),
            ApplicationType = configuration.ApplicationType,
            ProductUri = configuration.ProductUri,
            DiscoveryUrls = GetDiscoveryUrls()
        };

        endpoints = new EndpointDescriptionCollection();
        IList<EndpointDescription> endpointsForHost = null;

        var baseAddresses = configuration.ServerConfiguration.BaseAddresses;
        var requiredSchemes = Utils.DefaultUriSchemes.Where(scheme => baseAddresses.Any(a => a.StartsWith(scheme, StringComparison.Ordinal)));

        foreach (var scheme in requiredSchemes)
        {
            var binding = bindingFactory.GetBinding(scheme);
            if (binding != null)
            {
                endpointsForHost = binding.CreateServiceHost(
                    this,
                    hosts,
                    configuration,
                    configuration.ServerConfiguration.BaseAddresses,
                    serverDescription,
                    configuration.ServerConfiguration.SecurityPolicies,
                    InstanceCertificate,
                    InstanceCertificateChain
                    );
                endpoints.AddRange(endpointsForHost);
            }
        }

        return new List<ServiceHost>(hosts.Values);
    }


    /// <summary>
    /// Creates an instance of the service host.
    /// </summary>
    public override ServiceHost CreateServiceHost(ServerBase server, params Uri[] addresses)
    {
        return new ServiceHost(this, typeof(SessionEndpoint), addresses);
    }

    protected override EndpointBase GetEndpointInstance(ServerBase server)
    {
        return new DiscoveryEndpoint(server);
    }
}
