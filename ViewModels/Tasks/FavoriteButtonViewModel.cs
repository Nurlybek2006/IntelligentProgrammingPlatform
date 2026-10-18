namespace IntelligentProgrammingPlatform.ViewModels.Tasks;

// Таңдаулы батырмасына есеп кілті мен көрініс күйін береді.
public sealed record FavoriteButtonViewModel(int TaskId, bool IsFavorite, bool Compact = false);
