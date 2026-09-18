using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EasyAuthSimulator.Session;

/// <summary>
/// Keeps the auth cookie down to a session key instead of the full ticket — Entra access
/// tokens routinely blow past the ~4KB cookie limit. Local-dev trade-off: restarting the
/// simulator signs everyone out, since nothing is persisted to disk.
/// </summary>
public sealed class InMemoryTicketStore : ITicketStore
{
    private readonly ConcurrentDictionary<string, AuthenticationTicket> _tickets = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        _tickets[key] = ticket;
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        _tickets[key] = ticket;
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        _tickets.TryGetValue(key, out var ticket);
        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        _tickets.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
