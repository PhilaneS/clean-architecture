using Application.Abstractions.Messaging;

namespace Application.Todos.Get;

public sealed record GetTodosQuery(Guid UserId, bool? IsCompleted = null) : IQuery<List<TodoResponse>>;
