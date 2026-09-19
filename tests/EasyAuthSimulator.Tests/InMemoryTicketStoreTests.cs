using System.Security.Claims;
using EasyAuthSimulator.Session;
using Microsoft.AspNetCore.Authentication;

namespace EasyAuthSimulator.Tests;

public sealed class InMemoryTicketStoreTests
{
    [Fact]
    public async Task StoreAsync_ThenRetrieveAsync_RoundTripsTheTicket()
    {
        var store = new InMemoryTicketStore();
        var ticket = CreateTicket();

        var key = await store.StoreAsync(ticket);
        var retrieved = await store.RetrieveAsync(key);

        Assert.Same(ticket, retrieved);
    }

    [Fact]
    public async Task StoreAsync_GeneratesDistinctKeys_ForEachTicket()
    {
        var store = new InMemoryTicketStore();

        var key1 = await store.StoreAsync(CreateTicket());
        var key2 = await store.StoreAsync(CreateTicket());

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsNull_ForUnknownKey()
    {
        var store = new InMemoryTicketStore();

        Assert.Null(await store.RetrieveAsync("unknown-key"));
    }

    [Fact]
    public async Task RenewAsync_ReplacesTheStoredTicket_ForTheSameKey()
    {
        var store = new InMemoryTicketStore();
        var key = await store.StoreAsync(CreateTicket());
        var renewedTicket = CreateTicket();

        await store.RenewAsync(key, renewedTicket);

        Assert.Same(renewedTicket, await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheStoredTicket()
    {
        var store = new InMemoryTicketStore();
        var key = await store.StoreAsync(CreateTicket());

        await store.RemoveAsync(key);

        Assert.Null(await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task RemoveAsync_IsNoOp_ForUnknownKey()
    {
        var store = new InMemoryTicketStore();

        await store.RemoveAsync("unknown-key");
    }

    private static AuthenticationTicket CreateTicket() =>
        new(new ClaimsPrincipal(), new AuthenticationProperties(), "scheme");
}
