using Joonasw.ElectronicSigningDemo.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Joonasw.ElectronicSigningDemo.Workflows;

public class Cleanup
{
    private readonly SigningDbContext _db;

    public Cleanup(SigningDbContext db)
    {
        _db = db;
    }

    [Function(nameof(CleanupSigningWorkflows))]
    public async Task CleanupSigningWorkflows(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        [DurableClient] DurableTaskClient orchestrationClient,
        FunctionContext executionContext)
    {
        var log = executionContext.GetLogger<Cleanup>();
        var signingRequests = await _db.Requests
            .Include(r => r.Signers)
            .Where(r => r.WorkflowCompletedAt != null
                && EF.Functions.DateDiffDay(r.WorkflowCompletedAt, DateTimeOffset.UtcNow) > 60)
            .ToListAsync();
        foreach (var signingRequest in signingRequests)
        {
            foreach (var signer in signingRequest.Signers)
            {
                var signerPurgeResult = await orchestrationClient.PurgeInstanceAsync(signer.WaitForSignatureInstanceId);
                log.LogInformation(
                    "Purged instance history for signer {SignerId}, {InstancesDeleted} instances deleted",
                    signer.Id, signerPurgeResult.PurgedInstanceCount);
            }

            var requestPurgeResult = await orchestrationClient.PurgeInstanceAsync(signingRequest.Workflow.Id);
            log.LogInformation(
                "Purged instance history for signing request {RequestId}, {InstancesDeleted} instances deleted",
                signingRequest.Id, requestPurgeResult.PurgedInstanceCount);
        }
    }

    [Function(nameof(CleanupOldWorkflows))]
    public async Task CleanupOldWorkflows(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        [DurableClient] DurableTaskClient orchestrationClient,
        FunctionContext executionContext)
    {
        var log = executionContext.GetLogger<Cleanup>();
        var createdTimeFrom = DateTime.UtcNow.Subtract(TimeSpan.FromDays(365 + 30));
        var createdTimeTo = createdTimeFrom.AddDays(30);
        var runtimeStatus = new List<OrchestrationRuntimeStatus>
        {
            OrchestrationRuntimeStatus.Completed
        };
        var result = await orchestrationClient.PurgeAllInstancesAsync(new PurgeInstancesFilter(createdTimeFrom, createdTimeTo, runtimeStatus));
        log.LogInformation("Scheduled cleanup done, {InstancesDeleted} instances deleted", result.PurgedInstanceCount);
    }
}
