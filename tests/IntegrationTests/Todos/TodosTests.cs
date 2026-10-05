using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests.Todos;

public sealed class TodosTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TodoDto(Guid Id, Guid UserId, string Description, string? Notes, bool IsCompleted);
    private sealed record PagedTodosDto(
        List<TodoDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    [Fact]
    public async Task GetTodo_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTodo_Should_PersistTodo_ThatCanBeRetrievedById()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Integration test todo",
            notes = "Integration test note",
            labels = new[] { "integration" },
            priority = 2
        };

        // Act
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);

        // Assert
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        todoId.ShouldNotBe(Guid.Empty);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();

        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.Id.ShouldBe(todoId);
        todo.UserId.ShouldBe(userId);
        todo.Description.ShouldBe("Integration test todo");
        todo.Notes.ShouldBe("Integration test note");
        todo.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateTodo_Should_PreserveNotes_WhenNotesIsOmitted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Todo with a note",
            notes = "Keep this note",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync(
            $"todos/{todoId}",
            new { description = "Updated description" });

        // Assert
        updateResponse.EnsureSuccessStatusCode();

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();
        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.Description.ShouldBe("Updated description");
        todo.Notes.ShouldBe("Keep this note");
    }

    [Fact]
    public async Task GetTodos_Should_FilterByCompletionStatus_WhenIsCompletedIsSpecified()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var completedTodoRequest = new
        {
            userId,
            description = "Completed todo",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage completedCreateResponse =
            await HttpClient.PostAsJsonAsync("todos", completedTodoRequest);
        completedCreateResponse.EnsureSuccessStatusCode();
        Guid completedTodoId = await completedCreateResponse.Content.ReadFromJsonAsync<Guid>();

        var incompleteTodoRequest = new
        {
            userId,
            description = "Incomplete todo",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage incompleteCreateResponse =
            await HttpClient.PostAsJsonAsync("todos", incompleteTodoRequest);
        incompleteCreateResponse.EnsureSuccessStatusCode();
        Guid incompleteTodoId = await incompleteCreateResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage completeResponse =
            await HttpClient.PutAsync($"todos/{completedTodoId}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage allResponse = await HttpClient.GetAsync($"todos?userId={userId}");
        HttpResponseMessage secondPageResponse =
            await HttpClient.GetAsync($"todos?userId={userId}&page=2&pageSize=1");
        HttpResponseMessage completedResponse =
            await HttpClient.GetAsync($"todos?userId={userId}&isCompleted=true");
        HttpResponseMessage incompleteResponse =
            await HttpClient.GetAsync($"todos?userId={userId}&isCompleted=false");

        // Assert
        allResponse.EnsureSuccessStatusCode();
        secondPageResponse.EnsureSuccessStatusCode();
        completedResponse.EnsureSuccessStatusCode();
        incompleteResponse.EnsureSuccessStatusCode();

        PagedTodosDto? allTodos = await allResponse.Content.ReadFromJsonAsync<PagedTodosDto>();
        PagedTodosDto? secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PagedTodosDto>();
        PagedTodosDto? completedTodos = await completedResponse.Content.ReadFromJsonAsync<PagedTodosDto>();
        PagedTodosDto? incompleteTodos = await incompleteResponse.Content.ReadFromJsonAsync<PagedTodosDto>();

        allTodos!.Page.ShouldBe(1);
        allTodos.PageSize.ShouldBe(20);
        allTodos.TotalCount.ShouldBe(2);
        allTodos.TotalPages.ShouldBe(1);
        allTodos.Items.Count.ShouldBe(2);
        allTodos.Items.ShouldAllBe(todo => todo.UserId == userId);

        secondPage!.Page.ShouldBe(2);
        secondPage.PageSize.ShouldBe(1);
        secondPage.TotalCount.ShouldBe(2);
        secondPage.TotalPages.ShouldBe(2);
        secondPage.Items.Count.ShouldBe(1);

        completedTodos!.Items.Count.ShouldBe(1);
        completedTodos.TotalCount.ShouldBe(1);
        completedTodos.TotalPages.ShouldBe(1);
        completedTodos.Items.ShouldAllBe(todo => todo.UserId == userId && todo.IsCompleted);
        completedTodos.Items[0].Id.ShouldBe(completedTodoId);

        incompleteTodos!.Items.Count.ShouldBe(1);
        incompleteTodos.TotalCount.ShouldBe(1);
        incompleteTodos.TotalPages.ShouldBe(1);
        incompleteTodos.Items.ShouldAllBe(todo => todo.UserId == userId && !todo.IsCompleted);
        incompleteTodos.Items[0].Id.ShouldBe(incompleteTodoId);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task GetTodos_Should_ReturnBadRequest_WhenPaginationParameterIsInvalid(
        string queryString,
        string errorField)
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}&{queryString}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty(errorField, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteTodo_Should_MarkTodoAsCompleted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Todo to complete",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage completeResponse = await HttpClient.PutAsync($"todos/{todoId}/complete", null);

        // Assert
        completeResponse.EnsureSuccessStatusCode();

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();
        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.IsCompleted.ShouldBeTrue();
    }
}
