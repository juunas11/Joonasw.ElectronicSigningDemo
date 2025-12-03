using Azure.Storage.Blobs;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Emulator;
using DurableTask.Netherite;
using DurableTask.ServiceBus;
using DurableTask.ServiceBus.Settings;
using DurableTask.ServiceBus.Tracking;
using DurableTask.SqlServer;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.DurableTask.Activities;
using Joonasw.ElectronicSigningDemo.DurableTask.Orchestrations;
using Joonasw.ElectronicSigningDemo.DurableTask.Services;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PdfSharp.Fonts;

namespace Joonasw.ElectronicSigningDemo.DurableTask;

internal class Program
{
    static async Task Main(string[] args)
    {
        // You can also host the worker with Microsoft.Extensions.Hosting

        var config = CreateConfiguration();
        var sp = CreateServiceProvider(config);

        var orchestrationService = CreateOrchestrationService(
            Backend.AzureDurableTaskScheduler, config, sp);

        await orchestrationService.CreateIfNotExistsAsync();

        var worker = new TaskHubWorker(orchestrationService)
            .AddTaskOrchestrations(
                typeof(SigningOrchestration),
                typeof(WaitForSignOrchestration))
            .AddTaskActivities(
                new CreateSignedDocument(sp),
                new MarkWorkflowCompleted(sp),
                new MarkWorkflowStarted(sp),
                new SendCompletionEmail(sp),
                new SendPleaseSignEmail(sp),
                new SetEventOrchestratorInfo(sp),
                new SetSigningDataForSigner(sp));

        await worker.StartAsync();

        Console.WriteLine("Worker started. Press Enter to start demo.");
        Console.ReadLine();

        // Upload demo document, start orchestration
        using var serviceScope = sp.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<SigningDbContext>();

        var signingRequest = await UploadDemoDocumentAsync(serviceScope, db);

        var client = new TaskHubClient((IOrchestrationServiceClient)orchestrationService);

        await StartDemoOrchestrationAsync(
            client,
            signingRequest);

        Console.WriteLine("Demo started.");

        foreach (var signer in signingRequest.Signers)
        {
            Console.Write($"{signer.Email} - approve? y/n: ");
            var approved = Console.ReadLine() == "y";

            await SendEventAsync(client, db, signer, approved);
        }

        Console.WriteLine("Events raised. Press Enter to stop worker.");
        Console.ReadLine();
        await worker.StopAsync();
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .Build();
    }

    private static IServiceProvider CreateServiceProvider(IConfiguration config)
    {
        var dbConnectionString = config["ConnectionStrings:Sql"] ?? throw new Exception("ConnectionStrings:Sql missing from config");
        var containerName = config["Storage:ContainerName"] ?? throw new Exception("Storage:ContainerName missing from config");
        var storageConnectionString = config["Storage:ConnectionString"] ?? throw new Exception("Storage:ConnectionString missing from config");
        var sendGridApiKey = config["SendGridKey"] ?? throw new Exception("SendGridKey missing from config");
        var sendGridFromAddress = config["FromEmail"] ?? throw new Exception("FromEmail missing from config");

        GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        var services = new ServiceCollection()
            .AddSingleton(config)
            .AddDbContext<SigningDbContext>(o =>
            {
                o.UseSqlServer(dbConnectionString);
            })
            .AddSingleton<DocumentSigningService>()
            .AddSingleton(sp =>
            {
                BlobServiceClient blobServiceClient = sp.GetRequiredService<BlobServiceClient>();

                return new BlobStorageService(blobServiceClient, containerName);
            })
            .AddSingleton(sp =>
            {
                var client = new SendGrid.SendGridClient(sendGridApiKey);
                return new SendGridEmailService(client, sendGridFromAddress);
            })
            .AddLogging(log =>
            {
                log.AddConsole();
                log.AddFilter("Azure.Core", LogLevel.Warning);
                log.AddFilter("Azure.Messaging.ServiceBus", LogLevel.Warning);
            });

        services.AddAzureClients(clients =>
        {
            clients.AddBlobServiceClient(storageConnectionString);
        });

        return services.BuildServiceProvider();
    }

