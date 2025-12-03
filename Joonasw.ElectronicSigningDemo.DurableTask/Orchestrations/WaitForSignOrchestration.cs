using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.DurableTask.Activities;
using Joonasw.ElectronicSigningDemo.WorkflowModels;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Orchestrations;

public class WaitForSignOrchestration
    : TaskOrchestration<SignerResult, WaitForSignParameters, SigningEvent, bool>
{
    private TaskCompletionSource<SigningEvent> _signingEventTcs = new();

    public override async Task<SignerResult> RunTask(
        OrchestrationContext context, WaitForSignParameters input)
    {
        // Update this sub-orchestrator data to DB so event can be sent to this instance
        // Note this could also be done differently;
        // We could define the instance id in the parent orchestrator and store it in DB there
        await context.ScheduleTask<bool>(typeof(SetEventOrchestratorInfo), new SetEventOrchestratorInfoParameters
        {
            InstanceId = context.OrchestrationInstance.InstanceId,
            RequestId = input.RequestId,
            SignerEmail = input.SignerEmail
        });

        // Wait for user to sign for 5 days
        // Note this does not actually make the function wait here for 5 days
        // The function is completely suspended until something happens
        // You can't put more than 6 days of wait time here though

        var timeoutCancellationSource = new CancellationTokenSource();
        var timeoutTask = context.CreateTimer(
            context.CurrentUtcDateTime.AddDays(5),
            true,
            timeoutCancellationSource.Token);
        var eventTask = _signingEventTcs.Task;
        var completedTask = await Task.WhenAny(timeoutTask, eventTask);
        if (completedTask == timeoutTask)
        {
            // User did not respond within 5 days
            return new SignerResult
            {
                Result = SigningDecision.Expired,
                DecidedAt = context.CurrentUtcDateTime,
                SignerEmail = input.SignerEmail
            };
            // Note above we get the current time from the context
            // This is important since orchestrators must be _deterministic_
            // DateTimeOffset.UtcNow is not deterministic since the value is different on each run
            // CurrentUtcDateTime is derived from the history table used by Durable Functions
        }

        // Cancel the timer since we got the event
        // Otherwise the orchestration will not end until the timer fires
        // (as there is an "open" task)
        timeoutCancellationSource.Cancel();

        SigningEvent ev = await eventTask;
        if (ev.Email != input.SignerEmail)
        {
            throw new Exception("Wrong signer");
        }

        // Update decision info to DB
        await context.ScheduleTask<bool>(typeof(SetSigningDataForSigner), new SetSigningDataForSignerParameters
        {
            RequestId = input.RequestId,
            SignerEmail = input.SignerEmail,
            Signed = ev.Signed,
            DecidedAt = ev.DecidedAt
        });

        return new SignerResult
        {
            Result = ev.Signed ? SigningDecision.Signed : SigningDecision.Rejected,
            SignerEmail = input.SignerEmail,
            DecidedAt = ev.DecidedAt
        };
    }

    public override void OnEvent(
        OrchestrationContext context, string name, SigningEvent input)
    {
        _signingEventTcs.SetResult(input);
    }
}

public class WaitForSignParameters
{
    public required string SignerEmail { get; set; }
    public required Guid RequestId { get; set; }
}
