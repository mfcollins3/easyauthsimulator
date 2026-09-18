using System.Diagnostics.CodeAnalysis;

namespace EasyAuthSimulator.Providers;

/// <summary>Looks up a registered <see cref="IEasyAuthProvider"/> by its slug (e.g. "aad").</summary>
public sealed class EasyAuthProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IEasyAuthProvider> _providers;

    public EasyAuthProviderRegistry(IEnumerable<IEasyAuthProvider> providers) =>
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string name, [NotNullWhen(true)] out IEasyAuthProvider? provider) =>
        _providers.TryGetValue(name, out provider);
}
