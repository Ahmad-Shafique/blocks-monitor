using Blocks.Genesis;
using Blocks.Secrets;
using Worker;
using Worker.Configuration;
using DomainService.Shared.Entity;
using MonitoringWorker.Consumers;
using DomainService.Shared.Utilities;

const string serviceName = "blocks-monitor-worker";

var vaultType = ApplicationConfigurations.ResolveVaultType();
var secret = await ApplicationConfigurations.ConfigureLogAndSecretsAsync(serviceName, vaultType);

await CreateHostBuilder(args).Build().RunAsync();

IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, builder) =>
        {
            // ApplicationConfigurations.ConfigureWorkerEnv(builder, args);
            // Genesis loader (I07-WP3): KeyValueStores first, then the legacy Secrets document.
            builder.AddKeyValueConfiguration(options =>
            {
                options.ConnectionString = secret.DatabaseConnectionString;
                options.DatabaseName = secret.RootDatabaseName;
                options.Key = "blocks-secret-monitor";
            });
        })
        .ConfigureServices((services) =>
        {
            services.AddHttpClient();

            services.Configure<VerioSystemSettings>(services.BuildServiceProvider().GetRequiredService<IConfiguration>().GetSection("VerioSystemSettings"));



            services.AddHostedService<PeriodicPingBackgroundService>();
            services.AddHostedService<MonitorSchedulerBackgroundWorker>();




            #region Identifier Service Consumers
            Alert.DomainService.ServiceRegistry.AddApplicationServices(services);
            // Alert mail reads root mail-configuration passwords from Blocks Secrets (blocks-monitor#253).
            services.AddBlocksSecrets();
            services.AddSingleton<IConsumer<MonitorConfigurationUpdateQueue>, MonitorConfigurationUpdateConsumer>();

            ApplicationConfigurations.ConfigureWorker(services, MonitorConstants.GetWorkerMessageConfiguration(secret.MessageConnectionString));
            //ApplicationConfigurations.ConfigureWorker(services, IdentifierConstants.GetMessageConfiguration(secret.MessageConnectionString));
            #endregion
        });

