using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Activities;

public class SetEventOrchestratorInfo(
    IServiceProvider serviceProvider) : AsyncTaskActivity<SetEventOrchestratorInfoParameters, bool>
{
    protected override async Task<bool> ExecuteAsync(
        TaskContext context, SetEventOrchestratorInfoParameters input)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SigningDbContext>();

        Signer signer = await db.Signers.SingleAsync(s =>
            s.Email == input.SignerEmail
            && s.RequestId == input.RequestId
            && s.WaitForSignatureInstanceId == null);

        signer.WaitForSignatureInstanceId = input.InstanceId;
        await db.SaveChangesAsync();
        return true;
    }
}

public class SetEventOrchestratorInfoParameters
{
    public required string InstanceId { get; set; }
    public required Guid RequestId { get; set; }
    public required string SignerEmail { get; set; }
}