using ERP.Infrastructure.Persistence;
using ERP.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Sync;

public class IdempotencyStoreTests
{
    [Fact]
    public async Task Save_then_get_returns_stored_response()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var store = new EfIdempotencyStore(db);

        (await store.GetAsync(businessId, "key-1")).Should().BeNull();

        await store.SaveAsync(businessId, "key-1", 200, "{\"data\":{\"id\":\"x\"}}");

        var got = await store.GetAsync(businessId, "key-1");
        got.Should().NotBeNull();
        got!.StatusCode.Should().Be(200);
        got.ResponseBody.Should().Contain("data");
    }

    [Fact]
    public async Task Duplicate_save_keeps_the_first_response()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var store = new EfIdempotencyStore(db);

        await store.SaveAsync(businessId, "key-1", 200, "first");
        await store.SaveAsync(businessId, "key-1", 500, "second");   // ignored

        (await store.GetAsync(businessId, "key-1"))!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Keys_are_scoped_per_business()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await using var db = TestDb.Create(a);
        var store = new EfIdempotencyStore(db);

        await store.SaveAsync(a, "shared", 200, "a");
        (await store.GetAsync(b, "shared")).Should().BeNull();   // different business, same key
    }
}
