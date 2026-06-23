using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Time;

public class SystemDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
