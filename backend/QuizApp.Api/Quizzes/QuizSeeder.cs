using QuizApp.Api.Persistence;

namespace QuizApp.Api.Quizzes;

/// <summary>Loads the sample quizzes into the document store at startup.</summary>
public sealed class QuizSeeder(IDocumentStore<Quiz> quizzes, ILogger<QuizSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var quiz in SampleQuizzes.All)
        {
            try
            {
                await quizzes.CreateAsync(quiz, cancellationToken);
                logger.LogInformation("Seeded quiz '{QuizId}' ({QuestionCount} questions)", quiz.Id, quiz.Questions.Count);
            }
            catch (DocumentConflictException)
            {
                // Already present; nothing to do.
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class SampleQuizzes
{
    public static IReadOnlyList<Quiz> All =>
    [
        new Quiz
        {
            Id = "world-geography",
            Title = "World Geography",
            Questions =
            [
                Q("q1", "What is the capital of Australia?", "Sydney", "Canberra", "Melbourne", "Perth"),
                Q("q2", "Which river is the longest in the world?", "Amazon", "Yangtze", "Nile", "Mississippi"),
                Q("q3", "Which country has the most natural lakes?", "Canada", "Russia", "United States", "Finland"),
                Q("q4", "Mount Kilimanjaro is located in which country?", "Kenya", "Uganda", "Tanzania", "Ethiopia"),
                Q("q5", "What is the smallest country in the world by area?", "Monaco", "Vatican City", "San Marino", "Liechtenstein"),
            ],
        },
        new Quiz
        {
            Id = "web-fundamentals",
            Title = "Web Fundamentals",
            TimeLimitSeconds = 90,
            Questions =
            [
                Q("q1", "Which HTTP status code indicates that a resource was created?", "200", "201", "204", "302"),
                Q("q2", "Which HTTP method is idempotent?", "POST", "PUT", "PATCH", "CONNECT"),
                Q("q3", "In Vue, which directive renders an element conditionally?", "v-for", "v-bind", "v-if", "v-model"),
                Q("q4", "What does CORS stand for?", "Cross-Origin Resource Sharing", "Client Origin Request Security",
                    "Cross-Object Response Signing", "Cached Origin Resource Store"),
            ],
        },
        new Quiz
        {
            Id = "space",
            Title = "Space Exploration",
            Questions =
            [
                Q("q1", "Who was the first person to walk on the Moon?", "Buzz Aldrin", "Yuri Gagarin", "Neil Armstrong", "John Glenn"),
                Q("q2", "Which planet is known as the Red Planet?", "Venus", "Mars", "Jupiter", "Mercury"),
                Q("q3", "What was the first artificial satellite?", "Explorer 1", "Vanguard 1", "Sputnik 1", "Telstar"),
            ],
        },
    ];

    private static Question Q(string id, string text, params string[] choices) =>
        new(id, text, choices.Select((choice, i) => new Choice($"{id}-{(char)('a' + i)}", choice)).ToList());
}
