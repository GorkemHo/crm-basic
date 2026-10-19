using CRM.BuildingBlocks.Application;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ValidationException = CRM.BuildingBlocks.Exceptions.ValidationException;

namespace CRM.UnitTests.BuildingBlocks;

public class DispatcherTests
{
    public sealed record Echo(string Text) : IQuery<string>;

    public sealed class EchoHandler : IRequestHandler<Echo, string>
    {
        public Task<string> Handle(Echo request, CancellationToken cancellationToken) => Task.FromResult(request.Text.ToUpperInvariant());
    }

    public sealed class EchoValidator : AbstractValidator<Echo>
    {
        public EchoValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(5);
    }

    private static ISender CreateSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequestHandlersFromAssembly(typeof(DispatcherTests).Assembly);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Send_RoutesToHandler()
    {
        Assert.Equal("HI", await CreateSender().Send(new Echo("hi")));
    }

    [Fact]
    public async Task Send_InvalidRequest_ThrowsValidationWithCamelCaseFields()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateSender().Send(new Echo("")));

        Assert.True(exception.Errors!.ContainsKey("text"));
    }

    [Fact]
    public async Task Send_UnregisteredRequest_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatcher();
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new Echo("x")));
    }
}
