using ERP.Application.Common.Reporting;
using ERP.Infrastructure.Reporting;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Reporting;

public class ReportExporterTests
{
    private static ReportTable Sample() => new()
    {
        Title = "Expenses",
        Subtitle = "2026-06-01 to 2026-06-30",
        Columns = new[] { new ReportColumn("Date"), new ReportColumn("Description"), new ReportColumn("Amount", true) },
        Rows = new List<IReadOnlyList<string>>
        {
            new[] { "2026-06-23", "Diesel", "4,200.00" },
            new[] { "2026-06-24", "Maintenance", "600.00" }
        },
        TotalsRow = new[] { "Total", "", "4,800.00" }
    };

    [Fact]
    public void Pdf_export_produces_a_pdf()
    {
        var file = new ReportExporter().Export(Sample(), "pdf");
        file.Content.Length.Should().BeGreaterThan(0);
        file.ContentType.Should().Be("application/pdf");
        file.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public void Excel_export_produces_a_spreadsheet()
    {
        var file = new ReportExporter().Export(Sample(), "excel");
        file.Content.Length.Should().BeGreaterThan(0);
        file.ContentType.Should().Contain("spreadsheetml");
        file.FileName.Should().EndWith(".xlsx");
    }
}
