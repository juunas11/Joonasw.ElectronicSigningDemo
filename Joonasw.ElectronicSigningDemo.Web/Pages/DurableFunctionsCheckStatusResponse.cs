using System.Text.Json.Serialization;

namespace Joonasw.ElectronicSigningDemo.Web.Pages
{
    internal class DurableFunctionsCheckStatusResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("statusQueryGetUri")]
        public string StatusQueryGetUri { get; set; }
        [JsonPropertyName("sendEventPostUri")]
        public string SendEventPostUri { get; set; }
        [JsonPropertyName("terminatePostUri")]
        public string TerminatePostUri { get; set; }
        [JsonPropertyName("purgeHistoryDeleteUri")]
        public string PurgeHistoryDeleteUri { get; set; }
    }
}
