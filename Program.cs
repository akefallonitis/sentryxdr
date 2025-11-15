using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentryXDR.Services;
using SentryXDR.Services.Authentication;
using SentryXDR.Services.Workers;
using SentryXDR.Services.Storage;
using Azure.Storage.Blobs;
using Azure.Data.Tables;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        
        // Multi-tenant authentication
        services.AddSingleton<IMultiTenantAuthService, MultiTenantAuthService>();
        
        // Managed Identity authentication
        services.AddSingleton<IManagedIdentityAuthService, ManagedIdentityAuthService>();
        
        services.AddSingleton<IGraphServiceFactory, GraphServiceFactory>();
        
        // HTTP clients for all services
        services.AddHttpClient<IMDEApiService, MDEApiService>();
        services.AddHttpClient<MDOApiService>();
        services.AddHttpClient<EntraIDApiService>();
        services.AddHttpClient<IntuneApiService>();
        services.AddHttpClient<IMCASApiService, MCASApiService>();
        services.AddHttpClient<IMDIApiService, MDIApiService>();
        
        // Advanced Hunting Service
        services.AddHttpClient<IAdvancedHuntingService, AdvancedHuntingService>();
        services.AddScoped<IAdvancedHuntingService, AdvancedHuntingService>();
        
        // Live Response Service
        services.AddHttpClient<ILiveResponseService, LiveResponseService>();
        services.AddScoped<ILiveResponseService, LiveResponseService>();
        
        // Azure Worker (NEW)
        services.AddHttpClient<IAzureWorkerService, AzureWorkerService>();
        services.AddScoped<IAzureWorkerService, AzureWorkerService>();
        
        // Worker services
        services.AddScoped<IMDEWorkerService, MDEWorkerService>();
        services.AddScoped<IMDOApiService, MDOApiService>();
        services.AddScoped<IMDOWorkerService>(sp => sp.GetRequiredService<IMDOApiService>() as IMDOWorkerService);
        services.AddScoped<IEntraIDWorkerService, EntraIDApiService>();
        services.AddScoped<IIntuneWorkerService, IntuneApiService>();
        services.AddScoped<IMCASApiService, MCASApiService>();
        services.AddScoped<IMDIWorkerService, MDIWorkerService>();
        
        // Storage services
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        
        // Blob Service Client - reads from Function App environment variable
        services.AddSingleton(sp =>
        {
            // Function Apps automatically provide AzureWebJobsStorage
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") 
                ?? configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException("AzureWebJobsStorage environment variable not found");
            return new BlobServiceClient(connectionString);
        });
        
        // Table Service Client - reads from Function App environment variable
        services.AddSingleton(sp =>
        {
            // Function Apps automatically provide AzureWebJobsStorage
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") 
                ?? configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException("AzureWebJobsStorage environment variable not found");
            return new TableServiceClient(connectionString);
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
