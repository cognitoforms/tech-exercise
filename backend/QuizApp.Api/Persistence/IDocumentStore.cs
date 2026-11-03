namespace QuizApp.Api.Persistence;

/// <summary>
/// Minimal document database abstraction: get, list, create, update, and delete documents.
/// </summary>
/// <remarks>
/// Listing methods return documents ordered by <see cref="Document.Id"/> (ordinal). <see cref="GetRangeAsync"/>
/// is a key prefix scan, so related documents can be grouped by giving them ids with a shared prefix.
/// </remarks>
public interface IDocumentStore<TDocument> where TDocument : Document
{
    /// <summary>Returns the document with the given id, or <c>null</c> if it does not exist.</summary>
    Task<TDocument?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns every document, ordered by id.</summary>
    Task<IReadOnlyList<TDocument>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every document whose id starts with <paramref name="idPrefix"/> (ordinal, case-sensitive),
    /// ordered by id. An empty prefix matches every document.
    /// </summary>
    Task<IReadOnlyList<TDocument>> GetRangeAsync(string idPrefix, CancellationToken cancellationToken = default);

    /// <summary>Persists a new document.</summary>
    /// <exception cref="DocumentConflictException">A document with the same id already exists.</exception>
    Task CreateAsync(TDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing document. Succeeds only if <see cref="Document.Version"/> matches the stored version.
    /// </summary>
    /// <exception cref="DocumentNotFoundException">No document with the given id exists.</exception>
    /// <exception cref="DocumentConflictException">The document was modified since it was read.</exception>
    Task UpdateAsync(TDocument document, CancellationToken cancellationToken = default);

    /// <summary>Removes the document with the given id.</summary>
    /// <exception cref="DocumentNotFoundException">No document with the given id exists.</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class DocumentConflictException(string id)
    : Exception($"Document '{id}' already exists or was modified concurrently.")
{
    public string DocumentId { get; } = id;
}

public sealed class DocumentNotFoundException(string id)
    : Exception($"Document '{id}' does not exist.")
{
    public string DocumentId { get; } = id;
}
