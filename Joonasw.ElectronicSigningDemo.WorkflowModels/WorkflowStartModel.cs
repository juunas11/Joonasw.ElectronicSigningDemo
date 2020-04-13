using System;

namespace Joonasw.ElectronicSigningDemo.WorkflowModels
{
    public class WorkflowStartModel
    {
        public Guid RequestId { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string DocumentName { get; set; }
        public string[] SignerEmails { get; set; }
    }
}
