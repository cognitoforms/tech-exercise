using QuizApp.Api.Persistence;

namespace QuizApp.Api.Quizzes;

/// <summary>A multiple choice quiz. The id doubles as the quiz's URL slug.</summary>
public sealed class Quiz : Document
{
    public required string Title { get; init; }

    public required IReadOnlyList<Question> Questions { get; init; }
}

public sealed record Question(string Id, string Text, IReadOnlyList<Choice> Choices);

public sealed record Choice(string Id, string Text);
