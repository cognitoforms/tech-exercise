using System.Collections.Concurrent;
using System.Text.Json;

namespace QuizApp.Api.Persistence;

/// <summary>
/// Thread-safe, in-memory <see cref="IDocumentStore{TDocument}"/>.
/// </summary>
/// <remarks>
/// Documents are stored as serialized JSON so that callers always receive an independent copy and cannot
/// mutate stored state without going through <see cref="UpdateAsync"/>, mirroring a real document database.
/// </remarks>
public sealed class InMemoryDocumentStore<TDocument> : IDocumentStore<TDocument> where TDocument : Document
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    private readonly ConcurrentDictionary<string, string> _documents = new(StringComparer.Ordinal);

    public Task<TDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return Task.FromResult(_documents.TryGetValue(id, out var json) ? Deserialize(json) : null);
    }

    public Task<IReadOnlyList<TDocument>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TDocument>>(OrderedSnapshot().Select(Deserialize).ToList());

    public Task<IReadOnlyList<TDocument>> GetRangeAsync(string idPrefix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idPrefix);

        // Filter on the key before deserializing so only matching documents are materialized.
        return Task.FromResult<IReadOnlyList<TDocument>>(
            OrderedSnapshot(id => id.StartsWith(idPrefix, StringComparison.Ordinal)).Select(Deserialize).ToList());
    }

    public Task CreateAsync(TDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Version = 1;
        if (!_documents.TryAdd(document.Id, Serialize(document)))
        {
            document.Version = 0;
            throw new DocumentConflictException(document.Id);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(TDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!_documents.TryGetValue(document.Id, out var currentJson))
            throw new DocumentNotFoundException(document.Id);

        var expectedVersion = document.Version;
        if (Deserialize(currentJson).Version != expectedVersion)
            throw new DocumentConflictException(document.Id);

        document.Version = expectedVersion + 1;

        // Compare-and-swap against the exact snapshot we validated, so a concurrent update between the
        // version check and the write is detected rather than silently overwritten.
        if (!_documents.TryUpdate(document.Id, Serialize(document), currentJson))
        {
            document.Version = expectedVersion;
            throw new DocumentConflictException(document.Id);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!_documents.TryRemove(id, out _))
            throw new DocumentNotFoundException(id);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Point-in-time copy of the stored JSON, optionally filtered by id and ordered by id. <see cref="ConcurrentDictionary{TKey,TValue}.ToArray"/>
    /// takes a consistent snapshot, unlike enumerating the dictionary directly.
    /// </summary>
    private IEnumerable<string> OrderedSnapshot(Func<string, bool>? idFilter = null) =>
        _documents.ToArray()
            .Where(entry => idFilter is null || idFilter(entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Value);

    private static string Serialize(TDocument document) => JsonSerializer.Serialize(document, SerializerOptions);

    private static TDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<TDocument>(json, SerializerOptions)
        ?? throw new InvalidOperationException("Stored document could not be deserialized.");
}
