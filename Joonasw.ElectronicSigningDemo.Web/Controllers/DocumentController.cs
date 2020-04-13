using Joonasw.ElectronicSigningDemo.Data;
using Joonasw.ElectronicSigningDemo.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Joonasw.ElectronicSigningDemo.Web.Controllers
{
    [Route("document")]
    public class DocumentController : Controller
    {
        private readonly SigningDbContext _db;
        private readonly BlobStorageService _blobStorageService;

        public DocumentController(
            SigningDbContext db,
            BlobStorageService blobStorageService)
        {
            _db = db;
            _blobStorageService = blobStorageService;
        }

        // For any kind of production scenario, authentication would be needed here
        // As it stands only knowing the request id is enough to get access to the documents

        [HttpGet("download/signed/{requestId}")]
        public async Task<IActionResult> DownloadSignedAsync(Guid requestId)
        {
            SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == requestId);
            Stream stream = await _blobStorageService.DownloadAsync(requestId, DocumentType.Signed);
            return File(stream, "application/pdf", "Signed-" + request.DocumentName);
        }

        [HttpGet("download/unsigned/{requestId}")]
        public async Task<IActionResult> DownloadUnsignedAsync(Guid requestId)
        {
            SigningRequest request = await _db.Requests.SingleAsync(r => r.Id == requestId);
            Stream stream = await _blobStorageService.DownloadAsync(requestId, DocumentType.Unsigned);
            return File(stream, "application/pdf", request.DocumentName);
        }
    }
}
