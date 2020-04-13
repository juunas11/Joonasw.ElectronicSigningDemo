using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;

namespace Joonasw.ElectronicSigningDemo.Workflows
{
    public class SigningWorkflow
    {
        private readonly SigningDbContext _db;
        private readonly DocumentSigningService _documentSigningService;
        private readonly IConfiguration _configuration;

        public SigningWorkflow(
            SigningDbContext db,
            DocumentSigningService documentSigningService,
            IConfiguration configuration)
        {
            _db = db;
            _documentSigningService = documentSigningService;
            _configuration = configuration;
        }



        [FunctionName(nameof(MainOrchestrator))]
        public async Task<SignerResult[]> MainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
        }


    }

    public class EmailSendParameters
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public Guid RequestId { get; set; }
        public string DocumentName { get; set; }
    }

    public class CreateSignedDocumentParameters
    {
        public Guid RequestId { get; set; }
        public SignerResult[] Results { get; set; }
    }

    public class SendCompletionEmailParameters
    {
        public Guid RequestId { get; set; }
        public string To { get; set; }
        public string DocumentName { get; set; }
    }
}