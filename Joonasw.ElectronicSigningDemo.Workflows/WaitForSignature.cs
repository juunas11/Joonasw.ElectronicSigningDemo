using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Joonasw.ElectronicSigningDemo.Workflows
{
    public class WaitForSignature
    {
        private const string SignEvent = nameof(SignEvent);
        private readonly SigningDbContext _db;

        public WaitForSignature(SigningDbContext db)
        {
            _db = db;
        }

        [FunctionName(nameof(WaitForSign))]
        public async Task<SignerResult> WaitForSign(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            WaitForSignParameters input = context.GetInput<WaitForSignParameters>();



            try
            {

            }
            catch (TimeoutException)
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
        }


    }

    public class WaitForSignParameters
    {
        public string SignerEmail { get; set; }
        public Guid RequestId { get; set; }
    }

    public class SetEventOrchestratorInfoParameters
    {
        public string InstanceId { get; set; }
        public Guid RequestId { get; set; }
        public string SignerEmail { get; set; }
    }

    public class SetSigningDataForSignerParameters
    {
        public Guid RequestId { get; set; }
        public string SignerEmail { get; set; }
        public bool Signed { get; set; }
        public DateTimeOffset DecidedAt { get; set; }
        public string AuthCode { get; set; }
    }
}
