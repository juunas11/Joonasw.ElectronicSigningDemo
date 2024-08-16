using Azure.Storage.Blobs;
using DurableFunctionsMonitor.DotNetIsolated;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerAppBuilder =>
    {
        workerAppBuilder.UseDurableFunctionsMonitor((settings, extensionPoints) =>
        {
            settings.Mode = DfmMode.ReadOnly;
            settings.DisableAuthentication = true;
        });
    })
    .ConfigureServices((context, services) =>
    {
        string dbConnectionString = context.Configuration["ConnectionStrings:Sql"];
        services.AddDbContext<SigningDbContext>(o =>
        {
            o.UseSqlServer(dbConnectionString);
        }, ServiceLifetime.Transient);
        services.AddSingleton<DocumentSigningService>();
        services.AddSingleton(sp =>
        {
            BlobServiceClient blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
            string containerName = context.Configuration["Storage:ContainerName"];
            return new BlobStorageService(blobServiceClient, containerName);
        });
        services.AddAzureClients(clients =>
        {
            clients.AddBlobServiceClient(context.Configuration["Storage:ConnectionString"]);
        });

        var sendGridApiKey = context.Configuration["SendGridKey"];
        var sendGridFromAddress = context.Configuration["FromEmail"];
        services.AddSingleton(sp =>
        {
            var client = new SendGrid.SendGridClient(sendGridApiKey);
            return new SendGridEmailService(client, sendGridFromAddress);
        });
    })
    .Build();

host.Run();
