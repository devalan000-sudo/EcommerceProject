using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/admin/dashboard"), Authorize(Roles = "Admin")]
public class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet]
    public Task<DashboardResponse> Get([FromQuery] DashboardQuery query, CancellationToken ct) => service.GetAsync(query, ct);
}
