using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using QuizApp.Api.Quizzes;

namespace QuizApp.Api.Tests;

public class QuizApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FullFlow_StartResumeAndComplete()
    {
        var quiz = await _client.GetFromJsonAsync<QuizProgress>("/api/quizzes/space");
        Assert.Equal("Space Exploration", quiz!.Title);
        Assert.Null(quiz.Token);

        var startResponse = await _client.PostAsJsonAsync("/api/quizzes/space/attempts",
            new AnswerRequest(quiz.CurrentQuestion!.Id, quiz.CurrentQuestion.Choices[2].Id));
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var started = (await startResponse.Content.ReadFromJsonAsync<QuizProgress>())!;
        Assert.NotNull(started.Token);
        Assert.Equal($"/api/quizzes/space/attempts/{started.Token}", startResponse.Headers.Location!.ToString());

        var resumed = (await _client.GetFromJsonAsync<QuizProgress>($"/api/quizzes/space/attempts/{started.Token}"))!;
        Assert.Equal(started.CurrentQuestion!.Id, resumed.CurrentQuestion!.Id);

        var progress = resumed;
        while (progress.CurrentQuestion is { } question)
        {
            var response = await _client.PostAsJsonAsync($"/api/quizzes/space/attempts/{started.Token}/answers",
                new AnswerRequest(question.Id, question.Choices[0].Id));
            response.EnsureSuccessStatusCode();
            progress = (await response.Content.ReadFromJsonAsync<QuizProgress>())!;
        }

        Assert.True(progress.IsComplete);
        Assert.Equal(progress.TotalQuestions, progress.Summary!.Count);
        Assert.Equal("Neil Armstrong", progress.Summary[0].AnswerText);
    }

    [Theory]
    [InlineData("/api/quizzes/does-not-exist")]
    [InlineData("/api/quizzes/space/attempts/not-a-token")]
    [InlineData("/api/unknown")]
    public async Task UnknownResources_Return404(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Start_WithoutChoice_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/quizzes/space/attempts", new AnswerRequest("q1", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReansweringQuestion_Returns409()
    {
        var started = await (await _client.PostAsJsonAsync("/api/quizzes/space/attempts", new AnswerRequest("q1", "q1-a")))
            .Content.ReadFromJsonAsync<QuizProgress>();

        var response = await _client.PostAsJsonAsync($"/api/quizzes/space/attempts/{started!.Token}/answers",
            new AnswerRequest("q1", "q1-b"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
