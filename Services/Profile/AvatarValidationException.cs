namespace IntelligentProgrammingPlatform.Services.Profile;

public sealed class AvatarValidationException : Exception
{
    public string ResourceKey { get; }

    // Тексеру қатесін аударылатын ресурс кілтімен жеткізеді.
    public AvatarValidationException(string resourceKey) : base(resourceKey) => ResourceKey = resourceKey;
}
