using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Organization.Api.Controllers
{
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AuthController : ControllerBase
{
    [Route("info")]
    [HttpGet]
    public IActionResult Info()
    {
        return Ok(new
        {
            IsUserAuthenticated = User.Identity.IsAuthenticated,
            UserName = User.Identity.Name,
            Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }
}
}
