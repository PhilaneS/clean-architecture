namespace Application.Todos.Get;

public sealed record GetTodosResponse(
    List<TodoResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
