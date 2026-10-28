namespace QuizApp.Api.Persistence;

/// <summary>
/// Base type for anything persisted through <see cref="IDocumentStore{TDocument}"/>.
/// </summary>
public abstract class Document
{
    public required string Id { get; init; }

    /// <summary>
    /// Optimistic concurrency token. Assigned by the store on create and incremented on every
    /// successful update; callers should treat it as opaque.
    /// </summary>
    public long Version { get; set; }
}
