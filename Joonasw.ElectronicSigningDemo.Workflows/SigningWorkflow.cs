using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Joonasw.ElectronicSigningDemo.Workflows;

public class SigningWorkflow
{
    private readonly SigningDbContext _db;
    private readonly DocumentSigningService _documentSigningService;
    private readonly SendGridEmailService _emailService;
    private readonly IConfiguration _configuration;

    public SigningWorkflow(
        SigningDbContext db,
        DocumentSigningService documentSigningService,
        SendGridEmailService emailService,
        IConfiguration configuration)
    {
        _db = db;
        _documentSigningService = documentSigningService;
        _emailService = emailService;
        _configuration = configuration;
    }

    [Function(nameof(StartSigningWorkflow))]
    public async Task<HttpResponseData> StartSigningWorkflow(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient starter,
        ILogger log)
    {
        WorkflowStartModel request = await req.ReadFromJsonAsync<WorkflowStartModel>();
        string instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(MainOrchestrator), request);

        log.LogInformation("Started orchestration with ID '{InstanceId}'.", instanceId);
        return starter.CreateCheckStatusResponse(req, instanceId);
    }

    [Function(nameof(MainOrchestrator))]
    public async Task<SignerResult[]> MainOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        WorkflowStartModel request = context.GetInput<WorkflowStartModel>();

        // Database update, has to be done in an activity
        await context.CallActivityAsync(nameof(MarkWorkflowStarted), request.RequestId);
        // there is also a variant that supports retries

        // Prepare email send tasks
        string[] signerEmails = request.SignerEmails;
        var emailSendTasks = new List<Task>(signerEmails.Length);
        for (int i = 0; i < signerEmails.Length; i++)
        {
            Task sendTask = context.CallActivityAsync(nameof(SendPleaseSignEmail), new EmailSendParameters
            {
                To = signerEmails[i],
                Subject = request.Subject,
                Message = request.Message,
                RequestId = request.RequestId,
                DocumentName = request.DocumentName
            });
            emailSendTasks.Add(sendTask);
        }

        // Fan out to send emails, activities triggered in parallel
        await Task.WhenAll(emailSendTasks);

        // Prepare parallel signing tasks
        var signingTasks = new List<Task<SignerResult>>(signerEmails.Length);
        for (int i = 0; i < signerEmails.Length; i++)
        {
            Task<SignerResult> signingTask = context.CallSubOrchestratorAsync<SignerResult>(nameof(WaitForSignature.WaitForSign), new WaitForSignParameters
            {
                SignerEmail = signerEmails[i],
                RequestId = request.RequestId
            });
            signingTasks.Add(signingTask);
        }

        // Wait for result from each signer, another fan out
        SignerResult[] results = await Task.WhenAll(signingTasks);

        // Create signed document if everyone signed
        if (results.All(r => r.Result == SigningDecision.Signed))
        {
            await context.CallActivityAsync(nameof(CreateSignedDocument), new CreateSignedDocumentParameters
            {
                RequestId = request.RequestId,
                Results = results
            });
        }

        // Send completion email to all signers
        var completionEmailSendTasks = new List<Task>(signerEmails.Length);
        for (int i = 0; i < signerEmails.Length; i++)
        {
            Task sendTask = context.CallActivityAsync(nameof(SendCompletionEmail), new SendCompletionEmailParameters
            {
                RequestId = request.RequestId,
                To = signerEmails[i],
                DocumentName = request.DocumentName
            });
            completionEmailSendTasks.Add(sendTask);
        }

        // Fan out to send completion emails
        await Task.WhenAll(completionEmailSendTasks);

        // Finally, mark the workflow completed in the DB
        await context.CallActivityAsync(nameof(MarkWorkflowCompleted), request.RequestId);

        return results;
    }

    [Function(nameof(MarkWorkflowStarted))]
    public async Task MarkWorkflowStarted(
        [ActivityTrigger] Guid requestId)
    {
        SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == requestId);
        request.WorkflowStartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    [Function(nameof(SendPleaseSignEmail))]
    public async Task SendPleaseSignEmail(
        [ActivityTrigger] EmailSendParameters parameters)
    {
        var subject = $"Please sign: {parameters.Subject}";
        string signLink = $"{_configuration["AppBaseUrl"]}/Sign/{parameters.RequestId}";
        signLink = QueryHelpers.AddQueryString(signLink, "email", parameters.To);
        var content =
$@"Hello,

You have been asked to sign a document: {parameters.DocumentName}.

Message from sender: {parameters.Message}

Link to sign: {signLink}";

        await _emailService.SendEmailAsync(parameters.To, subject, content);
//        var message = new SendGridMessage();
//        message.AddTo(parameters.To);
//        message.SetFrom(_configuration["FromEmail"]);
//        message.SetSubject($"Please sign: {parameters.Subject}");

//        string signLink = $"{_configuration["AppBaseUrl"]}/Sign/{parameters.RequestId}";
//        signLink = QueryHelpers.AddQueryString(signLink, "email", parameters.To);

//        message.AddContent("text/plain",
//$@"Hello,

//You have been asked to sign a document: {parameters.DocumentName}.

//Message from sender: {parameters.Message}

//Link to sign: {signLink}");

//        sendGridMessage = message;
    }

    [Function(nameof(CreateSignedDocument))]
    public async Task CreateSignedDocument([ActivityTrigger] CreateSignedDocumentParameters parameters)
    {
        await _documentSigningService.CreateSignedDocumentAsync(parameters.RequestId, parameters.Results);
    }

    [Function(nameof(SendCompletionEmail))]
    public async Task SendCompletionEmail(
        [ActivityTrigger] SendCompletionEmailParameters parameters)
    {
        var subject = $"Document {parameters.DocumentName} signing workflow completed";
        string statusLink = $"{_configuration["AppBaseUrl"]}/Status/{parameters.RequestId}";
        var content =
$@"Hello,

Signing workflow for the following document has completed: {parameters.DocumentName}.

Link to see status and download the signed document: {statusLink}";

        await _emailService.SendEmailAsync(parameters.To, subject, content);
//        var message = new SendGridMessage();
//        message.AddTo(parameters.To);
//        message.SetFrom(_configuration["FromEmail"]);
//        message.SetSubject($"Document {parameters.DocumentName} signing workflow completed");

//        string statusLink = $"{_configuration["AppBaseUrl"]}/Status/{parameters.RequestId}";

//        message.AddContent("text/plain",
//$@"Hello,

//Signing workflow for the following document has completed: {parameters.DocumentName}.

//Link to see status and download the signed document: {statusLink}");

//        sendGridMessage = message;
    }

    [Function(nameof(MarkWorkflowCompleted))]
    public async Task MarkWorkflowCompleted(
        [ActivityTrigger] Guid requestId)
    {
        SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == requestId);
        request.WorkflowCompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }
}

public class EmailSendParameters
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public Guid RequestId { get; set; }
    public string DocumentName { get; set; }
}

public class CreateSignedDocumentParameters
{
    public Guid RequestId { get; set; }
    public SignerResult[] Results { get; set; }
}

public class SendCompletionEmailParameters
{
    public Guid RequestId { get; set; }
    public string To { get; set; }
    public string DocumentName { get; set; }
}