using Azure.Storage.Blobs;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Joonasw.ElectronicSigningDemo.Web;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorPages();
        services.AddControllers();
        services.AddHttpClient(HttpClients.Workflow);
        services.AddDbContext<SigningDbContext>(o =>
        {
            o.UseSqlServer(_configuration.GetConnectionString("Sql"));
        });
        services.AddSingleton(sp =>
        {
            BlobServiceClient blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
            string containerName = _configuration["Storage:ContainerName"];
            return new BlobStorageService(blobServiceClient, containerName);
        });
        services.AddAzureClients(clients =>
        {
            clients.AddBlobServiceClient(_configuration["Storage:ConnectionString"]);
        });
        services.AddApplicationInsightsTelemetry();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
        });
    }
}