    private static IOrchestrationService CreateOrchestrationService(
        Backend backend,
        IConfiguration config,
        IServiceProvider sp)
    {
        var taskHubName = "ESigningDurableTask";

        var storageConnectionString = config["Storage:ConnectionString"] ?? throw new Exception("Storage:ConnectionString missing from config");

        var netheriteSettings = new NetheriteOrchestrationServiceSettings
        {
            HubName = taskHubName,
            PartitionCount = 4,
            StorageConnectionName = "Storage",
            EventHubsConnectionName = "EventHubs"
        };
        netheriteSettings.Validate((name) =>
        {
            return name switch
            {
                "Storage" => storageConnectionString,
                "EventHubs" => config["EventHubsConnectionString"] ?? throw new Exception("EventHubsConnectionString missing from config"),
                _ => throw new Exception($"Unknown connection name {name}"),
            };
        });

        return backend switch
        {
            Backend.AzureStorage => new AzureStorageOrchestrationService(new AzureStorageOrchestrationServiceSettings
            {
                PartitionCount = 4,
                TaskHubName = "ESigningDurableTask",
                StorageAccountClientProvider = new StorageAccountClientProvider(storageConnectionString),
                LoggerFactory = sp.GetRequiredService<ILoggerFactory>()
            }),
            Backend.Emulator => new LocalOrchestrationService(),
            Backend.SqlServer => new SqlOrchestrationService(
                new SqlOrchestrationServiceSettings(
                    connectionString: config["ConnectionStrings:DurableTaskSql"] ?? throw new Exception("SqlConnectionString missing from config"),
                    taskHubName: taskHubName,
                    schemaName: "dbo")
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    CreateDatabaseIfNotExists = true
                }),
            Backend.Netherite => new NetheriteOrchestrationService(netheriteSettings, sp.GetRequiredService<ILoggerFactory>()),
            Backend.AzureDurableTaskScheduler => new AzureManagedOrchestrationService(
                AzureManagedOrchestrationServiceOptions.FromConnectionString(config["AzureDurableTaskSchedulerConnectionString"] ?? throw new Exception("AzureDurableTaskSchedulerConnectionString missing from config")),
                sp.GetRequiredService<ILoggerFactory>()),
            Backend.ServiceBus => new ServiceBusOrchestrationService(
                ServiceBusConnectionSettings.Create(config["ServiceBusConnectionString"] ?? throw new Exception("ServiceBusConnectionString missing from config")),
                taskHubName + "sbus",
                new AzureTableInstanceStore(taskHubName + "sbus", storageConnectionString),
                new AzureStorageBlobStore(taskHubName + "sbus", storageConnectionString),
                new ServiceBusOrchestrationServiceSettings
                {
                }),
            _ => throw new NotSupportedException($"Backend {backend} is not implemented.")
        };
    }

    private static async Task<SigningRequest> UploadDemoDocumentAsync(
        IServiceScope serviceScope,
        SigningDbContext db)
    {
        var config = serviceScope.ServiceProvider.GetRequiredService<IConfiguration>();
        var blobStorageService = serviceScope.ServiceProvider.GetRequiredService<BlobStorageService>();

        var signerEmailsSetting = config["Demo:SignerEmails"] ?? throw new Exception("Demo:SignerEmails missing from config");
        var demoFilePath = config["Demo:FilePath"] ?? throw new Exception("Demo:FilePath missing from config");

        var id = Guid.NewGuid();
        string[] signerEmails = signerEmailsSetting
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        var req = new SigningRequest
        {
            Id = id,
            Subject = "DurableTask Demo",
            Message = "Please sign this",
            DocumentName = $"{Guid.NewGuid()}.pdf",
            Signers = signerEmails
                .Select(email => new Signer
                {
                    Email = email.Trim()
                })
                .ToList()
        };
        db.Requests.Add(req);

        await using Stream stream = File.OpenRead(demoFilePath);
        await blobStorageService.UploadAsync(req.Id, DocumentType.Unsigned, stream);

        await db.SaveChangesAsync();
        return req;
    }

    private static async Task StartDemoOrchestrationAsync(
        TaskHubClient client,
        SigningRequest req)
    {
        var model = new WorkflowStartModel
        {
            RequestId = req.Id,
            DocumentName = req.DocumentName,
            Message = req.Message,
            SignerEmails = req.Signers.Select(s => s.Email).ToArray(),
            Subject = req.Subject
        };

        await client.CreateOrchestrationInstanceAsync(
            typeof(SigningOrchestration),
            Guid.NewGuid().ToString(),
            model);
    }

    private static async Task SendEventAsync(
        TaskHubClient client,
        SigningDbContext db,
        Signer signer,
        bool approved)
    {
        // Get instance ID from DB

        var instance = new OrchestrationInstance
        {
            InstanceId = await db.Signers
                .Where(s => s.Id == signer.Id)
                .Select(s => s.WaitForSignatureInstanceId)
                .SingleAsync()
        };

        await client.RaiseEventAsync(instance, "SignEvent", new SigningEvent
        {
            Email = signer.Email,
            Signed = approved,
            DecidedAt = DateTimeOffset.UtcNow
        });
    }

    private enum Backend
    {
        AzureDurableTaskScheduler,
        AzureStorage,
        Emulator,
        Netherite,
        ServiceBus,
        SqlServer,
    }
}
