# Quiz App

Multiple choice quizzes that can be resumed later. Vue 3 SPA + ASP.NET Core (.NET 10) JSON API.

## Running

Requires the .NET 10 SDK and Node 20+.

**Aspire** (recommended; hot reload, one command). Requires the [Aspire CLI](https://aspire.dev/get-started/install-cli/):

```bash
aspire run
```

This starts the API and the Vite dev server (running `npm install` first if needed) and opens the Aspire
dashboard, which shows each resource's URL and logs. Open the `frontend` URL from the dashboard, e.g.
`<frontend url>/quiz/world-geography`. The AppHost is `backend/QuizApp.AppHost/AppHost.cs`.

**Single process** (API serves the built SPA):

```bash
cd frontend && npm install && npm run build   # outputs to backend/QuizApp.Api/wwwroot
cd .. && dotnet run --project backend/QuizApp.Api
# http://localhost:5210/quiz/world-geography
```

**Development without Aspire** (hot reload; Vite proxies `/api` to the backend):

```bash
dotnet run --project backend/QuizApp.Api      # API on http://localhost:5210
cd frontend && npm run dev                     # SPA on http://localhost:5173
```

Sample quizzes (seeded at startup from `backend/QuizApp.Api/Quizzes/QuizSeeder.cs`):

- `/quiz/world-geography`
- `/quiz/web-fundamentals` (timed: 90 seconds; see `frontend/src/quizTimeLimits.ts`)
- `/quiz/space`

## Tests

```bash
dotnet test QuizApp.slnx        # document store, quiz rules, API integration
cd frontend && npm test          # QuestionForm component
```

## How it works

### Flow and URLs

1. `/quiz/{quizId}` loads the quiz at question 1. Nothing is persisted yet, so people who open a link and leave
   don't create documents.
2. Answering question 1 creates an attempt. The server returns a token and the SPA calls `router.replace` to
   change the URL to `/quiz/{quizId}/{token}`. It uses `replace` so the Back button can't go to an answered question.
3. Loading `/quiz/{quizId}/{token}` resumes at the first unanswered question, or shows the summary once all
   questions are answered.

### API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/quizzes/{quizId}` | Quiz positioned at the first question (no token) |
| POST | `/api/quizzes/{quizId}/attempts` | Answer question 1; creates the attempt → `201` with token |
| GET | `/api/quizzes/{quizId}/attempts/{token}` | Resume: current question, or summary when complete |
| POST | `/api/quizzes/{quizId}/attempts/{token}/answers` | Answer the current question |

Every response has the same shape (`QuizProgress`): title, counts, token, and *either* `currentQuestion` *or*
`summary`. The client never gets other questions, so it can't skip ahead.

Answer bodies are `{ questionId, choiceId }`. The server only accepts the attempt's current question, so:

- an answered question can't be changed (`409`),
- a double-clicked or retried "Next" can't be applied to the following question (`409`, and the SPA resyncs),
- a choice from another question is rejected (`400`).

Errors use RFC 7807 problem details. A token that belongs to a different quiz returns `404`, the same as an
unknown token.

### Persistence

`IDocumentStore<T>` provides get/create/update/delete by id, plus `GetAll` and `GetRange(idPrefix)` (key
prefix scan) for listing. `InMemoryDocumentStore<T>` implements it:

- Documents are stored as serialized JSON, so callers always get a copy and can only change stored state by
  calling `UpdateAsync`, the same as with a real document DB.
- `Version` gives optimistic concurrency: updating from a stale read throws `DocumentConflictException`.
  `QuizService` retries by re-reading, so when two submissions race for the same question, one wins and the
  other gets `409`. There's a test for this.
- Listing returns documents ordered by id (ordinal). `GetRange` matches ids by prefix (ordinal, case-sensitive),
  so related documents can be grouped under a shared id prefix. There are no other indexes or filters.

Two document types: `Quiz` (id = URL slug) and `QuizAttempt` (id = the resume token: 128 random bits,
base64url).

### Layout

```
backend/QuizApp.Api/
  Persistence/      document store abstraction + in-memory implementation
  Quizzes/          documents, QuizService (rules), endpoints, contracts (DTOs), seed data
backend/QuizApp.Api.Tests/
backend/QuizApp.AppHost/  Aspire orchestration for local dev (API + Vite)
frontend/src/
  api/quizApi.ts               typed API client
  views/QuizView.vue           loading, errors, submit, URL update
  components/QuestionForm.vue  one question; Next is disabled until a choice is selected
  components/QuizSummary.vue
```
