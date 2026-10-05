using Application.Abstractions.Messaging;

namespace Application.Todos.Get;

public sealed record GetTodosQuery(
    Guid UserId,
    bool? IsCompleted = null,
    int Page = 1,
    int PageSize = 20) : IQuery<GetTodosResponse>;
