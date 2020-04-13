using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Joonasw.ElectronicSigningDemo.Web.Pages
{
    public class SignModel : PageModel
    {
        private readonly SigningDbContext _db;
        private readonly string _addSignEventUrl;
        private readonly HttpClient _httpClient;

        public SignModel(
            SigningDbContext db,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _addSignEventUrl = configuration["Workflow:AddSignEventUrl"];
            _httpClient = httpClientFactory.CreateClient(HttpClients.Workflow);
        }

        [BindProperty]
        public Guid RequestId { get; set; }
        [BindProperty]
        public string SignerEmail { get; set; }

        [Display(Name = "Subject")]
        public string RequestSubject { get; private set; }
        [Display(Name = "Message")]
        public string RequestMessage { get; private set; }
        [Display(Name = "Document")]
        public string DocumentName { get; private set; }
        public bool WorkflowReadyForEvent { get; private set; }

        public async Task<IActionResult> OnGetAsync(Guid id, [FromQuery] string email)
        {
            SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == id);
            Signer signer = await _db.Signers.SingleAsync(s => s.RequestId == id && s.Email == email);

            if (signer.DecidedAt != null)
            {
                // Signer has already given their decision
                return RedirectToPage("Status", new { id = id.ToString() });
            }

            RequestId = request.Id;
            SignerEmail = email;
            RequestSubject = request.Subject;
            RequestMessage = request.Message;
            DocumentName = request.DocumentName;
            WorkflowReadyForEvent = signer.WaitForSignatureInstanceId != null;
            return Page();
        }

        public Task<IActionResult> OnPostApproveAsync()
        {
            return HandlePostAsync(isApprove: true);
        }

        public Task<IActionResult> OnPostDeclineAsync()
        {
            return HandlePostAsync(isApprove: false);
        }

        private async Task<IActionResult> HandlePostAsync(bool isApprove)
        {
            SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == RequestId);
            Signer signer = await _db.Signers.SingleAsync(s => s.RequestId == RequestId && s.Email == SignerEmail);

            var model = new AddSignEventModel
            {
                InstanceId = signer.WaitForSignatureInstanceId,
                EventData = new SigningEvent
                {
                    DecidedAt = DateTimeOffset.UtcNow,
                    Email = SignerEmail,
                    Signed = isApprove
                }
            };
            string json = JsonSerializer.Serialize(model);
            var startRequest = new HttpRequestMessage(HttpMethod.Post, _addSignEventUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            HttpResponseMessage startResponse = await _httpClient.SendAsync(startRequest);
            if (startResponse.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                throw new Exception("Unexpected start response: " + startResponse.StatusCode);
            }

            return RedirectToPage("Status", new { id = RequestId.ToString() });
        }
    }
}