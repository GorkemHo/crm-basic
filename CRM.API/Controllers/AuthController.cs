using CRM.Core.Exceptions;
using CRM.Core.DTOs;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Mappings;
using CRM.Core.Services;
using CRM.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        private readonly ICurrentUser _currentUser;

        public AuthController(IAuthService authService, IUserService userService, ICurrentUser currentUser)
        {
            _authService = authService;
            _userService = userService;
            _currentUser = currentUser;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = _currentUser.Id
                ?? throw new UnauthorizedException("The access token has no user id.", "auth.invalid_token");

            var user = await _userService.GetByIdAsync(userId)
                ?? throw new NotFoundException("User", userId);

            return Ok(user.ToDto());
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
    }
}
