using QuizApp.Api.Persistence;

namespace QuizApp.Api.Tests;

public class InMemoryDocumentStoreTests
{
    private sealed class TestDocument : Document
    {
        public string? Value { get; set; }
    }

    private readonly InMemoryDocumentStore<TestDocument> _store = new();

    [Fact]
    public async Task Get_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _store.GetAsync("missing"));
    }

    [Fact]
    public async Task Create_ThenGet_RoundTripsDocument()
    {
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "one" });

        var stored = await _store.GetAsync("a");

        Assert.NotNull(stored);
        Assert.Equal("one", stored.Value);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task Create_Throws_WhenIdExists()
    {
        await _store.CreateAsync(new TestDocument { Id = "a" });

        await Assert.ThrowsAsync<DocumentConflictException>(() => _store.CreateAsync(new TestDocument { Id = "a" }));
    }

    [Fact]
    public async Task Get_ReturnsCopy_SoCallerMutationsAreNotPersisted()
    {
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "one" });

        var copy = await _store.GetAsync("a");
        copy!.Value = "changed";

        Assert.Equal("one", (await _store.GetAsync("a"))!.Value);
    }

    [Fact]
    public async Task Update_PersistsChangesAndIncrementsVersion()
    {
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "one" });
        var doc = (await _store.GetAsync("a"))!;

        doc.Value = "two";
        await _store.UpdateAsync(doc);

        var stored = (await _store.GetAsync("a"))!;
        Assert.Equal("two", stored.Value);
        Assert.Equal(2, stored.Version);
        Assert.Equal(2, doc.Version);
    }

    [Fact]
    public async Task Update_Throws_WhenDocumentIsStale()
    {
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "one" });
        var first = (await _store.GetAsync("a"))!;
        var second = (await _store.GetAsync("a"))!;

        first.Value = "first";
        await _store.UpdateAsync(first);

        second.Value = "second";
        await Assert.ThrowsAsync<DocumentConflictException>(() => _store.UpdateAsync(second));
        Assert.Equal("first", (await _store.GetAsync("a"))!.Value);
    }

    [Fact]
    public async Task Update_Throws_WhenMissing()
    {
        await Assert.ThrowsAsync<DocumentNotFoundException>(() => _store.UpdateAsync(new TestDocument { Id = "missing" }));
    }
}
