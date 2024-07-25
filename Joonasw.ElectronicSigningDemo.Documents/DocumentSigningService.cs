using IronPdf;
using Joonasw.ElectronicSigningDemo.WorkflowModels;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Joonasw.ElectronicSigningDemo.Documents;

public class DocumentSigningService
{
    private readonly BlobStorageService _blobStorageService;

    public DocumentSigningService(BlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    // This class uses the IronPdf library to modify the PDF file
    // Production use of this library requires a license
    // See their website: https://ironpdf.com/licensing/
    // Development and testing use is allowed for free, though does add watermarks to the PDFs

    public async Task CreateSignedDocumentAsync(Guid requestId, SignerResult[] results)
    {
        await using Stream unsignedPdfStream = await _blobStorageService.DownloadAsync(requestId, DocumentType.Unsigned);

        // TODO: TEST IF THIS IS STILL TRUE
        // IronPDF is weird, it really wants a file or memory stream
        // A blob stream won't do
        var memoryStream = new MemoryStream();
        await unsignedPdfStream.CopyToAsync(memoryStream);
        var pdf = new PdfDocument(memoryStream);

        PdfDocument signaturesPdf = await GeneratePdfWithSignerInfoAsync(results);
        pdf.AppendPdf(signaturesPdf);

        await _blobStorageService.UploadAsync(requestId, DocumentType.Signed, pdf.Stream);
    }

    private async Task<PdfDocument> GeneratePdfWithSignerInfoAsync(SignerResult[] results)
    {
        string html = "<h1>Signed by users</h1>";
        foreach (SignerResult result in results)
        {
            html += $"<p>{WebUtility.HtmlEncode(result.SignerEmail)} signed at {result.DecidedAt:dd.MM.yyyy HH:mm K}</p>";
        }

        var renderer = new ChromePdfRenderer();
        return await renderer.RenderHtmlAsPdfAsync(html);
    }
}
