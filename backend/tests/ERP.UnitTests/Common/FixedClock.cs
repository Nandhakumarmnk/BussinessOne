using ERP.Application.Common.Interfaces;

namespace ERP.UnitTests.Common;

/// <summary>Deterministic clock for tests.</summary>
public class FixedClock : IDateTime
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;
    public DateTime UtcNow { get; }
}
