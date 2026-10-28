namespace QuizApp.Api.Quizzes;

// API contracts. Kept separate from the stored documents so the persistence shape can evolve independently
// and so the client only sees the question it is allowed to answer next.

public sealed record AnswerRequest(string? QuestionId, string? ChoiceId);

/// <summary>
/// Everything the client needs to render a quiz at its current position: either the next question to answer,
/// or (once every question is answered) the summary.
/// </summary>
public sealed record QuizProgress(
    string QuizId,
    string Title,
    /// <summary>Resume token; <c>null</c> until the first question has been answered.</summary>
    string? Token,
    int TotalQuestions,
    int AnsweredCount,
    QuestionView? CurrentQuestion,
    IReadOnlyList<SummaryItem>? Summary)
{
    public bool IsComplete => CurrentQuestion is null;
}

public sealed record QuestionView(string Id, int Number, string Text, IReadOnlyList<ChoiceView> Choices);

public sealed record ChoiceView(string Id, string Text);

public sealed record SummaryItem(int Number, string QuestionId, string QuestionText, string AnswerText);
