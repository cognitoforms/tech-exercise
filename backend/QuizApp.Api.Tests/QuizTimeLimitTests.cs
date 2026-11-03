using QuizApp.Api.Persistence;
using QuizApp.Api.Quizzes;

namespace QuizApp.Api.Tests;

public class QuizTimeLimitTests
{
    private readonly InMemoryDocumentStore<Quiz> _quizzes = new();
    private readonly InMemoryDocumentStore<QuizAttempt> _attempts = new();
    private readonly ManualTime _time = new();
    private readonly QuizService _service;

    public QuizTimeLimitTests()
    {
        _service = new QuizService(_quizzes, _attempts, _time);
        _quizzes.CreateAsync(new Quiz
        {
            Id = "timed",
            Title = "Timed",
            TimeLimitSeconds = 60,
            Questions =
            [
                new Question("q1", "First?", [new Choice("q1-a", "A1"), new Choice("q1-b", "B1")]),
                new Question("q2", "Second?", [new Choice("q2-a", "A2"), new Choice("q2-b", "B2")]),
            ],
        }).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Untimed_Quiz_ReportsNoTimeRemaining()
    {
        await _quizzes.CreateAsync(new Quiz
        {
            Id = "untimed",
            Title = "Untimed",
            Questions = [new Question("q1", "First?", [new Choice("q1-a", "A1")])],
        });

        var progress = (await _service.StartAsync("untimed", "q1", "q1-a", default)).Progress!;

        Assert.Null(progress.TimeRemainingSeconds);
        Assert.False(progress.TimedOut);
    }

    [Fact]
    public async Task Timer_StartsAtFirstAnswer_AndCountsDown()
    {
        var progress = (await _service.StartAsync("timed", "q1", "q1-a", default)).Progress!;
        Assert.Equal(60, progress.TimeRemainingSeconds);

        _time.Advance(TimeSpan.FromSeconds(25));
        progress = (await _service.GetProgressAsync("timed", progress.Token!, default)).Progress!;

        Assert.Equal(35, progress.TimeRemainingSeconds);
        Assert.False(progress.TimedOut);
        Assert.Equal("q2", progress.CurrentQuestion!.Id);
    }

    [Fact]
    public async Task Answer_AfterDeadline_IsRejected_AndAttemptReportsTimedOut()
    {
        var token = (await _service.StartAsync("timed", "q1", "q1-a", default)).Progress!.Token!;

        _time.Advance(TimeSpan.FromSeconds(60));
        var answer = await _service.AnswerAsync("timed", token, "q2", "q2-a", default);
        Assert.Equal(QuizError.TimeExpired, answer.Error);

        var progress = (await _service.GetProgressAsync("timed", token, default)).Progress!;
        Assert.True(progress.TimedOut);
        Assert.Null(progress.CurrentQuestion);
        Assert.Equal(0, progress.TimeRemainingSeconds);
        Assert.Equal(1, progress.AnsweredCount);
        Assert.Equal(2, progress.Summary!.Count);
        Assert.Equal(string.Empty, progress.Summary[1].AnswerText);
    }

    [Fact]
    public async Task Answer_BeforeDeadline_IsAccepted()
    {
        var token = (await _service.StartAsync("timed", "q1", "q1-a", default)).Progress!.Token!;

        _time.Advance(TimeSpan.FromSeconds(59));
        var answer = await _service.AnswerAsync("timed", token, "q2", "q2-a", default);

        Assert.Null(answer.Error);
        Assert.False(answer.Progress!.TimedOut);
        Assert.True(answer.Progress.IsComplete);
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
