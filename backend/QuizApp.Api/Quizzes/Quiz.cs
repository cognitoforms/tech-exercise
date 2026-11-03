using QuizApp.Api.Persistence;

namespace QuizApp.Api.Quizzes;

/// <summary>A multiple choice quiz. The id doubles as the quiz's URL slug.</summary>
public sealed class Quiz : Document
{
    public required string Title { get; init; }

    public required IReadOnlyList<Question> Questions { get; init; }

    /// <summary>
    /// Optional time limit. The clock starts when the attempt is created (its first answer) and an attempt
    /// can't be answered after it expires. <c>null</c> means untimed.
    /// </summary>
    public int? TimeLimitSeconds { get; init; }
}

public sealed record Question(string Id, string Text, IReadOnlyList<Choice> Choices);

public sealed record Choice(string Id, string Text);
