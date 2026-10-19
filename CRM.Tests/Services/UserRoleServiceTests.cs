using System.Linq.Expressions;
using CRM.Core.DTOs.RoleDtos;
using CRM.Core.Entities;
using CRM.Core.Exceptions;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Services;
using NSubstitute;

namespace CRM.Tests.Services;

public class UserRoleServiceTests
{
    private readonly IUserRoleRepository _userRoles = Substitute.For<IUserRoleRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly UserRoleService _sut;

    public UserRoleServiceTests()
    {
        _sut = new UserRoleService(_userRoles, _users, _roles);
    }

    private void UserExists(bool exists) =>
        _users.ExistsAsync(Arg.Any<Expression<Func<User, bool>>>()).Returns(exists);

    private void RolesInDatabase(params int[] ids) =>
        _roles.FindAsync(Arg.Any<Expression<Func<Role, bool>>>())
            .Returns(ids.Select(id => new Role { Id = id, Name = $"R{id}" }));

    [Fact]
    public async Task Assign_UnknownUser_ThrowsNotFound()
    {
        UserExists(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.AssignRolesAsync(1, new AssignRolesDto { RoleIds = [1] }));
        await _userRoles.DidNotReceive().ReplaceUserRolesAsync(Arg.Any<int>(), Arg.Any<IEnumerable<int>>());
    }

    [Fact]
    public async Task Assign_UnknownRole_ThrowsNotFound_AndKeepsExistingRoles()
    {
        UserExists(true);
        RolesInDatabase(1);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.AssignRolesAsync(1, new AssignRolesDto { RoleIds = [1, 99] }));

        Assert.Contains("99", exception.Message);
        await _userRoles.DidNotReceive().ReplaceUserRolesAsync(Arg.Any<int>(), Arg.Any<IEnumerable<int>>());
    }

    [Fact]
    public async Task Assign_ValidRoles_ReplacesWithDistinctIds()
    {
        UserExists(true);
        RolesInDatabase(1, 2);

        await _sut.AssignRolesAsync(1, new AssignRolesDto { RoleIds = [1, 2, 2] });

        await _userRoles.Received(1).ReplaceUserRolesAsync(1, Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1, 2 })));
    }
}
