using Application.Abstractions.Messaging;
using Application.Todos.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos", async (
            Guid userId,
            bool? isCompleted,
            IQueryHandler<GetTodosQuery, GetTodosResponse> handler,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = 20) =>
        {
            if (page < 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["page"] = ["Page must be greater than or equal to 1."]
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pageSize"] = ["Page size must be between 1 and 100."]
                });
            }

            var query = new GetTodosQuery(userId, isCompleted, page, pageSize);

            Result<GetTodosResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match<IResult>(() => Results.Ok(result.Value), CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
