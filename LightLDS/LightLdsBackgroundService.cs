using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opc.Ua.Configuration;
using Opc.Ua;

namespace LightLDS;

public class LightLdsBackgroundService : BackgroundService
{
    private readonly ILogger<LightLdsBackgroundService> _Logger;
    private ApplicationInstance? _Application;
    private DiscoveryServerInstance? _Server;

    public LightLdsBackgroundService(ILogger<LightLdsBackgroundService> logger)
    {
        _Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_Logger.IsEnabled(LogLevel.Information))
            {
                _Logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await StartInternal(cancellationToken);
    }

    private async Task StartInternal(CancellationToken cancellationToken)
    {
        _Application = new ApplicationInstance
        {
            DisableCertificateAutoCreation = false,
            ApplicationConfiguration = new ApplicationConfiguration
            {
                ApplicationName = "Light OPC UA Local Discovery Server",
                ApplicationUri = "urn:localhost:MD-V:LocalDiscoveryServer",
                ProductUri = "http://MD-V/LocalDiscoveryServer",
                ApplicationType = ApplicationType.DiscoveryServer,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%/LightLDS/PKI/own",
                        SubjectName = "CN=Local Discovery Server, O=MD-V, DC=localhost",
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%/LightLDS/PKI/issuers",

                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%/LightLDS/PKI/trusted",

                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%/LightLDS/PKI/rejected",

                    },
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024
                },

                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection(new List<string>() { "opc.tcp://localhost:4840" }),
                    SecurityPolicies = new ServerSecurityPolicyCollection(new List<ServerSecurityPolicy>() {

                    new()
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None",
                    }
                }),
                    UserTokenPolicies = new UserTokenPolicyCollection(new List<UserTokenPolicy>() {
                    new()
                    {
                        TokenType = UserTokenType.Anonymous,
                        SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None"
                    }
                }),
                    DiagnosticsEnabled = true,
                    MaxSessionCount = 100,
                    MinSessionTimeout = 10000,
                    MaxSessionTimeout = 3600000,
                    MaxBrowseContinuationPoints = 10,
                    MaxQueryContinuationPoints = 10,
                    MaxHistoryContinuationPoints = 100,
                    MaxRequestAge = 600000,
                    MinPublishingInterval = 100,
                    MaxPublishingInterval = 3600000,
                    PublishingResolution = 100,
                    MaxSubscriptionLifetime = 3600000,
                    MaxMessageQueueSize = 100,
                    MaxNotificationQueueSize = 100,
                    MaxNotificationsPerPublish = 1000,
                    MinMetadataSamplingInterval = 1000
                },
                TransportQuotas = new TransportQuotas(),
                TraceConfiguration = new TraceConfiguration
                {
                    OutputFilePath = "%CommonApplicationData%/LightLDS/Logs/LocalDiscoveryServer.log.txt",
                    DeleteOnLoad = true,
                    TraceMasks = 0x7FFFFFFF
                },
            }
        };

        await _Application.ApplicationConfiguration.Validate(ApplicationType.Server);
        
        // check the application certificate.
        var result = await _Application.CheckApplicationInstanceCertificate(false, 0, 12, cancellationToken);

        // start the server.
        _Server = new DiscoveryServerInstance();
        await _Application.Start(_Server);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _Application?.Stop();
        _Server?.Stop();
        _Server?.Dispose();
        return Task.CompletedTask;
    }
}
