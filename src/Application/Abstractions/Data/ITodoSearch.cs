using Domain.Todos;

namespace Application.Abstractions.Data;

public interface ITodoSearch
{
    IQueryable<TodoItem> Apply(IQueryable<TodoItem> todoItems, string search);
}
