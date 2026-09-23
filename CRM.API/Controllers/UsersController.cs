using CRM.Core.DTOs.RoleDtos;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Mappings;
using CRM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IUserRoleService _userRoleService;

        public UsersController(IUserService userService, IUserRoleService roleService)
        {
            _userService = userService;
            _userRoleService = roleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();

            return Ok(users.Select(x => x.ToDto()));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userService.GetByIdAsync(id);

            if(user is null)
            {
                return NotFound();
            }

            return Ok(user.ToDto());
        }

        [HttpGet("{email}")]
        public async Task<IActionResult> GetByEmail(string email)
        {
            var user = await _userService.GetByEmailAsync(email);

            if(user is null)
            {
                return NotFound();
            }

            return Ok(user.ToDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(
        [FromBody] CreateUserDto request)
        {
            var createdUser =
            await _userService.CreateAsync(request);

            return CreatedAtAction(
            nameof(GetById),
            new { id = createdUser.Id },
            createdUser.ToDto());
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateUserDto request)
        {
            if(id != request.Id)
            {
                return BadRequest();
            }

            var user =
            await _userService.UpdateAsync(id, request);

            if(user is null)
            {
                return NotFound();
            }

            return Ok(user.ToDto());
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> Patch(
   int id,
   [FromBody] UpdateUserPatchDto request)
        {
            var company = await _userService
                .PatchAsync(id, request);

            if(company is null)
            {
                return NotFound();
            }

            return Ok(company.ToDto());
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted =
            await _userService.DeleteAsync(id);

            if(!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        [HttpGet("{id:int}/roles")]
        public async Task<IActionResult> GetRoles(int id)
        {
            var roles = await _userRoleService.GetUserRolesAsync(id);

            return Ok(roles);
        }
        [HttpPost("{id:int}/roles")]
        public async Task<IActionResult> AssignRoles(int id, [FromBody] AssignRolesDto request)
        {
            await _userRoleService.AssignRolesAsync(id, request);

            return NoContent();
        }
    }
}
