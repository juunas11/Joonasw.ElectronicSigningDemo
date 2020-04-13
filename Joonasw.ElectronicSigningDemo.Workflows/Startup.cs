using Azure.Storage.Blobs;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(Joonasw.ElectronicSigningDemo.Workflows.Startup))]

namespace Joonasw.ElectronicSigningDemo.Workflows
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            string dbConnectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__Sql");
            builder.Services.AddDbContext<SigningDbContext>(o =>
            {
                o.UseSqlServer(dbConnectionString);
            }, ServiceLifetime.Transient);
            builder.Services.AddSingleton<DocumentSigningService>();
            builder.Services.AddSingleton(sp =>
            {
                BlobServiceClient blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
                string containerName = Environment.GetEnvironmentVariable("Storage__ContainerName");
                return new BlobStorageService(blobServiceClient, containerName);
            });
            builder.Services.AddAzureClients(clients =>
            {
                clients.AddBlobServiceClient("UseDevelopmentStorage=true");
            });
        }
    }
}
