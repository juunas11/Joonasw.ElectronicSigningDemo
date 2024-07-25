using System;
using System.Collections.Generic;

namespace Joonasw.ElectronicSigningDemo.Data;

public class SigningRequest
{
    public Guid Id { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public string DocumentName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? WorkflowStartedAt { get; set; }
    public DateTimeOffset? WorkflowCompletedAt { get; set; }

    public WorkflowInstance Workflow { get; set; } = new WorkflowInstance();

    public ICollection<Signer> Signers { get; set; }
}