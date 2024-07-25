using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace Joonasw.ElectronicSigningDemo.Web.Pages;

public class IndexModel : PageModel
{
    private readonly SigningDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly string _workflowStartUrl;
    private readonly BlobStorageService _blobStorageService;

    public IndexModel(
        SigningDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        BlobStorageService blobStorageService)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient(HttpClients.Workflow);
        _workflowStartUrl = configuration["Workflow:StartUrl"];
        _blobStorageService = blobStorageService;
    }

    [BindProperty]
    public SendForSignModel Model { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!Model.Document.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Document", "Must send PDF file");
            return Page();
        }

        var id = Guid.NewGuid();
        string[] signerEmails = Model.SignerEmails
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        var req = new SigningRequest
        {
            Id = id,
            Subject = Model.Subject,
            Message = Model.Message,
            DocumentName = $"{Guid.NewGuid()}.pdf",
            Signers = signerEmails
                .Select(email => new Signer
                {
                    Email = email.Trim()
                })
                .ToList()
        };
        _db.Requests.Add(req);

        await UploadAttachmentAsync(req.Id);

        await _db.SaveChangesAsync();

        await StartWorkflowAsync(req, signerEmails);

        return RedirectToPage("Status", new { id = id.ToString() });
    }

    private async Task UploadAttachmentAsync(Guid requestId)
    {
        using (Stream stream = Model.Document.OpenReadStream())
        {
            await _blobStorageService.UploadAsync(requestId, DocumentType.Unsigned, stream);
        }
    }

    private async Task StartWorkflowAsync(SigningRequest req, string[] signerEmails)
    {
        var model = new WorkflowStartModel
        {
            RequestId = req.Id,
            DocumentName = req.DocumentName,
            Message = req.Message,
            SignerEmails = signerEmails,
            Subject = req.Subject
        };
        string json = JsonSerializer.Serialize(model);
        var startRequest = new HttpRequestMessage(HttpMethod.Post, _workflowStartUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        HttpResponseMessage startResponse = await _httpClient.SendAsync(startRequest);
        if (startResponse.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            throw new Exception("Unexpected start response: " + startResponse.StatusCode);
        }

        // Update info about the started workflow in DB
        // Allows us to query status, terminate it etc.
        DurableFunctionsCheckStatusResponse checkStatusResponse = JsonSerializer.Deserialize<DurableFunctionsCheckStatusResponse>(await startResponse.Content.ReadAsStringAsync());
        req.Workflow.Id = checkStatusResponse.Id;
        req.Workflow.StatusQueryUrl = checkStatusResponse.StatusQueryGetUri;
        req.Workflow.SendEventUrl = checkStatusResponse.SendEventPostUri;
        req.Workflow.TerminateUrl = checkStatusResponse.TerminatePostUri;
        req.Workflow.PurgeHistoryUrl = checkStatusResponse.PurgeHistoryDeleteUri;
        await _db.SaveChangesAsync();
    }
}
