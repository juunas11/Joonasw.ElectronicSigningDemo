using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.Extensions.DependencyInjection;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Activities;

public class CreateSignedDocument(
    IServiceProvider serviceProvider) : AsyncTaskActivity<CreateSignedDocumentParameters, bool>
{
    protected override async Task<bool> ExecuteAsync(
        TaskContext context, CreateSignedDocumentParameters input)
    {
        using var scope = serviceProvider.CreateScope();
        var documentSigningService = scope.ServiceProvider.GetRequiredService<DocumentSigningService>();

        await documentSigningService.CreateSignedDocumentAsync(input.RequestId, input.Results);
        return true;
    }
}

public class CreateSignedDocumentParameters
{
    public required Guid RequestId { get; set; }
    public required SignerResult[] Results { get; set; }
}