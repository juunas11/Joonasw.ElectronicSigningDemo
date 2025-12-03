using System.Text.Json.Serialization;

namespace Joonasw.ElectronicSigningDemo.Web.Pages;

internal class DurableFunctionsCheckStatusResponse
{
    [JsonPropertyName("Id")]
    public string Id { get; set; }
    [JsonPropertyName("StatusQueryGetUri")]
    public string StatusQueryGetUri { get; set; }
    [JsonPropertyName("SendEventPostUri")]
    public string SendEventPostUri { get; set; }
    [JsonPropertyName("TerminatePostUri")]
    public string TerminatePostUri { get; set; }
    [JsonPropertyName("PurgeHistoryDeleteUri")]
    public string PurgeHistoryDeleteUri { get; set; }
}
