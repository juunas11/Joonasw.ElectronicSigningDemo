using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;

namespace Joonasw.ElectronicSigningDemo.Workflows
{
    public class SigningWorkflow
    {
        private readonly SigningDbContext _db;
        private readonly DocumentSigningService _documentSigningService;
        private readonly IConfiguration _configuration;

        public SigningWorkflow(
            SigningDbContext db,
            DocumentSigningService documentSigningService,
            IConfiguration configuration)
        {
            _db = db;
            _documentSigningService = documentSigningService;
            _configuration = configuration;
        }

        [FunctionName(nameof(StartSigningWorkflow))]
        public async Task<HttpResponseMessage> StartSigningWorkflow(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            WorkflowStartModel request = await req.Content.ReadAsAsync<WorkflowStartModel>();
            string instanceId = await starter.StartNewAsync(nameof(MainOrchestrator), request);

            log.LogInformation("Started orchestration with ID '{InstanceId}'.", instanceId);
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(MainOrchestrator))]
        public async Task<SignerResult[]> MainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
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

        [FunctionName(nameof(MarkWorkflowStarted))]
        public async Task MarkWorkflowStarted(
            [ActivityTrigger] Guid requestId)
        {
            SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == requestId);
            request.WorkflowStartedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        [FunctionName(nameof(SendPleaseSignEmail))]
        public void SendPleaseSignEmail(
            [ActivityTrigger] EmailSendParameters parameters,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage sendGridMessage)
        {
            var message = new SendGridMessage();
            message.AddTo(parameters.To);
            message.SetFrom(_configuration["FromEmail"]);
            message.SetSubject($"Please sign: {parameters.Subject}");

            string signLink = $"{_configuration["AppBaseUrl"]}/Sign/{parameters.RequestId}";
            signLink = QueryHelpers.AddQueryString(signLink, "email", parameters.To);

            message.AddContent("text/plain",
$@"Hello,

You have been asked to sign a document: {parameters.DocumentName}.

Message from sender: {parameters.Message}

Link to sign: {signLink}");

            sendGridMessage = message;
        }

        [FunctionName(nameof(CreateSignedDocument))]
        public async Task CreateSignedDocument([ActivityTrigger] CreateSignedDocumentParameters parameters)
        {
            await _documentSigningService.CreateSignedDocumentAsync(parameters.RequestId, parameters.Results);
        }

        [FunctionName(nameof(SendCompletionEmail))]
        public void SendCompletionEmail(
            [ActivityTrigger] SendCompletionEmailParameters parameters,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage sendGridMessage)
        {
            var message = new SendGridMessage();
            message.AddTo(parameters.To);
            message.SetFrom(_configuration["FromEmail"]);
            message.SetSubject($"Document {parameters.DocumentName} signing workflow completed");

            string statusLink = $"{_configuration["AppBaseUrl"]}/Status/{parameters.RequestId}";

            message.AddContent("text/plain",
$@"Hello,

Signing workflow for the following document has completed: {parameters.DocumentName}.

Link to see status and download the signed document: {statusLink}");

            sendGridMessage = message;
        }

        [FunctionName(nameof(MarkWorkflowCompleted))]
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
}