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

    [Fact]
    public async Task GetAll_ReturnsEmpty_WhenStoreIsEmpty()
    {
        Assert.Empty(await _store.GetAllAsync());
    }

    [Fact]
    public async Task GetAll_ReturnsAllDocumentsOrderedById()
    {
        await _store.CreateAsync(new TestDocument { Id = "c", Value = "three" });
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "one" });
        await _store.CreateAsync(new TestDocument { Id = "b", Value = "two" });

        var all = await _store.GetAllAsync();

        Assert.Equal(["a", "b", "c"], all.Select(d => d.Id));
        Assert.Equal(["one", "two", "three"], all.Select(d => d.Value));
        Assert.All(all, d => Assert.Equal(1, d.Version));
    }

    [Fact]
    public async Task GetAll_ReturnsCopies_SoCallerMutationsAreNotPersisted()
    {
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "one" });

        (await _store.GetAllAsync())[0].Value = "changed";

        Assert.Equal("one", (await _store.GetAsync("a"))!.Value);
    }

    [Theory]
    [InlineData("quiz-1/", new[] { "quiz-1/a", "quiz-1/b" })]
    [InlineData("quiz-1", new[] { "quiz-1/a", "quiz-1/b", "quiz-10/a" })]
    [InlineData("quiz-2/", new[] { "quiz-2/a" })]
    [InlineData("QUIZ", new string[0])]
    [InlineData("missing", new string[0])]
    [InlineData("", new[] { "quiz-1/a", "quiz-1/b", "quiz-10/a", "quiz-2/a" })]
    public async Task GetRange_ReturnsDocumentsWithIdPrefix_OrderedById(string prefix, string[] expectedIds)
    {
        await _store.CreateAsync(new TestDocument { Id = "quiz-2/a" });
        await _store.CreateAsync(new TestDocument { Id = "quiz-1/b" });
        await _store.CreateAsync(new TestDocument { Id = "quiz-10/a" });
        await _store.CreateAsync(new TestDocument { Id = "quiz-1/a" });

        var range = await _store.GetRangeAsync(prefix);

        Assert.Equal(expectedIds, range.Select(d => d.Id));
    }

    [Fact]
    public async Task GetRange_ReturnsCopies_SoCallerMutationsAreNotPersisted()
    {
        await _store.CreateAsync(new TestDocument { Id = "a/1", Value = "one" });

        (await _store.GetRangeAsync("a/"))[0].Value = "changed";

        Assert.Equal("one", (await _store.GetAsync("a/1"))!.Value);
    }

    [Fact]
    public async Task GetRange_Throws_WhenPrefixIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.GetRangeAsync(null!));
    }

    [Fact]
    public async Task Delete_RemovesDocument()
    {
        await _store.CreateAsync(new TestDocument { Id = "a" });
        await _store.CreateAsync(new TestDocument { Id = "b" });

        await _store.DeleteAsync("a");

        Assert.Null(await _store.GetAsync("a"));
        Assert.Equal(["b"], (await _store.GetAllAsync()).Select(d => d.Id));
    }

    [Fact]
    public async Task Delete_Throws_WhenMissing()
    {
        await Assert.ThrowsAsync<DocumentNotFoundException>(() => _store.DeleteAsync("missing"));
    }

    [Fact]
    public async Task Delete_AllowsIdToBeReused()
    {
        await _store.CreateAsync(new TestDocument { Id = "a", Value = "old" });
        await _store.DeleteAsync("a");

        await _store.CreateAsync(new TestDocument { Id = "a", Value = "new" });

        var stored = (await _store.GetAsync("a"))!;
        Assert.Equal("new", stored.Value);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task Update_Throws_WhenDocumentWasDeleted()
    {
        await _store.CreateAsync(new TestDocument { Id = "a" });
        var doc = (await _store.GetAsync("a"))!;
        await _store.DeleteAsync("a");

        await Assert.ThrowsAsync<DocumentNotFoundException>(() => _store.UpdateAsync(doc));
    }
}
