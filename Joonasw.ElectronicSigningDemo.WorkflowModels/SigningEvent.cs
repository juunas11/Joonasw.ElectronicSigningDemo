using System;

namespace Joonasw.ElectronicSigningDemo.WorkflowModels
{
    public class SigningEvent
    {
        public bool Signed { get; set; }
        public DateTimeOffset DecidedAt { get; set; }
        public string Email { get; set; }
    }
}
