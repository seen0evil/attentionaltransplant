namespace AttentionalTransplants.DonorDataCollection
{
    public enum AttentionSemanticLayer
    {
        Auto = 0,
        Environment = 1,
        Obstacle = 2,
        Signs = 3
    }

    public enum GuidanceRole
    {
        Structural = 0,
        Goal = 1,
        DirectionalSign = 2,
        Landmark = 3,
        Distractor = 4,
        Other = 5
    }

    public enum TrialEndReason
    {
        None = 0,
        ObjectiveReached = 1,
        Timeout = 2,
        ManualStop = 3,
        Aborted = 4
    }
}
