namespace ERP.Application.Common.Reporting;

public record ReportColumn(string Header, bool Numeric = false);

/// <summary>A format-neutral tabular report. Cells are pre-formatted strings.</summary>
public class ReportTable
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public IReadOnlyList<ReportColumn> Columns { get; init; } = Array.Empty<ReportColumn>();
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();
    public IReadOnlyList<string>? TotalsRow { get; init; }
}

public record ReportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Renders a <see cref="ReportTable"/> to PDF or Excel (see Infrastructure).</summary>
public interface IReportExporter
{
    ReportFile Export(ReportTable table, string format);   // format: "pdf" | "excel"
}
