using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Todos.Get;

internal sealed class GetTodosQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ITodoSearch todoSearch)
    : IQueryHandler<GetTodosQuery, GetTodosResponse>
{
    public async Task<Result<GetTodosResponse>> Handle(GetTodosQuery query, CancellationToken cancellationToken)
    {
        if (query.UserId != userContext.UserId)
        {
            return Result.Failure<GetTodosResponse>(UserErrors.Unauthorized());
        }

        IQueryable<Domain.Todos.TodoItem> todoItems = context.TodoItems
            .Where(todoItem => todoItem.UserId == query.UserId);

        if (query.IsCompleted.HasValue)
        {
            todoItems = todoItems.Where(todoItem => todoItem.IsCompleted == query.IsCompleted.Value);
        }

        string? search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            todoItems = todoSearch.Apply(todoItems, search);
        }

        int totalCount = await todoItems.CountAsync(cancellationToken);
        int totalPages = totalCount / query.PageSize + (totalCount % query.PageSize == 0 ? 0 : 1);
        int skip = (int)Math.Min(((long)query.Page - 1) * query.PageSize, int.MaxValue);

        List<TodoResponse> todos = await todoItems
            .OrderByDescending(todoItem => todoItem.CreatedAt)
            .ThenByDescending(todoItem => todoItem.Id)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                UserId = todoItem.UserId,
                Description = todoItem.Description,
                Notes = todoItem.Notes,
                DueDate = todoItem.DueDate,
                Labels = todoItem.Labels,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt,
                CompletedAt = todoItem.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return new GetTodosResponse(todos, query.Page, query.PageSize, totalCount, totalPages);
    }
}
