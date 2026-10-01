using System.Security.Cryptography;

namespace IntelligentProgrammingPlatform.Services.Security;

public sealed class ContentSecurityPolicy
{
    public string Nonce { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private bool _monacoStyles;

    // Monaco бар Razor бөлігіне ғана редактордың жолдық стильдерін рұқсат етеді.
    public void EnableMonacoStyles() => _monacoStyles = true;

    // Бір сұраудың nonce мәнімен жергілікті ресурстарға арналған саясатты құрады.
    public string BuildHeader() =>
        "default-src 'none'; " +
        $"script-src 'self' 'nonce-{Nonce}'; script-src-attr 'none'; " +
        $"style-src 'self' 'nonce-{Nonce}'; " +
        $"style-src-attr {(_monacoStyles ? "'unsafe-inline'" : "'none'")}; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self'; worker-src 'self'; " +
        "object-src 'none'; frame-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
}
