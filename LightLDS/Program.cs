using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;

namespace LightLDS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting light OPC UA Local Discovery Server");

            ApplicationInstance application = new ApplicationInstance();

            try
            {

                var applicationConfiguration = new ApplicationConfiguration();

                applicationConfiguration.ApplicationName = "Light UA Local Discovery Server";
                applicationConfiguration.ApplicationUri = "urn:localhost:K47S:LocalDiscoveryServer";
                applicationConfiguration.ProductUri = "http://K47S/LocalDiscoveryServer";
                applicationConfiguration.ApplicationType = ApplicationType.DiscoveryServer;

                var securityConfiguration = new SecurityConfiguration();

                securityConfiguration.ApplicationCertificate = new CertificateIdentifier()
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%/LightLDS/PKI/own",
                    SubjectName = "CN=Local Discovery Server, O=K47S, DC=localhost",
                };

                securityConfiguration.TrustedIssuerCertificates = new CertificateTrustList()
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%/LightLDS/PKI/issuers",

                };

                securityConfiguration.TrustedPeerCertificates = new CertificateTrustList()
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%/LightLDS/PKI/trusted",

                };

                securityConfiguration.RejectedCertificateStore = new CertificateTrustList()
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%/LightLDS/PKI/rejected",

                };

                securityConfiguration.AutoAcceptUntrustedCertificates = true;
                securityConfiguration.RejectSHA1SignedCertificates = false;
                securityConfiguration.MinimumCertificateKeySize = 1024;



                applicationConfiguration.SecurityConfiguration = securityConfiguration;


                var serverConfiguration = new ServerConfiguration();

                serverConfiguration.BaseAddresses = new StringCollection(new List<string>() { "opc.tcp://localhost:4840" });

                serverConfiguration.SecurityPolicies = new ServerSecurityPolicyCollection(new List<ServerSecurityPolicy>() {

                    new ServerSecurityPolicy()
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None",
                    }
                });

                serverConfiguration.UserTokenPolicies = new UserTokenPolicyCollection(new List<UserTokenPolicy>() {
                    new UserTokenPolicy()
                    {
                        TokenType = UserTokenType.Anonymous,
                        SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None"
                    }
                });

                serverConfiguration.DiagnosticsEnabled = true;
                serverConfiguration.MaxSessionCount = 100;
                serverConfiguration.MinSessionTimeout = 10000;
                serverConfiguration.MaxSessionTimeout = 3600000;
                serverConfiguration.MaxBrowseContinuationPoints = 10;
                serverConfiguration.MaxQueryContinuationPoints = 10;
                serverConfiguration.MaxHistoryContinuationPoints = 100;
                serverConfiguration.MaxRequestAge = 600000;
                serverConfiguration.MinPublishingInterval = 100;
                serverConfiguration.MaxPublishingInterval = 3600000;
                serverConfiguration.PublishingResolution = 100;
                serverConfiguration.MaxSubscriptionLifetime = 3600000;
                serverConfiguration.MaxMessageQueueSize = 100;
                serverConfiguration.MaxNotificationQueueSize = 100;
                serverConfiguration.MaxNotificationsPerPublish = 1000;
                serverConfiguration.MinMetadataSamplingInterval = 1000;

                serverConfiguration.RegistrationEndpoint = new EndpointDescription()
                {
                    EndpointUrl = "opc.tcp://localhost:4840",
                    Server = new ApplicationDescription()
                    {
                        ApplicationUri = "opc.tcp://localhost:4840",
                        ApplicationType = ApplicationType.DiscoveryServer,
                        DiscoveryUrls = new StringCollection(new List<string>() { "opc.tcp://localhost:4840" })
                    },
                    SecurityMode = MessageSecurityMode.None
                };

                serverConfiguration.MaxRegistrationInterval = 0;

                applicationConfiguration.ServerConfiguration = serverConfiguration;


                var transportQuotaConfig = new TransportQuotas();


                applicationConfiguration.TransportQuotas = transportQuotaConfig;


                var traceConfiguration = new TraceConfiguration();

                traceConfiguration.OutputFilePath = "%CommonApplicationData%/LightLDS/Logs/LightDiscoveryServer.log.txt";
                traceConfiguration.DeleteOnLoad = true;

                traceConfiguration.TraceMasks = 0x7FFFFFFF;



                applicationConfiguration.TraceConfiguration = traceConfiguration;


                application.ApplicationConfiguration = applicationConfiguration;


                application.ApplicationConfiguration.Validate(ApplicationType.Server);

                // check the application certificate.
                application.CheckApplicationInstanceCertificate(false, 0).Wait();


                var server = new DiscoveryServerInstance();

                // start the server.
                application.Start(server).Wait();

                Console.WriteLine("Running! Press ENTER to exit.");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.ReadLine();
        }
    }



    

    
}
