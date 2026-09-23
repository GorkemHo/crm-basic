using CRM.Core.Common;
using CRM.Core.DTOs.CompanyDtos;
using CRM.Core.Mappings;
using CRM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "User")]
    public class CompaniesController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompaniesController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var companies = await _companyService.GetAllAsync();

            return Ok(companies);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var company = await _companyService.GetByIdAsync(id);

            if(company is null)
            {
                return NotFound();
            }

            return Ok(company);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCompanyDto request)
        {
            var company = request.ToEntity();

            var createdCompany = await _companyService.CreateAsync(company);

            return CreatedAtAction(nameof(GetById), new { id = createdCompany.Id }, createdCompany.ToDto());
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCompanyDto request)
        {
            if(id != request.Id)
            {
                return BadRequest();
            }

            var company = await _companyService.UpdateAsync(id, request);

            if(company is null)
            {
                return NotFound();
            }

            return Ok(company.ToDto());
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _companyService.DeleteAsync(id);

            if(!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request)
        {
            var result = await _companyService
                .GetPagedAsync(request);

            return Ok(result);
        }

        [HttpGet("filtered")]
        public async Task<IActionResult> GetFiltered([FromQuery] CompanyFilterRequest request)
        {
            var result = await _companyService
                .GetFilteredAsync(request);

            return Ok(result);
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> Patch(
    int id,
    [FromBody] UpdateCompanyPatchDto request)
        {
            var company = await _companyService
                .PatchAsync(id, request);

            if(company is null)
            {
                return NotFound();
            }

            return Ok(company.ToDto());
        }
    }
}
