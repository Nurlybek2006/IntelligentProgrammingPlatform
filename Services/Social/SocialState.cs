namespace IntelligentProgrammingPlatform.Services.Social;

public enum SocialRelationship { Guest, Self, None, Outgoing, Incoming, Friends, Blocked, Unavailable }
public enum FriendshipAction { Request, Accept, Decline, Cancel, Remove, Block, Unblock }
public enum SocialOutcome { Changed, Unchanged, NotFound, NotAllowed, Unavailable, InvalidMessage, Busy, Failed }

// Әлеуметтік әрекеттің нәтижесі мен қажет болса сақталған объект нөмірін қайтарады.
public sealed record SocialResult(SocialOutcome Outcome, long? Id = null)
{
    public bool Succeeded => Outcome is SocialOutcome.Changed or SocialOutcome.Unchanged;
}
