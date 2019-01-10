using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Timers;

namespace LightLDS
{
    /// <summary>
    /// The server instance
    /// </summary>
    public class DiscoveryServerInstance : DiscoveryServerBase
    {
        private const string _CacheName = "LocalDiscoveryServerCache";
        private MemoryCache _Cache = new MemoryCache(_CacheName);
        private readonly CacheItemPolicy _CacheItemPolicy;

        private object _ServerLock = new object();

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
                servers = new ApplicationDescriptionCollection(_Cache.OfType<ApplicationDescription>());

                ResponseHeader responseHeader = new ResponseHeader();

                responseHeader.Timestamp = DateTime.UtcNow;
                responseHeader.RequestHandle = requestHeader.RequestHandle;
                responseHeader.ServiceResult = new StatusCode(StatusCodes.Good);

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
            endpoints = null;

            // filter by profile.
            IList<BaseAddress> baseAddresses = FilterByProfile(profileUris, BaseAddresses);

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
            return base.RegisterServer2(requestHeader, server, discoveryConfiguration, out configurationResults, out diagnosticInfos);
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
                    var newServer = new ApplicationDescription()
                    {
                        ApplicationName = server.ServerNames?.First(),
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
            EndpointDescriptionCollection endpoints = null;

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
        protected override IList<Task> InitializeServiceHosts(
            ApplicationConfiguration configuration,
            out ApplicationDescription serverDescription,
            out EndpointDescriptionCollection endpoints)
        {
            serverDescription = null;
            endpoints = null;

            Dictionary<string, Task> hosts = new Dictionary<string, Task>();

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
            serverDescription = new ApplicationDescription();

            serverDescription.ApplicationUri = configuration.ApplicationUri;
            serverDescription.ApplicationName = new LocalizedText("en-US", configuration.ApplicationName);
            serverDescription.ApplicationType = configuration.ApplicationType;
            serverDescription.ProductUri = configuration.ProductUri;
            serverDescription.DiscoveryUrls = GetDiscoveryUrls();

            endpoints = new EndpointDescriptionCollection();
            IList<EndpointDescription> endpointsForHost = null;

            // create UA TCP host.
            endpointsForHost = CreateUaTcpServiceHost(
                hosts,
                configuration,
                configuration.ServerConfiguration.BaseAddresses,
                serverDescription,
                configuration.ServerConfiguration.SecurityPolicies);

            endpoints.InsertRange(0, endpointsForHost);

            // create HTTPS host.
            endpointsForHost = CreateHttpsServiceHost(
                hosts,
                configuration,
                configuration.ServerConfiguration.BaseAddresses,
                serverDescription,
                configuration.ServerConfiguration.SecurityPolicies);

            endpoints.AddRange(endpointsForHost);
            return new List<Task>(hosts.Values);
        }

        protected override EndpointBase GetEndpointInstance(ServerBase server)
        {
            return new DiscoveryEndpoint(server);
        }
    }
}
