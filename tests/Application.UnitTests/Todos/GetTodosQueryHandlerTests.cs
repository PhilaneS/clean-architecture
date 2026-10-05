using Application.Abstractions.Authentication;
using Application.Todos.Get;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Domain.Users;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class GetTodosQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnDefaultPageForUser_WhenPaginationIsNotSpecified()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTodosAsync(context);
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId);

        // Act
        Result<GetTodosResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(1);
        result.Value.PageSize.ShouldBe(20);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalPages.ShouldBe(1);
        result.Value.Items.Count.ShouldBe(3);
        result.Value.Items.ShouldAllBe(todo => todo.UserId == UserId);
        result.Value.Items.Select(todo => todo.Id).ShouldBe(
            [TodoId(3), TodoId(2), TodoId(1)], ignoreOrder: false);
    }

    [Fact]
    public async Task Handle_Should_ReturnRequestedPageWithMetadata()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTodosAsync(context);
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId, Page: 2, PageSize: 1);

        // Act
        Result<GetTodosResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(1);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalPages.ShouldBe(3);
        result.Value.Items.Select(todo => todo.Id).ShouldBe([TodoId(2)]);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyItems_WhenPageExceedsTotalPages()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTodosAsync(context);
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId, Page: 4, PageSize: 1);

        // Act
        Result<GetTodosResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalPages.ShouldBe(3);
    }

    [Theory]
    [InlineData(true, 1, 1)]
    [InlineData(false, 2, 2)]
    public async Task Handle_Should_ComposeCompletionFilterWithPagination(
        bool isCompleted,
        int expectedCount,
        int expectedTotalPages)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTodosAsync(context);
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId, isCompleted, Page: 1, PageSize: 1);

        // Act
        Result<GetTodosResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(expectedCount);
        result.Value.TotalPages.ShouldBe(expectedTotalPages);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items.ShouldAllBe(todo => todo.UserId == UserId && todo.IsCompleted == isCompleted);
    }

    [Fact]
    public async Task Handle_Should_ReturnZeroTotalPages_WhenNoTodosMatch()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetTodosQueryHandler handler = CreateHandler(context);
        var query = new GetTodosQuery(UserId, IsCompleted: true);

        // Act
        Result<GetTodosResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.TotalPages.ShouldBe(0);
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
        Result<GetTodosResponse> result = await handler.Handle(query, CancellationToken.None);

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

    private static Guid TodoId(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static async Task SeedTodosAsync(TestDbContext context)
    {
        DateTime firstCreated = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime secondCreated = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        context.TodoItems.AddRange(
            new TodoItem
            {
                Id = TodoId(1),
                UserId = UserId,
                Description = "Old incomplete todo",
                CreatedAt = firstCreated,
                IsCompleted = false,
                Priority = Priority.Normal
            },
            new TodoItem
            {
                Id = TodoId(2),
                UserId = UserId,
                Description = "Completed todo",
                CreatedAt = secondCreated,
                IsCompleted = true,
                Priority = Priority.Normal
            },
            new TodoItem
            {
                Id = TodoId(3),
                UserId = UserId,
                Description = "Newer incomplete todo",
                CreatedAt = secondCreated,
                IsCompleted = false,
                Priority = Priority.Normal
            },
            new TodoItem
            {
                Id = TodoId(4),
                UserId = Guid.NewGuid(),
                Description = "Another user's completed todo",
                CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                IsCompleted = true,
                Priority = Priority.Normal
            });

        await context.SaveChangesAsync();
    }
}
