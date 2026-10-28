using QuizApp.Api.Persistence;
using QuizApp.Api.Quizzes;

namespace QuizApp.Api.Tests;

public class QuizServiceTests
{
    private readonly InMemoryDocumentStore<Quiz> _quizzes = new();
    private readonly InMemoryDocumentStore<QuizAttempt> _attempts = new();
    private readonly QuizService _service;

    public QuizServiceTests()
    {
        _service = new QuizService(_quizzes, _attempts, TimeProvider.System);
        _quizzes.CreateAsync(new Quiz
        {
            Id = "quiz",
            Title = "Test Quiz",
            Questions =
            [
                new Question("q1", "First?", [new Choice("q1-a", "A1"), new Choice("q1-b", "B1")]),
                new Question("q2", "Second?", [new Choice("q2-a", "A2"), new Choice("q2-b", "B2")]),
            ],
        }).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetNewProgress_ReturnsFirstQuestionWithoutToken()
    {
        var result = await _service.GetNewProgressAsync("quiz", default);

        Assert.Null(result.Error);
        Assert.Null(result.Progress!.Token);
        Assert.Equal("q1", result.Progress.CurrentQuestion!.Id);
        Assert.Equal(1, result.Progress.CurrentQuestion.Number);
        Assert.Equal(0, result.Progress.AnsweredCount);
    }

    [Fact]
    public async Task GetNewProgress_ReturnsNotFound_ForUnknownQuiz()
    {
        Assert.Equal(QuizError.QuizNotFound, (await _service.GetNewProgressAsync("nope", default)).Error);
    }

    [Fact]
    public async Task Start_CreatesAttemptWithTokenAndAdvances()
    {
        var result = await _service.StartAsync("quiz", "q1", "q1-b", default);

        var progress = result.Progress!;
        Assert.False(string.IsNullOrEmpty(progress.Token));
        Assert.Equal(1, progress.AnsweredCount);
        Assert.Equal("q2", progress.CurrentQuestion!.Id);
        Assert.NotNull(await _attempts.GetAsync(progress.Token!));
    }

    [Fact]
    public async Task Start_GeneratesDistinctTokens()
    {
        var a = await _service.StartAsync("quiz", "q1", "q1-a", default);
        var b = await _service.StartAsync("quiz", "q1", "q1-a", default);

        Assert.NotEqual(a.Progress!.Token, b.Progress!.Token);
    }

    [Theory]
    [InlineData("q2", "q2-a", QuizError.QuestionNotCurrent)]
    [InlineData("q1", "q2-a", QuizError.InvalidChoice)]
    [InlineData("q1", "bogus", QuizError.InvalidChoice)]
    public async Task Start_RejectsInvalidAnswers(string questionId, string choiceId, QuizError expected)
    {
        var result = await _service.StartAsync("quiz", questionId, choiceId, default);

        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task Resume_ReturnsFirstUnansweredQuestion()
    {
        var token = (await _service.StartAsync("quiz", "q1", "q1-a", default)).Progress!.Token!;

        var result = await _service.GetProgressAsync("quiz", token, default);

        Assert.Equal("q2", result.Progress!.CurrentQuestion!.Id);
        Assert.Equal(2, result.Progress.CurrentQuestion.Number);
        Assert.Null(result.Progress.Summary);
    }

    [Fact]
    public async Task AnsweringAllQuestions_ReturnsSummary()
    {
        var token = (await _service.StartAsync("quiz", "q1", "q1-b", default)).Progress!.Token!;

        var result = await _service.AnswerAsync("quiz", token, "q2", "q2-a", default);

        var progress = result.Progress!;
        Assert.True(progress.IsComplete);
        Assert.Null(progress.CurrentQuestion);
        Assert.Equal(
            [new SummaryItem(1, "q1", "First?", "B1"), new SummaryItem(2, "q2", "Second?", "A2")],
            progress.Summary);

        var resumed = await _service.GetProgressAsync("quiz", token, default);
        Assert.Equal(progress.Summary, resumed.Progress!.Summary);
        Assert.NotNull((await _attempts.GetAsync(token))!.CompletedAt);
    }

    [Fact]
    public async Task Answer_RejectsAlreadyAnsweredQuestion()
    {
        var token = (await _service.StartAsync("quiz", "q1", "q1-a", default)).Progress!.Token!;

        var result = await _service.AnswerAsync("quiz", token, "q1", "q1-b", default);

        Assert.Equal(QuizError.QuestionNotCurrent, result.Error);
        var attempt = (await _attempts.GetAsync(token))!;
        Assert.Equal("q1-a", Assert.Single(attempt.Answers).ChoiceId);
    }

    [Fact]
    public async Task Answer_RejectsWhenQuizComplete()
    {
        var token = (await _service.StartAsync("quiz", "q1", "q1-a", default)).Progress!.Token!;
        await _service.AnswerAsync("quiz", token, "q2", "q2-a", default);

        var result = await _service.AnswerAsync("quiz", token, "q2", "q2-b", default);

        Assert.Equal(QuizError.QuestionNotCurrent, result.Error);
    }

    [Fact]
    public async Task Token_IsScopedToItsQuiz()
    {
        await _quizzes.CreateAsync(new Quiz { Id = "other", Title = "Other", Questions = [] });
        var token = (await _service.StartAsync("quiz", "q1", "q1-a", default)).Progress!.Token!;

        Assert.Equal(QuizError.AttemptNotFound, (await _service.GetProgressAsync("other", token, default)).Error);
        Assert.Equal(QuizError.AttemptNotFound, (await _service.AnswerAsync("other", token, "q2", "q2-a", default)).Error);
    }

    [Fact]
    public async Task UnknownToken_ReturnsAttemptNotFound()
    {
        Assert.Equal(QuizError.AttemptNotFound, (await _service.GetProgressAsync("quiz", "nope", default)).Error);
    }

    [Fact]
    public async Task ConcurrentAnswers_OnlyOneIsRecorded()
    {
        var token = (await _service.StartAsync("quiz", "q1", "q1-a", default)).Progress!.Token!;

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            Task.Run(() => _service.AnswerAsync("quiz", token, "q2", i % 2 == 0 ? "q2-a" : "q2-b", default))));

        Assert.Single(results, r => r.Error is null);
        Assert.Equal(2, (await _attempts.GetAsync(token))!.Answers.Count);
    }
}
