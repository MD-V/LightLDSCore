using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightLDS
{
    public class DiscoveryServerInstance : DiscoveryServerBase
    {

        #region IDiscovery
        public override ResponseHeader FindServers(RequestHeader requestHeader, string endpointUrl, StringCollection localeIds, StringCollection serverUris, out ApplicationDescriptionCollection servers)
        {

            var serverDummy = new ApplicationDescriptionCollection(new List<ApplicationDescription>()
            {
                new ApplicationDescription()
                {
                    ApplicationName = "SoftingOpcUaDemoServer",
                    ApplicationUri = "opc.tcp://localhost:51510/UA/DemoServer",
                    ApplicationType = ApplicationType.Server,
                    ProductUri = "http://industrial.softing.com/OpcUaNetToolkit/OpcUaDemoServer",
                    DiscoveryUrls = new StringCollection(new List<string>(){
                            "opc.tcp://localhost:51510/UA/DemoServer"
                    }
                    ),
                },
                new ApplicationDescription()
                {
                    ApplicationName = "SoftingOpcUaDemoServer1",
                    ApplicationUri = "opc.tcp://localhost:51511/UA/DemoServer1",
                    ApplicationType = ApplicationType.Server,
                    ProductUri = "http://industrial.softing.com/OpcUaNetToolkit/OpcUaDemoServer1",
                    DiscoveryUrls = new StringCollection(new List<string>(){
                            "opc.tcp://localhost:51511/UA/DemoServer1"
                    }
                    ),
                }



            });

            servers = serverDummy;

            ResponseHeader responseHeader = new ResponseHeader();

            responseHeader.Timestamp = DateTime.UtcNow;
            responseHeader.RequestHandle = requestHeader.RequestHandle;
            responseHeader.ServiceResult = new StatusCode(StatusCodes.Good);


            return responseHeader;
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

            //ValidateRequest(requestHeader);


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
            return base.RegisterServer(requestHeader, server);
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
                    base.ServerDescription,
                    baseAddresses,
                    applicationName);

                // translate the endpoint descriptions.
                endpoints = TranslateEndpointDescriptions(
                    parsedEndpointUrl,
                    baseAddresses,
                    this.Endpoints,
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
#if !NO_HTTPS
            endpointsForHost = CreateHttpsServiceHost(
                hosts,
                configuration,
                configuration.ServerConfiguration.BaseAddresses,
                serverDescription,
                configuration.ServerConfiguration.SecurityPolicies);

            endpoints.AddRange(endpointsForHost);
#endif
            return new List<Task>(hosts.Values);
        }


        protected override EndpointBase GetEndpointInstance(ServerBase server)
        {
            return new DiscoveryEndpoint(server);
        }
    }
}
