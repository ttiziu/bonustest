using BonusTest.Application.Bonuses;
using Microsoft.AspNetCore.Mvc;

namespace BonusTest.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class BonusController(BonusService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] BonusFilter filter, CancellationToken ct) =>
        Ok(await service.ListAsync(filter, ct));

    [HttpGet("employee/{departmentCode}/{employeeNumber:int}")]
    public async Task<IActionResult> Employee(string departmentCode, int employeeNumber, CancellationToken ct) =>
        Ok(await service.FindEmployeeAsync(departmentCode, employeeNumber, ct));

    [HttpPost]
    public async Task<IActionResult> Register(RegisterBonusRequest request, [FromHeader(Name = "UserCode")] string? userCode, CancellationToken ct)
    {
        var bonusId = await service.RegisterAsync(request, userCode, ct);
        return StatusCode(StatusCodes.Status201Created, new { bonusId });
    }

    [HttpPut("{bonusId:int}/cancel")]
    public async Task<IActionResult> Cancel(int bonusId, [FromHeader(Name = "UserCode")] string? userCode, CancellationToken ct)
    {
        await service.CancelAsync(bonusId, userCode, ct);
        return NoContent();
    }

    [HttpDelete("{bonusId:int}")]
    public async Task<IActionResult> Delete(int bonusId, [FromHeader(Name = "UserCode")] string? userCode, CancellationToken ct)
    {
        await service.DeleteAsync(bonusId, userCode, ct);
        return NoContent();
    }
}
