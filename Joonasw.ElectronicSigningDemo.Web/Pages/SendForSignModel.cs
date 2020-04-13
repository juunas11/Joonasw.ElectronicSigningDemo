using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Joonasw.ElectronicSigningDemo.Web.Pages
{
    public class SendForSignModel
    {
        [Required]
        public string Subject { get; set; }
        [Required]
        public string Message { get; set; }
        [Required]
        [Display(Name = "Document")]
        public IFormFile Document { get; set; }
        [Required]
        [Display(Name = "Signer email(s)")]
        public string SignerEmails { get; set; }
    }
}