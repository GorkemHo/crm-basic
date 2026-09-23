using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers;

[Route("api/auth-test")]
[ApiController]
public class AuthTestController : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            Message = "You are authenticated.",
            User = User.Identity?.Name
        });
    }
}