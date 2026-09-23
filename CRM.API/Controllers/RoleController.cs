using CRM.Core.DTOs.RoleDtos;
using CRM.Core.Mappings;
using CRM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _roleService.GetAllAsync();

            var roleDtos = roles.Select(x => x.ToDto()).ToList();

            return Ok(roleDtos);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var role = await _roleService.GetByIdAsync(id);

            if(role is null)
            {
                return NotFound();
            }

            return Ok(role.ToDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(
        [FromBody] CreateRoleDto request)
        {
            var role = request.ToEntity();

            var createdRole =
            await _roleService.CreateAsync(role);

            return CreatedAtAction(
            nameof(GetById),
            new { id = createdRole.Id },
            createdRole.ToDto());
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateRoleDto request)
        {
            if(id != request.Id)
            {
                return BadRequest();
            }

            var role =
            await _roleService.UpdateAsync(id, request);

            if(role is null)
            {
                return NotFound();
            }

            return Ok(role.ToDto());
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted =
            await _roleService.DeleteAsync(id);

            if(!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
