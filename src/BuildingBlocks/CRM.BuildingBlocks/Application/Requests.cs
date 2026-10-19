namespace CRM.BuildingBlocks.Application;

/// <summary>A request dispatched through <see cref="ISender"/> to exactly one handler.</summary>
public interface IRequest<out TResponse>;

/// <summary>A request that changes state.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A state-changing request without a result.</summary>
public interface ICommand : ICommand<Unit>;

/// <summary>A read-only request.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>"No result" value for commands.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Wraps every request of the matching type (cross-cutting concerns: validation, logging...).
/// Behaviors run in registration order, outermost first.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
