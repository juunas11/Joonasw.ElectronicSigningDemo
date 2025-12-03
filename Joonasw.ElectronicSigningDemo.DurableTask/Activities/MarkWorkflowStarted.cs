using DurableTask.Core;
using Joonasw.ElectronicSigningDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Joonasw.ElectronicSigningDemo.DurableTask.Activities;

public class MarkWorkflowStarted(
    IServiceProvider serviceProvider) : AsyncTaskActivity<Guid, bool>
{
    protected override async Task<bool> ExecuteAsync(TaskContext context, Guid input)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SigningDbContext>();

        SigningRequest request = await db.Requests.SingleAsync(r => r.Id == input);
        request.WorkflowStartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }
}
