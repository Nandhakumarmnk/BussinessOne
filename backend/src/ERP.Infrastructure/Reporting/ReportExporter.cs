using ClosedXML.Excel;
using ERP.Application.Common.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace ERP.Infrastructure.Reporting;

/// <summary>Renders a <see cref="ReportTable"/> to PDF (QuestPDF) or Excel (ClosedXML).</summary>
public class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        // QuestPDF Community license (free for small businesses / <$1M revenue).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public ReportFile Export(ReportTable table, string format)
        => format == "excel" ? ToExcel(table) : ToPdf(table);

    private static ReportFile ToPdf(ReportTable t)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);

                page.Header().Column(col =>
                {
                    col.Item().Text(t.Title).FontSize(18).SemiBold();
                    if (t.Subtitle is { } s) col.Item().Text(s).FontSize(10).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(10).Table(tbl =>
                {
                    tbl.ColumnsDefinition(cols => { foreach (var _ in t.Columns) cols.RelativeColumn(); });

                    tbl.Header(header =>
                    {
                        foreach (var c in t.Columns)
                            header.Cell().PaddingVertical(4).BorderBottom(1).Text(c.Header).SemiBold();
                    });

                    foreach (var row in t.Rows)
                        foreach (var cell in row)
                            tbl.Cell().PaddingVertical(2).Text(cell);

                    if (t.TotalsRow is { } totals)
                        foreach (var cell in totals)
                            tbl.Cell().PaddingVertical(4).BorderTop(1).Text(cell).SemiBold();
                });

                page.Footer().AlignRight().Text(x => x.CurrentPageNumber());
            });
        }).GeneratePdf();

        return new ReportFile(bytes, "application/pdf", FileName(t.Title, "pdf"));
    }

    private static ReportFile ToExcel(ReportTable t)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(SheetName(t.Title));

        var row = 1;
        ws.Cell(row, 1).Value = t.Title;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        row++;
        if (t.Subtitle is { } s) { ws.Cell(row, 1).Value = s; row++; }
        row++;

        for (var c = 0; c < t.Columns.Count; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = t.Columns[c].Header;
            cell.Style.Font.Bold = true;
        }
        row++;

        foreach (var dataRow in t.Rows)
        {
            for (var c = 0; c < dataRow.Count; c++)
                ws.Cell(row, c + 1).Value = dataRow[c];
            row++;
        }

        if (t.TotalsRow is { } totals)
        {
            for (var c = 0; c < totals.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = totals[c];
                cell.Style.Font.Bold = true;
            }
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(t.Title, "xlsx"));
    }

    private static string SheetName(string title)
    {
        var clean = new string(title.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray());
        return clean.Length > 31 ? clean[..31] : (clean.Length == 0 ? "Report" : clean);
    }

    private static string FileName(string title, string ext)
        => $"{new string(title.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray())}.{ext}";
}
