using ERP.Application.Features.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class EmployeesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetEmployeesQuery(), ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetEmployeeQuery(id), ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateEmployeeCommand(
            id, body.Name, body.Mobile, body.Address, body.JoiningDate, body.Salary, body.Status), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteEmployeeCommand(id), ct));

    // ---- Salary ----

    [HttpGet("{id:guid}/salary")]
    public async Task<IActionResult> SalaryHistory(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetSalaryHistoryQuery(id), ct) });

    [HttpPost("{id:guid}/salary")]
    public async Task<IActionResult> RecordSalary(Guid id, [FromBody] RecordSalaryRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new RecordSalaryCommand(
            id, body.PeriodMonth, body.Amount, body.PaidAmount, body.PaidOn, body.Note), ct));

    [HttpGet("~/api/v1/reports/salary")]
    public async Task<IActionResult> MonthlySalaryReport([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetMonthlySalaryReportQuery(year, month), ct) });

    // ---- Attendance ----

    [HttpGet("{id:guid}/attendance")]
    public async Task<IActionResult> Attendance(Guid id, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetAttendanceQuery(id, year, month), ct) });

    [HttpPost("{id:guid}/attendance")]
    public async Task<IActionResult> MarkAttendance(Guid id, [FromBody] MarkAttendanceRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new MarkAttendanceCommand(id, body.AttendanceDate, body.Status), ct));
}

public record UpdateEmployeeRequest(
    string Name, string? Mobile, string? Address, DateOnly? JoiningDate, decimal Salary, string Status);
public record RecordSalaryRequest(DateOnly PeriodMonth, decimal Amount, decimal PaidAmount, DateOnly? PaidOn, string? Note);
public record MarkAttendanceRequest(DateOnly AttendanceDate, string Status);
