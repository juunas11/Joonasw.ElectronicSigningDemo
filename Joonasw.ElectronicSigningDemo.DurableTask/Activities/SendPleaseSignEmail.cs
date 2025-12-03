using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.DurableTask.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Activities;

public class SendPleaseSignEmail(
    IServiceProvider serviceProvider) : AsyncTaskActivity<EmailSendParameters, bool>
{
    protected override async Task<bool> ExecuteAsync(
        TaskContext context, EmailSendParameters input)
    {
        using var scope = serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var emailService = scope.ServiceProvider.GetRequiredService<SendGridEmailService>();

        var subject = $"Please sign: {input.Subject}";
        var content =
$@"Hello,

You have been asked to sign a document: {input.DocumentName}.

Message from sender: {input.Message}";

        await emailService.SendEmailAsync(input.To, subject, content);
        return true;
    }
}

public class EmailSendParameters
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Message { get; set; }
    public required Guid RequestId { get; set; }
    public required string DocumentName { get; set; }
}