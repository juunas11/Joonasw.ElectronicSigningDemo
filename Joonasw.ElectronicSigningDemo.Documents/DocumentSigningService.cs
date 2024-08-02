using Joonasw.ElectronicSigningDemo.WorkflowModels;
using PdfSharp.Drawing.Layout;
using PdfSharp.Drawing;
using PdfSharp;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Joonasw.ElectronicSigningDemo.Documents;

public class DocumentSigningService
{
    private readonly BlobStorageService _blobStorageService;

    public DocumentSigningService(BlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    public async Task CreateSignedDocumentAsync(Guid requestId, SignerResult[] results)
    {
        await using Stream unsignedPdfStream = await _blobStorageService.DownloadAsync(requestId, DocumentType.Unsigned);

        var pdf = new PdfDocument();
        var unsignedPdf = PdfReader.Open(unsignedPdfStream, PdfDocumentOpenMode.Import);
        // Copy pages from unsigned PDF to signed PDF
        for (var pageNum = 0; pageNum < unsignedPdf.PageCount; pageNum++)
        {
            pdf.AddPage(unsignedPdf.Pages[pageNum]);
        }

        AddPageWithSignerInfo(pdf, results);

        await using var signedPdfStream = await _blobStorageService.OpenWriteAsync(requestId, DocumentType.Signed);

        pdf.Save(signedPdfStream, closeStream: false);
    }

    private void AddPageWithSignerInfo(PdfDocument pdf, SignerResult[] results)
    {
        var page = pdf.AddPage();
        page.Size = PageSize.A4;

        var gfx = XGraphics.FromPdfPage(page);
        var headingFont = new XFont("Arial", 32, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 16, XFontStyleEx.Regular);
        var textFormatter = new XTextFormatter(gfx);

        var heading = "Signed by users";
        var body = string.Join("\n", results.Select(r => $"{r.SignerEmail} signed at {r.DecidedAt:dd.MM.yyyy HH:mm K}"));

        var headingRect = new XRect(40, 40, page.Width.Point - 40, 40);
        var bodyRect = new XRect(40, 40 + 40 + 16, page.Width.Point - 40 - 40, page.Height.Point - 40 - 40 - 40 - 16);

        gfx.DrawRectangle(XBrushes.Transparent, headingRect);
        textFormatter.DrawString(heading, headingFont, XBrushes.Black, headingRect);

        gfx.DrawRectangle(XBrushes.Transparent, bodyRect);
        textFormatter.DrawString(body, bodyFont, XBrushes.Black, bodyRect);
    }
}
