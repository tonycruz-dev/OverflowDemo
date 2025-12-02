namespace Contracts;

public record AiReputationChanged(
    string AiId,
    int Delta,
    ReputationReason Reason,
    string ActorUserId,
    DateTime Occurred
    );