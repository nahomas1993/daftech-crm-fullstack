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

    public static byte[] Render(string title, string[] headers, IReadOnlyList<string[]> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(16).Bold();
                    col.Item().PaddingTop(2).Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC · {rows.Count} row(s)").FontSize(8).FontColor(Colors.Grey.Darken1);
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
