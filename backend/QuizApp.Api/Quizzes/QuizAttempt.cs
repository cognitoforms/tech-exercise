using QuizApp.Api.Persistence;

namespace QuizApp.Api.Quizzes;

/// <summary>
/// A user's progress through a quiz. The id is the unguessable resume token that appears in the URL.
/// </summary>
public sealed class QuizAttempt : Document
{
    public required string QuizId { get; init; }

    /// <summary>Answers in the order they were given, which is always quiz question order.</summary>
    public List<RecordedAnswer> Answers { get; init; } = [];

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record RecordedAnswer(string QuestionId, string ChoiceId, DateTimeOffset AnsweredAt);
