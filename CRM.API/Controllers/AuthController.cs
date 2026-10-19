using CRM.Core.DTOs;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request);

            return Ok(response);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto request)
        {
            await _authService.RegisterAsync(request);

            return Ok(new
            {
                Message = "User registered successfully."
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
        {
            var response = await _authService.RefreshAsync(request.RefreshToken);

            return Ok(response);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
        {
            await _authService.LogoutAsync(request.RefreshToken);

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminOnly()
        {
            return Ok(new
            {
                Message = "Welcome Admin"
            });
        }
    }
}
