using Application.Abstractions.Data;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Todos;

internal sealed class PostgresTodoSearch : ITodoSearch
{
    private const string EscapeCharacter = "!";

    public IQueryable<TodoItem> Apply(IQueryable<TodoItem> todoItems, string search)
    {
        string escapedSearch = search
            .Replace(EscapeCharacter, "!!")
            .Replace("%", "!%")
            .Replace("_", "!_");
        string pattern = $"%{escapedSearch}%";

        return todoItems.Where(todoItem =>
            EF.Functions.ILike(todoItem.Description, pattern, EscapeCharacter) ||
            todoItem.Notes != null && EF.Functions.ILike(todoItem.Notes, pattern, EscapeCharacter));
    }
}
