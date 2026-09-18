using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAuthSimulator.Extensions;

/// <summary>Carries the service collection and configuration through the AddEasyAuth().AddXxx() chain.</summary>
public sealed class EasyAuthBuilder(IServiceCollection services, IConfiguration configuration)
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
}
