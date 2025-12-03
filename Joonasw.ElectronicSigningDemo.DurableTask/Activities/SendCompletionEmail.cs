using Azure.Core;
using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.DurableTask.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Activities;

public class SendCompletionEmail(
    IServiceProvider serviceProvider) : AsyncTaskActivity<SendCompletionEmailParameters, bool>
{
    protected override async Task<bool> ExecuteAsync(
        TaskContext context, SendCompletionEmailParameters input)
    {
        using var scope = serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var emailService = scope.ServiceProvider.GetRequiredService<SendGridEmailService>();

        var subject = $"Document {input.DocumentName} signing workflow completed";
        var content =
$@"Hello,

Signing workflow for the following document has completed: {input.DocumentName}.";

        await emailService.SendEmailAsync(input.To, subject, content);
        return true;
    }
}

public class SendCompletionEmailParameters
{
    public required Guid RequestId { get; set; }
    public required string To { get; set; }
    public required string DocumentName { get; set; }
}
