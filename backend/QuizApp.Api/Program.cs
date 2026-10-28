using QuizApp.Api.Persistence;
using QuizApp.Api.Quizzes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(typeof(IDocumentStore<>), typeof(InMemoryDocumentStore<>));
builder.Services.AddScoped<QuizService>();
builder.Services.AddHostedService<QuizSeeder>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapQuizEndpoints();

// Unknown API routes should 404 rather than fall through to the SPA.
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));

// Serve the built SPA (frontend `npm run build` outputs to wwwroot). In development the Vite dev server is used instead.
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
