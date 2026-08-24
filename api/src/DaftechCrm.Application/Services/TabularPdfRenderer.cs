using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Renders a generic headers+rows table to a landscape A4 PDF, used by
/// TicketReportService.ExportPdfAsync for all six Reports-module exports.
/// Deliberately generic (no report-specific logic) since every report
/// needs the same "title, filter summary, table, page numbers" shape.
///
/// LICENSING NOTE: QuestPDF's Community license (which
/// DaftechCrm.Application.csproj pulls in) is free only for
/// companies/individuals under specific revenue and headcount thresholds
/// — see https://questpdf.com/license/. Confirm DAFTECH still qualifies
/// before this ships to production; if not, the Professional/Enterprise
/// license (or an alternative library) would be needed instead. This is a
/// business decision, not something this code can verify for you.
/// </summary>
public static class TabularPdfRenderer
{
    static TabularPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string BrandCharcoal = "#2B2B2B";
    private const string BrandBlue = "#1D4ED8";

    /// <summary>The DAFTECH triangle mark, identical to the in-app inline SVG logo.</summary>
    private const string BrandMarkSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100">
          <path d="M50 6 L69 40 Q57 33 43 36 Z" fill="#D92D20" />
          <path d="M38 32 L38 78 L8 78 Z" fill="#1D4ED8" />
          <path d="M62 36 L92 78 L56 78 Q66 60 62 36 Z" fill="#D92D20" />
        </svg>
        """;

    public static byte[] Render(string title, string[] headers, IReadOnlyList<string[]> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        // Brand mark drawn as vector SVG at render time — no
                        // bitmap file is embedded or pasted in, so it stays
                        // crisp at any zoom and needs no asset deployment.
                        row.ConstantItem(34).Height(34).Svg(BrandMarkSvg);

                        row.RelativeItem().PaddingLeft(8).Column(brand =>
                        {
                            brand.Item().Text("DAF-TECH").FontSize(13).Bold().LetterSpacing(0.12f).FontColor(BrandCharcoal);
                            brand.Item().Text("Computer Engineering").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantItem(260).AlignRight().Column(meta =>
                        {
                            meta.Item().AlignRight().Text(title).FontSize(14).Bold();
                            meta.Item().AlignRight().Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC · {rows.Count} row(s)")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    header.Item().PaddingTop(6).LineHorizontal(1).LineColor(BrandBlue);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers)
                            columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(h).Bold().FontSize(8.5f);
                        }
                        header.Cell().ColumnSpan((uint)headers.Length).PaddingTop(1).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                    });

                    var rowIndex = 0;
                    foreach (var row in rows)
                    {
                        var background = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        foreach (var cell in row)
                        {
                            table.Cell().Background(background).Padding(4).Text(cell ?? "").FontSize(8);
                        }
                        rowIndex++;
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
