using Application.Abstractions.Authentication;
using Application.Todos.Get;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class GetTodosQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnAllTodosForUser_WhenCompletionFilterIsNotSpecified()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTodosAsync(context);
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId);

        // Act
        Result<List<TodoResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldAllBe(todo => todo.UserId == UserId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_Should_FilterTodosByCompletionStatus(bool isCompleted)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTodosAsync(context);
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId, isCompleted);

        // Act
        Result<List<TodoResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value.ShouldAllBe(todo => todo.UserId == UserId && todo.IsCompleted == isCompleted);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUserIdDoesNotMatchContext()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        var handler = new GetTodosQueryHandler(context, userContext);
        var query = new GetTodosQuery(UserId, true);

        // Act
        Result<List<TodoResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    private static GetTodosQueryHandler CreateHandler(TestDbContext context)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        return new GetTodosQueryHandler(context, userContext);
    }

    private static async Task SeedTodosAsync(TestDbContext context)
    {
        context.TodoItems.AddRange(
            new TodoItem
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Description = "Completed todo",
                IsCompleted = true,
                Priority = Priority.Normal
            },
            new TodoItem
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Description = "Incomplete todo",
                IsCompleted = false,
                Priority = Priority.Normal
            },
            new TodoItem
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Description = "Another user's completed todo",
                IsCompleted = true,
                Priority = Priority.Normal
            });

        await context.SaveChangesAsync();
    }
}
