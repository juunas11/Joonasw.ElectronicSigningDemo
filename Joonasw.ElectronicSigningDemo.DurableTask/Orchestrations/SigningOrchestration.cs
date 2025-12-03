using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.DurableTask.Activities;
using Joonasw.ElectronicSigningDemo.WorkflowModels;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Orchestrations;

public class SigningOrchestration : TaskOrchestration<SignerResult[], WorkflowStartModel>
{
    public override async Task<SignerResult[]> RunTask(
        OrchestrationContext context, WorkflowStartModel input)
    {
        // Database update, has to be done in an activity
        await context.ScheduleTask<bool>(typeof(MarkWorkflowStarted), input.RequestId);
        // there is also a variant that supports retries

        // Prepare email send tasks
        string[] signerEmails = input.SignerEmails;
        var emailSendTasks = new List<Task>(signerEmails.Length);
        for (int i = 0; i < signerEmails.Length; i++)
        {
            Task sendTask = context.ScheduleTask<bool>(typeof(SendPleaseSignEmail), new EmailSendParameters
            {
                To = signerEmails[i],
                Subject = input.Subject,
                Message = input.Message,
                RequestId = input.RequestId,
                DocumentName = input.DocumentName
            });
            emailSendTasks.Add(sendTask);
        }

        // Fan out to send emails, activities triggered in parallel
        await Task.WhenAll(emailSendTasks);

        // Prepare parallel signing tasks
        var signingTasks = new List<Task<SignerResult>>(signerEmails.Length);
        for (int i = 0; i < signerEmails.Length; i++)
        {
            Task<SignerResult> signingTask = context.CreateSubOrchestrationInstance<SignerResult>(typeof(WaitForSignOrchestration), new WaitForSignParameters
            {
                SignerEmail = signerEmails[i],
                RequestId = input.RequestId
            });
            signingTasks.Add(signingTask);
        }

        // Wait for result from each signer, another fan out
        SignerResult[] results = await Task.WhenAll(signingTasks);

        // Create signed document if everyone signed
        if (results.All(r => r.Result == SigningDecision.Signed))
        {
            await context.ScheduleTask<bool>(typeof(CreateSignedDocument), new CreateSignedDocumentParameters
            {
                RequestId = input.RequestId,
                Results = results
            });
        }

        // Send completion email to all signers
        var completionEmailSendTasks = new List<Task>(signerEmails.Length);
        for (int i = 0; i < signerEmails.Length; i++)
        {
            Task sendTask = context.ScheduleTask<bool>(typeof(SendCompletionEmail), new SendCompletionEmailParameters
            {
                RequestId = input.RequestId,
                To = signerEmails[i],
                DocumentName = input.DocumentName
            });
            completionEmailSendTasks.Add(sendTask);
        }

        // Fan out to send completion emails
        await Task.WhenAll(completionEmailSendTasks);

        // Finally, mark the workflow completed in the DB
        await context.ScheduleTask<bool>(typeof(MarkWorkflowCompleted), input.RequestId);

        return results;
    }
}
