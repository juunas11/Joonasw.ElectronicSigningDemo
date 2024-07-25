using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Joonasw.ElectronicSigningDemo.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Joonasw.ElectronicSigningDemo.Web;

public class StatusModel : PageModel
{
    private readonly SigningDbContext _db;
    private readonly HttpClient _httpClient;

    public StatusModel(
        SigningDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient(HttpClients.Workflow);
    }

    public Guid RequestId { get; private set; }
    [Display(Name = "Subject")]
    public string RequestSubject { get; private set; }
    [Display(Name = "Message")]
    public string RequestMessage { get; private set; }
    [Display(Name = "Document name")]
    public string DocumentName { get; private set; }
    [Display(Name = "Workflow started at")]
    [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm K}", ApplyFormatInEditMode = true)]
    public DateTimeOffset? WorkflowStartedAt { get; private set; }
    [Display(Name = "Workflow completed at")]
    [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm K}", ApplyFormatInEditMode = true)]
    public DateTimeOffset? WorkflowCompletedAt { get; private set; }
    public string WorkflowStatusResponse { get; private set; }
    public RequestStatus Status { get; private set; }
    public SignerModel[] Signers { get; private set; }

    public async Task OnGetAsync(Guid id)
    {
        SigningRequest request = await _db.Requests
            .Include(r => r.Signers)
            .SingleAsync(r => r.Id == id);

        RequestId = request.Id;
        RequestSubject = request.Subject;
        RequestMessage = request.Message;
        DocumentName = request.DocumentName;
        WorkflowStartedAt = request.WorkflowStartedAt;
        WorkflowCompletedAt = request.WorkflowCompletedAt;
        Signers = request.Signers.Select(s => new SignerModel
        {
            Email = s.Email,
            DecidedAt = s.DecidedAt,
            Signed = s.Signed
        }).ToArray();

        if (request.WorkflowStartedAt == null)
        {
            Status = RequestStatus.Creating;
        }
        else if (request.WorkflowCompletedAt == null)
        {
            Status = RequestStatus.WaitingForSignatures;
        }
        else if (request.Signers.All(s => s.Signed))
        {
            Status = RequestStatus.Completed;
        }
        else
        {
            Status = RequestStatus.Failed;
        }

        WorkflowStatusResponse = await GetWorkflowStatusAsync(request);
    }

    private async Task<string> GetWorkflowStatusAsync(SigningRequest request)
    {
        if (string.IsNullOrEmpty(request.Workflow?.StatusQueryUrl))
        {
            return "";
        }

        HttpResponseMessage res = await _httpClient.GetAsync(request.Workflow.StatusQueryUrl);
        if (res.StatusCode != HttpStatusCode.OK && res.StatusCode != HttpStatusCode.Accepted)
        {
            return $"Failed to get status, got status code: {(int)res.StatusCode}";
        }

        string json = await res.Content.ReadAsStringAsync();
        // Deserialize and serialize in indented form
        return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
    }
}

public class SignerModel
{
    public string Email { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public bool Signed { get; set; }
}

public enum RequestStatus
{
    Creating,
    WaitingForSignatures,
    Failed,
    Completed
}