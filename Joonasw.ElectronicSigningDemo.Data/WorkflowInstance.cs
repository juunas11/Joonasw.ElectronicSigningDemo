using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Joonasw.ElectronicSigningDemo.Data;

[Owned]
public class WorkflowInstance
{
    [MaxLength(64)]
    public string Id { get; set; }
    [MaxLength(512)]
    public string StatusQueryUrl { get; set; }
    [MaxLength(512)]
    public string SendEventUrl { get; set; }
    [MaxLength(512)]
    public string TerminateUrl { get; set; }
    [MaxLength(512)]
    public string PurgeHistoryUrl { get; set; }
}
