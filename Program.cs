using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentryXDR.Services;
using SentryXDR.Services.Authentication;
using SentryXDR.Services.Workers;
using SentryXDR.Services.Storage;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        
        // Multi-tenant authentication
        services.AddSingleton<IMultiTenantAuthService, MultiTenantAuthService>();
        services.AddSingleton<IGraphServiceFactory, GraphServiceFactory>();
        
        // HTTP clients
        services.AddHttpClient<IMDEApiService, MDEApiService>();
        services.AddHttpClient<IMDOApiService, MDOApiService>();
        services.AddHttpClient<IMCASApiService, MCASApiService>();
        services.AddHttpClient<IMDIApiService, MDIApiService>();
        
        // Worker services
        services.AddScoped<IMDEWorkerService, MDEWorkerService>();
        services.AddScoped<IMDOWorkerService, MDOWorkerService>();
        services.AddScoped<IMCASWorkerService, MCASWorkerService>();
        services.AddScoped<IMDIWorkerService, MDIWorkerService>();
        services.AddScoped<IEntraIDWorkerService, EntraIDWorkerService>();
        services.AddScoped<IIntuneWorkerService, IntuneWorkerService>();
        services.AddScoped<IAzureWorkerService, AzureWorkerService>();
        
        // Storage services
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<IStorageService, StorageService>();
        
        // Blob Service Client
        services.AddSingleton(sp =>
        {
            var connectionString = configuration["Storage:ConnectionString"];
            return new BlobServiceClient(connectionString);
        });
        
        // Validation services
        services.AddSingleton<IRemediationValidator, RemediationValidator>();
        services.AddSingleton<ITenantConfigService, TenantConfigService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

host.Run();
