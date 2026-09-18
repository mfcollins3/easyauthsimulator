namespace EasyAuthSimulator.Options;

public sealed class AadProviderOptions
{
    public const string SectionName = $"{EasyAuthOptions.SectionName}:Aad";

    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>Lets sovereign clouds (e.g. login.microsoftonline.us) override the public cloud authority host.</summary>
    public string AuthorityHost { get; set; } = "login.microsoftonline.com";

    /// <summary><c>offline_access</c> must stay in this list for a refresh token to be issued.</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email", "offline_access"];
}
