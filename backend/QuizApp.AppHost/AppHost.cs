var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.QuizApp_Api>("api");

// Vite dev server with hot reload. Its /api proxy targets the API's address, which Aspire passes in via
// WithReference (see frontend/vite.config.ts).
builder.AddViteApp("frontend", "../../frontend")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
