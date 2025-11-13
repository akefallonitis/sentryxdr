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
        
        // HTTP clients for all services
        services.AddHttpClient<IMDEApiService, MDEApiService>();
        services.AddHttpClient<MDOApiService>();  // MDOApiService is registered as itself
        services.AddHttpClient<EntraIDApiService>();  // EntraIDApiService is registered as itself
        services.AddHttpClient<IntuneApiService>();  // IntuneApiService is registered as itself
        services.AddHttpClient<IMCASApiService, MCASApiService>();
        services.AddHttpClient<IMDIApiService, MDIApiService>();
        services.AddHttpClient<IAzureApiService, AzureApiService>();
        
        // Worker services (these wrap the API services)
        services.AddScoped<IMDEWorkerService, MDEWorkerService>();
        services.AddScoped<IMDOApiService, MDOApiService>();  // Register as IMDOApiService
        services.AddScoped<IMDOWorkerService>(sp => sp.GetRequiredService<IMDOApiService>() as IMDOWorkerService);  // Alias to IMDOWorkerService
        services.AddScoped<IEntraIDWorkerService, EntraIDApiService>();  // EntraIDApiService implements IEntraIDWorkerService
        services.AddScoped<IIntuneWorkerService, IntuneApiService>();  // IntuneApiService implements IIntuneWorkerService
        services.AddScoped<IMCASWorkerService, MCASWorkerService>();
        services.AddScoped<IMDIWorkerService, MDIWorkerService>();
        services.AddScoped<IAzureWorkerService, AzureWorkerService>();
        
        // Storage services
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<IStorageService, StorageService>();
        
        // Blob Service Client
        services.AddSingleton(sp =>
        {
            var connectionString = configuration["Storage:ConnectionString"] 
                ?? configuration["AzureWebJobsStorage"];
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
