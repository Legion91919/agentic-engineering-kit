namespace ArchAdvisor.Core;

/// <summary>
/// A characteristic of the system being designed. Traits are facts about the problem,
/// never about the solution, so a discovery interview can fill them in before anyone
/// has an opinion about architecture.
/// </summary>
public enum Trait
{
    StaticContent,
    InternalUsersOnly,
    ComplexBusinessRules,
    SeveralBusinessAreas,
    ReadHeavy,
    MoneyMovement,
    RegulatoryAudit,
    MediaDelivery,
    LongRunningWorkflows,
    ExternalPartnerIntegrations,
    LiveTracking,
    LimitedInventory,
    PersonalData,
    HighVolumeDeviceData,
    MultiTenant,
    MillionsOfLiveConnections,
    GlobalScale,
    SmallTeam,
    DocumentIngestion,
    LegacySystems,
    BatchDataFeeds,
}

public enum ArchitectureStyle
{
    StaticSite,
    LayeredMonolith,
    CleanArchitecture,
    ModularMonolith,
    Microservices,
}

public enum Pattern
{
    AppendOnlyLedger,
    IdempotencyKeys,
    TransactionalOutbox,
    AuditTrail,
    Sagas,
    CachedReadModel,
    Cdn,
    EventStream,
    AdapterPerPartner,
    StranglerFig,
    OptimisticConcurrency,
    TenantIsolation,
    SeparateIngestionPipeline,
    ConnectionGateway,
    HumanInTheLoop,
    PersonalDataProtection,
    ServerlessFunction,
}

/// <summary>What discovery found out about the project.</summary>
public sealed record ProjectProfile(IReadOnlySet<Trait> Traits, string? Notes = null)
{
    public static ProjectProfile Of(params Trait[] traits) => new(new HashSet<Trait>(traits));
    public bool Has(Trait trait) => Traits.Contains(trait);
}

/// <summary>A worked example of a real-world system and the architecture that fits it.</summary>
public sealed record ReferenceExample(
    int Id,
    string Name,
    ArchitectureStyle Style,
    string BestFit,
    string Why,
    IReadOnlySet<Trait> Traits);

public sealed record PatternAdvice(Pattern Pattern, string Reason);

public sealed record SimilarExample(ReferenceExample Example, double Similarity);

public sealed record Recommendation(
    ArchitectureStyle Style,
    string StyleReason,
    IReadOnlyList<PatternAdvice> Patterns,
    IReadOnlyList<SimilarExample> ClosestExamples,
    IReadOnlyList<string> EvolveWhen,
    IReadOnlyList<string> Risks);
