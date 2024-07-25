using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Joonasw.ElectronicSigningDemo.Workflows;

public class WaitForSignature
{
    private const string SignEvent = nameof(SignEvent);
    private readonly SigningDbContext _db;

    public WaitForSignature(SigningDbContext db)
    {
        _db = db;
    }

    [Function(nameof(AddSignEvent))]
    public async Task<HttpResponseData> AddSignEvent(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient orchestrationClient,
        FunctionContext executionContext)
    {
        var log = executionContext.GetLogger<WaitForSignature>();
        AddSignEventModel model = await req.ReadFromJsonAsync<AddSignEventModel>();
        string instanceId = model.InstanceId;
        SigningEvent eventData = model.EventData;
        await orchestrationClient.RaiseEventAsync(instanceId, SignEvent, eventData);

        log.LogInformation("Sign event raised to instance {InstanceId}", instanceId);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function(nameof(WaitForSign))]
    public async Task<SignerResult> WaitForSign(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        WaitForSignParameters input = context.GetInput<WaitForSignParameters>();

        // Update this sub-orchestrator data to DB so event can be sent to this instance
        // Note this could also be done differently;
        // We could define the instance id in the parent orchestrator and store it in DB there
        await context.CallActivityAsync(nameof(SetEventOrchestratorInfo), new SetEventOrchestratorInfoParameters
        {
            InstanceId = context.InstanceId,
            RequestId = input.RequestId,
            SignerEmail = input.SignerEmail
        });

        try
        {
            // Wait for user to sign for 5 days
            // Note this does not actually make the function wait here for 5 days
            // The function is completely suspended until something happens
            // You can't put more than 6 days of wait time here though
            // Leaving out the timeout makes the wait _indefinite_
            SigningEvent ev = await context.WaitForExternalEvent<SigningEvent>(SignEvent, TimeSpan.FromDays(5));

            if (ev.Email != input.SignerEmail)
            {
                throw new Exception("Wrong signer");
            }

            // Update decision info to DB
            await context.CallActivityAsync(nameof(SetSigningDataForSigner), new SetSigningDataForSignerParameters
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

    [Function(nameof(SetEventOrchestratorInfo))]
    public async Task SetEventOrchestratorInfo(
        [ActivityTrigger] SetEventOrchestratorInfoParameters parameters)
    {
        Signer signer = await _db.Signers.SingleAsync(s =>
            s.Email == parameters.SignerEmail
            && s.RequestId == parameters.RequestId
            && s.WaitForSignatureInstanceId == null);

        signer.WaitForSignatureInstanceId = parameters.InstanceId;
        await _db.SaveChangesAsync();
    }

    [Function(nameof(SetSigningDataForSigner))]
    public async Task SetSigningDataForSigner(
        [ActivityTrigger] SetSigningDataForSignerParameters parameters)
    {
        Signer signer = await _db.Signers.SingleAsync(s =>
            s.Email == parameters.SignerEmail
            && s.RequestId == parameters.RequestId
            && s.DecidedAt == null);

        signer.DecidedAt = parameters.DecidedAt;
        signer.Signed = parameters.Signed;
        await _db.SaveChangesAsync();
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
