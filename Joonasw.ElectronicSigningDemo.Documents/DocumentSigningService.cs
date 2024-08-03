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
        PdfDocument unsignedPdf = PdfReader.Open(unsignedPdfStream, PdfDocumentOpenMode.Import);
        // Copy pages from unsigned PDF to signed PDF
        for (int pageNum = 0; pageNum < unsignedPdf.PageCount; pageNum++)
        {
            pdf.AddPage(unsignedPdf.Pages[pageNum]);
        }

        AddPageWithSignerInfo(pdf, results);

        await using Stream signedPdfStream = await _blobStorageService.OpenWriteAsync(requestId, DocumentType.Signed);

        pdf.Save(signedPdfStream, closeStream: false);
    }

    private void AddPageWithSignerInfo(PdfDocument pdf, SignerResult[] results)
    {
        PdfPage page = pdf.AddPage();
        page.Size = PageSize.A4;

        var gfx = XGraphics.FromPdfPage(page);
        var headingFont = new XFont("Arial", 32, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 16, XFontStyleEx.Regular);
        var textFormatter = new XTextFormatter(gfx);

        var heading = "Signed by users";
        var body = string.Join("\n", results.Select(r => $"{r.SignerEmail} signed at {r.DecidedAt:dd.MM.yyyy HH:mm K}"));

        // Layout:
        // +--------------------------------+
        // |          (40px margin)         |
        // |  Signed by users (40px high)   |
        // |          (16px spacing)        |
        // |  a@a.com signed at X           |
        // |  b@b.com signed at Y           |
        // |  ...                           |
        // |          (40px margin)         |
        // +--------------------------------+
        // (40 px margins on left and right too)

        const int Margin = 40;
        const int HeadingHeight = 40;
        const int HeadingBodySpacing = 16;
        double pageWidth = page.Width.Point;
        double pageHeight = page.Height.Point;

        var headingRect = new XRect(
            x: Margin,
            y: Margin,
            width: pageWidth - Margin - Margin,
            height: HeadingHeight);
        var bodyRect = new XRect(
            x: Margin,
            y: Margin + HeadingHeight + HeadingBodySpacing,
            width: pageWidth - Margin - Margin,
            height: pageHeight - Margin - Margin - HeadingHeight - HeadingBodySpacing);

        gfx.DrawRectangle(XBrushes.Transparent, headingRect);
        textFormatter.DrawString(heading, headingFont, XBrushes.Black, headingRect);

        gfx.DrawRectangle(XBrushes.Transparent, bodyRect);
        textFormatter.DrawString(body, bodyFont, XBrushes.Black, bodyRect);
    }
}
