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
            try
            {
                var response = await _authService.LoginAsync(request);

                return Ok(response);
            }
            catch(Exception)
            {
                throw new InvalidOperationException("Login Error");
            }

        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto request)
        {
            try
            {
                await _authService.RegisterAsync(request);

                return Ok(new
                {
                    Message = "User registered successfully."
                });
            }
            catch(Exception)
            {
                throw new InvalidOperationException("Register Error");
            }
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
