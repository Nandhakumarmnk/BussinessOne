namespace ERP.Application.Common.Interfaces;

/// <summary>Abstracts the clock so handlers stay deterministic and testable.</summary>
public interface IDateTime
{
    DateTime UtcNow { get; }
}
