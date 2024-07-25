using Azure.Storage.Blobs;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        string dbConnectionString = context.Configuration["ConnectionStrings__Sql"];
        services.AddDbContext<SigningDbContext>(o =>
        {
            o.UseSqlServer(dbConnectionString);
        }, ServiceLifetime.Transient);
        services.AddSingleton<DocumentSigningService>();
        services.AddSingleton(sp =>
        {
            BlobServiceClient blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
            string containerName = context.Configuration["Storage__ContainerName"];
            return new BlobStorageService(blobServiceClient, containerName);
        });
        services.AddAzureClients(clients =>
        {
            clients.AddBlobServiceClient("UseDevelopmentStorage=True");
        });
    })
    .Build();

host.Run();
