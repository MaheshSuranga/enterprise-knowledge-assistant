using System.Text;
using EnterpriseKnowledgeAssistant.Application.DTOs;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EnterpriseKnowledgeAssistant.Infrastructure.Parsing;

public class PdfPigDocumentParser : IPdfParser
{
    public Task<ParsedDocument> ParsePdfAsync(Stream pdfStream, string filename, CancellationToken cancellationToken = default)
    {
        var pages = new List<ParsedPage>();

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            var textBlocks = new List<ParsedTextBlock>();

            // Extract page dimensions
            var pageWidth = page.Width;
            var pageHeight = page.Height;

            // Extract words and group into line blocks
            var words = page.GetWords().ToList();
            if (words.Count > 0)
            {
                // Group words by approximate vertical line position (within 3pt delta)
                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4.0) * 4.0)
                    .OrderByDescending(g => g.Key)
                    .ToList();

                foreach (var lineGroup in lines)
                {
                    var orderedWords = lineGroup.OrderBy(w => w.BoundingBox.Left).ToList();
                    var lineText = string.Join(" ", orderedWords.Select(w => w.Text));
                    if (string.IsNullOrWhiteSpace(lineText)) continue;

                    var minLeft = orderedWords.Min(w => w.BoundingBox.Left);
                    var maxRight = orderedWords.Max(w => w.BoundingBox.Right);
                    var minBottom = orderedWords.Min(w => w.BoundingBox.Bottom);
                    var maxTop = orderedWords.Max(w => w.BoundingBox.Top);

                    var firstWord = orderedWords.First();
                    var fontName = firstWord.FontName;
                    var fontSize = firstWord.Letters.Count > 0 ? (double)firstWord.Letters[0].PointSize : 11.0;

                    // Heuristic: line with font size >= 14 or bold naming is likely a section header
                    var isHeader = fontSize >= 13.0 || (fontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) ?? false);

                    // Convert to normalized coordinates (0.0 to 1.0)
                    var normBox = new BoundingBoxDto(
                        PageNumber: page.Number,
                        Left: Math.Round(minLeft / pageWidth, 4),
                        Top: Math.Round((pageHeight - maxTop) / pageHeight, 4), // Flip Y from bottom-left to top-left
                        Width: Math.Round((maxRight - minLeft) / pageWidth, 4),
                        Height: Math.Round((maxTop - minBottom) / pageHeight, 4)
                    );

                    textBlocks.Add(new ParsedTextBlock(
                        Text: lineText.Trim(),
                        BoundingBox: normBox,
                        FontName: fontName,
                        FontSize: fontSize,
                        IsHeader: isHeader
                    ));
                }
            }

            pages.Add(new ParsedPage(
                PageNumber: page.Number,
                Width: pageWidth,
                Height: pageHeight,
                TextBlocks: textBlocks
            ));
        }

        return Task.FromResult(new ParsedDocument(
            Filename: filename,
            PageCount: pages.Count,
            Pages: pages
        ));
    }
}
