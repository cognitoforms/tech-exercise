using System.Buffers.Text;
using System.Security.Cryptography;
using QuizApp.Api.Persistence;

namespace QuizApp.Api.Quizzes;

public enum QuizError
{
    QuizNotFound,
    AttemptNotFound,
    /// <summary>The submitted question is not the attempt's current question (already answered or out of order).</summary>
    QuestionNotCurrent,
    /// <summary>The submitted choice does not belong to the question.</summary>
    InvalidChoice,
    /// <summary>The attempt's time limit has passed, so it can no longer be answered.</summary>
    TimeExpired,
}

public readonly record struct QuizResult(QuizProgress? Progress, QuizError? Error)
{
    public static implicit operator QuizResult(QuizProgress progress) => new(progress, null);
    public static implicit operator QuizResult(QuizError error) => new(null, error);
}

/// <summary>
/// Quiz-taking rules: questions are answered strictly in order, each exactly once, and progress is identified by
/// an unguessable token created when the first question is answered.
/// </summary>
public sealed class QuizService(
    IDocumentStore<Quiz> quizzes,
    IDocumentStore<QuizAttempt> attempts,
    TimeProvider timeProvider)
{
    // Covers the (rare) case of two concurrent submissions for the same attempt; on retry the loser re-reads
    // the attempt and is rejected by the normal "question already answered" rule.
    private const int MaxWriteAttempts = 3;

    /// <summary>Gets a quiz positioned at its first question, for a user who has not started it yet.</summary>
    public async Task<QuizResult> GetNewProgressAsync(string quizId, CancellationToken cancellationToken)
    {
        var quiz = await quizzes.GetAsync(quizId, cancellationToken);
        return quiz is null ? QuizError.QuizNotFound : BuildProgress(quiz, attempt: null, timeProvider.GetUtcNow());
    }

    public async Task<QuizResult> GetProgressAsync(string quizId, string token, CancellationToken cancellationToken)
    {
        var (quiz, attempt, error) = await LoadAsync(quizId, token, cancellationToken);
        return error ?? (QuizResult)BuildProgress(quiz!, attempt, timeProvider.GetUtcNow());
    }

    /// <summary>Answers the first question of a quiz, creating a new attempt (and token).</summary>
    public async Task<QuizResult> StartAsync(
        string quizId, string questionId, string choiceId, CancellationToken cancellationToken)
    {
        var quiz = await quizzes.GetAsync(quizId, cancellationToken);
        if (quiz is null)
            return QuizError.QuizNotFound;

        for (var i = 0; ; i++)
        {
            var attempt = new QuizAttempt
            {
                Id = GenerateToken(),
                QuizId = quiz.Id,
                StartedAt = timeProvider.GetUtcNow(),
            };

            if (TryRecordAnswer(quiz, attempt, questionId, choiceId) is { } error)
                return error;

            try
            {
                await attempts.CreateAsync(attempt, cancellationToken);
                return BuildProgress(quiz, attempt, timeProvider.GetUtcNow());
            }
            catch (DocumentConflictException) when (i < MaxWriteAttempts - 1)
            {
                // Token collision; astronomically unlikely with 128 random bits, but cheap to handle.
            }
        }
    }

    public async Task<QuizResult> AnswerAsync(
        string quizId, string token, string questionId, string choiceId, CancellationToken cancellationToken)
    {
        for (var i = 0; ; i++)
        {
            var (quiz, attempt, loadError) = await LoadAsync(quizId, token, cancellationToken);
            if (loadError is not null)
                return loadError.Value;

            if (TryRecordAnswer(quiz!, attempt!, questionId, choiceId) is { } error)
                return error;

            try
            {
                await attempts.UpdateAsync(attempt!, cancellationToken);
                return BuildProgress(quiz!, attempt, timeProvider.GetUtcNow());
            }
            catch (DocumentConflictException) when (i < MaxWriteAttempts - 1)
            {
                // Someone else updated the attempt since we read it; re-read and re-validate.
            }
        }
    }

    private async Task<(Quiz? Quiz, QuizAttempt? Attempt, QuizError? Error)> LoadAsync(
        string quizId, string token, CancellationToken cancellationToken)
    {
        var attempt = await attempts.GetAsync(token, cancellationToken);

        // A token for a different quiz is treated the same as an unknown token so tokens can't be probed across quizzes.
        if (attempt is null || attempt.QuizId != quizId)
            return (null, null, QuizError.AttemptNotFound);

        var quiz = await quizzes.GetAsync(quizId, cancellationToken);
        return quiz is null ? (null, null, QuizError.QuizNotFound) : (quiz, attempt, null);
    }

    private QuizError? TryRecordAnswer(Quiz quiz, QuizAttempt attempt, string questionId, string choiceId)
    {
        // Requiring the client to name the question it is answering makes submissions safe to retry: a duplicate
        // "Next" click can never be applied to the following question.
        var now = timeProvider.GetUtcNow();
        var current = FindCurrentQuestion(quiz, attempt);
        if (current is null || current.Id != questionId)
            return QuizError.QuestionNotCurrent;

        if (IsTimedOut(quiz, attempt, now))
            return QuizError.TimeExpired;

        if (!current.Choices.Any(c => c.Id == choiceId))
            return QuizError.InvalidChoice;

        attempt.Answers.Add(new RecordedAnswer(questionId, choiceId, now));

        if (FindCurrentQuestion(quiz, attempt) is null)
            attempt.CompletedAt = now;

        return null;
    }

    private static Question? FindCurrentQuestion(Quiz quiz, QuizAttempt? attempt)
    {
        if (attempt is null)
            return quiz.Questions.FirstOrDefault();

        var answered = attempt.Answers.Select(a => a.QuestionId).ToHashSet(StringComparer.Ordinal);
        return quiz.Questions.FirstOrDefault(q => !answered.Contains(q.Id));
    }

    private static DateTimeOffset? DeadlineOf(Quiz quiz, QuizAttempt? attempt) =>
        attempt is null || quiz.TimeLimitSeconds is not { } limit
            ? null
            : attempt.StartedAt.AddSeconds(limit);

    /// <summary>True when the attempt is still unfinished but its time limit has passed.</summary>
    private static bool IsTimedOut(Quiz quiz, QuizAttempt? attempt, DateTimeOffset now) =>
        DeadlineOf(quiz, attempt) is { } deadline && now >= deadline && FindCurrentQuestion(quiz, attempt) is not null;

    private static QuizProgress BuildProgress(Quiz quiz, QuizAttempt? attempt, DateTimeOffset now)
    {
        var answeredCount = quiz.Questions.Count(q => attempt?.Answers.Any(a => a.QuestionId == q.Id) == true);
        var timedOut = IsTimedOut(quiz, attempt, now);
        var current = timedOut ? null : FindCurrentQuestion(quiz, attempt);
        var deadline = DeadlineOf(quiz, attempt);

        int? remaining = deadline is null || (current is null && !timedOut)
            ? null
            : (int)Math.Ceiling(Math.Max(0, (deadline.Value - now).TotalSeconds));

        return new QuizProgress(
            QuizId: quiz.Id,
            Title: quiz.Title,
            Token: attempt?.Id,
            TotalQuestions: quiz.Questions.Count,
            AnsweredCount: answeredCount,
            CurrentQuestion: current is null ? null : ToView(current, number: answeredCount + 1),
            Summary: current is null ? BuildSummary(quiz, attempt) : null,
            TimeRemainingSeconds: remaining,
            TimedOut: timedOut);
    }

    private static QuestionView ToView(Question question, int number) => new(
        question.Id,
        number,
        question.Text,
        question.Choices.Select(c => new ChoiceView(c.Id, c.Text)).ToList());

    private static IReadOnlyList<SummaryItem> BuildSummary(Quiz quiz, QuizAttempt? attempt)
    {
        var answers = (attempt?.Answers ?? []).ToDictionary(a => a.QuestionId, a => a.ChoiceId, StringComparer.Ordinal);

        return quiz.Questions
            .Select((question, index) => new SummaryItem(
                index + 1,
                question.Id,
                question.Text,
                question.Choices.FirstOrDefault(c => c.Id == answers.GetValueOrDefault(question.Id))?.Text ?? string.Empty))
            .ToList();
    }

    private static string GenerateToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}
