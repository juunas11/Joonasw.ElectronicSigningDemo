using DurableTask.Core;
using Dynamitey.DynamicObjects;
using Joonasw.ElectronicSigningDemo.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Joonasw.ElectronicSigningDemo.Workflows
{
    public class Cleanup
    {
        private readonly SigningDbContext _db;

        public Cleanup(SigningDbContext db)
        {
            _db = db;
        }

        [FunctionName(nameof(CleanupSigningWorkflows))]
        public async Task CleanupSigningWorkflows(
            [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
            [DurableClient] IDurableOrchestrationClient orchestrationClient,
            ILogger log)
        {
            var signingRequests = await _db.Requests
                .Include(r => r.Signers)
                .Where(r => r.WorkflowCompletedAt != null
                    && EF.Functions.DateDiffDay(r.WorkflowCompletedAt, DateTimeOffset.UtcNow) > 60)
                .ToListAsync();
            foreach (var signingRequest in signingRequests)
            {
                foreach (var signer in signingRequest.Signers)
                {
                    var signerPurgeResult = await orchestrationClient.PurgeInstanceHistoryAsync(signer.WaitForSignatureInstanceId);
                    log.LogInformation(
                        "Purged instance history for signer {SignerId}, {InstancesDeleted} instances deleted",
                        signer.Id, signerPurgeResult.InstancesDeleted);
                }

                var requestPurgeResult = await orchestrationClient.PurgeInstanceHistoryAsync(signingRequest.Workflow.Id);
                log.LogInformation(
                    "Purged instance history for signing request {RequestId}, {InstancesDeleted} instances deleted",
                    signingRequest.Id, requestPurgeResult.InstancesDeleted);
            }
        }

        [FunctionName(nameof(CleanupOldWorkflows))]
        public async Task CleanupOldWorkflows(
            [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
            [DurableClient] IDurableOrchestrationClient orchestrationClient,
            ILogger log)
        {
            var createdTimeFrom = DateTime.UtcNow.Subtract(TimeSpan.FromDays(365 + 30));
            var createdTimeTo = createdTimeFrom.AddDays(30);
            var runtimeStatus = new List<OrchestrationStatus>
            {
                OrchestrationStatus.Completed
            };
            var result = await orchestrationClient.PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatus);
            log.LogInformation("Scheduled cleanup done, {InstancesDeleted} instances deleted", result.InstancesDeleted);
        }
    }
}
