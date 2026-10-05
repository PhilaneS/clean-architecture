using Application.Abstractions.Authentication;
using Application.Todos.Update;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class UpdateTodoCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: Guid.NewGuid());

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotFound");
    }

    [Fact]
    public async Task Handle_Should_UpdateDescriptionAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: UserId);

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.Description.ShouldBe("Updated description");
        todoItem.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemUpdatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_UpdateNotes_WhenNotesIsProvided()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: UserId, notes: "Original note");

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description", "Updated note");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.Notes.ShouldBe("Updated note");
    }

    [Fact]
    public async Task Handle_Should_PreserveNotes_WhenNotesIsNull()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: UserId, notes: "Keep this note");

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description", null);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.Notes.ShouldBe("Keep this note");
    }

    [Fact]
    public async Task Handle_Should_ClearNotes_WhenNotesIsEmpty()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: UserId, notes: "Clear this note");

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description", string.Empty);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.Notes.ShouldBe(string.Empty);
    }

    private static async Task<Guid> SeedTodoAsync(TestDbContext context, Guid ownerId, string? notes = null)
    {
        var todoItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Description = "Original description",
            Notes = notes,
            Priority = Priority.Low,
            Labels = [],
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        return todoItem.Id;
    }
}
