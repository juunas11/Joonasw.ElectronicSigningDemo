using System;

namespace Joonasw.ElectronicSigningDemo.WorkflowModels
{
    public class SignerResult
    {
        public string SignerEmail { get; set; }
        public SigningDecision Result { get; set; }
        public DateTimeOffset DecidedAt { get; set; }
    }
}