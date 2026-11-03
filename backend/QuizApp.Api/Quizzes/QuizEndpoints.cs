namespace QuizApp.Api.Quizzes;

public static class QuizEndpoints
{
    public static IEndpointRouteBuilder MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quizzes/{quizId}");

        // A quiz positioned at its first question. No attempt is persisted until the first answer is submitted.
        group.MapGet("/", async (string quizId, QuizService service, CancellationToken ct) =>
            ToHttpResult(await service.GetNewProgressAsync(quizId, ct)));

        // Answering the first question creates the attempt and returns its resume token.
        group.MapPost("/attempts", async (string quizId, AnswerRequest request, QuizService service, CancellationToken ct) =>
        {
            if (Validate(request) is { } invalid)
                return invalid;

            var result = await service.StartAsync(quizId, request.QuestionId!, request.ChoiceId!, ct);
            return result.Progress is { } progress
                ? Results.Created($"/api/quizzes/{quizId}/attempts/{progress.Token}", progress)
                : ToHttpResult(result);
        });

        group.MapGet("/attempts/{token}", async (string quizId, string token, QuizService service, CancellationToken ct) =>
            ToHttpResult(await service.GetProgressAsync(quizId, token, ct)));

        group.MapPost("/attempts/{token}/answers",
            async (string quizId, string token, AnswerRequest request, QuizService service, CancellationToken ct) =>
            {
                if (Validate(request) is { } invalid)
                    return invalid;

                return ToHttpResult(await service.AnswerAsync(quizId, token, request.QuestionId!, request.ChoiceId!, ct));
            });

        return app;
    }

    private static IResult? Validate(AnswerRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.QuestionId))
            errors[nameof(AnswerRequest.QuestionId)] = ["A question is required."];
        if (string.IsNullOrWhiteSpace(request.ChoiceId))
            errors[nameof(AnswerRequest.ChoiceId)] = ["An answer must be selected."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static IResult ToHttpResult(QuizResult result) => result.Error switch
    {
        null => Results.Ok(result.Progress),
        QuizError.QuizNotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Quiz not found."),
        QuizError.AttemptNotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Quiz progress not found. The link may be incorrect."),
        QuizError.QuestionNotCurrent => Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "This question has already been answered or is not the current question."),
        QuizError.TimeExpired => Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Time is up. This quiz's time limit has passed."),
        QuizError.InvalidChoice => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(AnswerRequest.ChoiceId)] = ["The selected answer is not valid for this question."],
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Error, null),
    };
}
