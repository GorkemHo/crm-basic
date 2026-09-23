using CRM.Core.Common;
using CRM.Core.DTOs.CustomerDtos;
using CRM.Core.Mappings;
using CRM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _customerService.GetAllAsync();

        return Ok(customers.Select(x => x.ToDto()));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _customerService.GetByIdAsync(id);

        if(customer is null)
        {
            return NotFound();
        }

        return Ok(customer.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerDto request)
    {
        var customer = request.ToEntity();

        var createdCustomer =
            await _customerService.CreateAsync(customer);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdCustomer.Id },
            createdCustomer.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
    int id,
    [FromBody] UpdateCustomerDto request)
    {
        if(id != request.Id)
        {
            return BadRequest();
        }

        var customer = await _customerService.UpdateAsync(id, request);

        if(customer is null)
        {
            return NotFound();
        }

        return Ok(customer.ToDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _customerService.DeleteAsync(id);

        if(!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request)
    {
        var result = await _customerService
            .GetPagedAsync(request);

        return Ok(result);
    }

    [HttpGet("filtered")]
    public async Task<IActionResult> GetFiltered([FromQuery] CustomerFilterRequest request)
    {
        var result = await _customerService
            .GetFilteredAsync(request);

        return Ok(result);
    }
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(
    int id,
    [FromBody] UpdateCustomerPatchDto request)
    {
        var customer = await _customerService
            .PatchAsync(id, request);

        if(customer is null)
        {
            return NotFound();
        }

        return Ok(customer.ToDto());
    }
}