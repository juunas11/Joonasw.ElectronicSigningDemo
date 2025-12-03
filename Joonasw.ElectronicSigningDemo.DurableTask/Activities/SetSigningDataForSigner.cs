using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Activities;

public class SetSigningDataForSigner(
    IServiceProvider serviceProvider) : AsyncTaskActivity<SetSigningDataForSignerParameters, bool>
{
    protected override async Task<bool> ExecuteAsync(
        TaskContext context, SetSigningDataForSignerParameters input)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SigningDbContext>();

        Signer signer = await db.Signers.SingleAsync(s =>
            s.Email == input.SignerEmail
            && s.RequestId == input.RequestId
            && s.DecidedAt == null);

        signer.DecidedAt = input.DecidedAt;
        signer.Signed = input.Signed;
        await db.SaveChangesAsync();
        return true;
    }
}

public class SetSigningDataForSignerParameters
{
    public required Guid RequestId { get; set; }
    public required string SignerEmail { get; set; }
    public required bool Signed { get; set; }
    public required DateTimeOffset DecidedAt { get; set; }
}
