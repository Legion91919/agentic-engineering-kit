using static ArchAdvisor.Core.Trait;

namespace ArchAdvisor.Core;

/// <summary>How well a candidate design fits a profile.</summary>
public enum Fit
{
    Recommended,
    GoodAlternative,
    Possible,
    Poor,
}

/// <summary>One way the system could be built. Family ties it back to the advisor's styles.</summary>
public sealed record CandidateDesign(
    string Id,
    string Name,
    ArchitectureStyle Family,
    int Complexity,
    string Summary,
    IReadOnlyList<string> Pros,
    IReadOnlyList<string> Cons,
    string ChooseWhen);

public sealed record CandidateAssessment(CandidateDesign Candidate, Fit Fit, IReadOnlyList<string> Reasons);

/// <summary>
/// Compares every candidate design against a profile, so a design review (or an interview) can answer
/// "why not X?" for each alternative. Deterministic and consistent with <see cref="ArchitectureAdvisor"/>:
/// exactly one candidate is Recommended and it always belongs to the advisor's recommended style.
/// </summary>
public static class ArchitectureComparison
{
    public static readonly IReadOnlyList<CandidateDesign> Candidates = new CandidateDesign[]
    {
        new("static", "Static site + serverless function", ArchitectureStyle.StaticSite, 1,
            "Pre-built pages on a CDN; one small function for the few dynamic bits.",
            new[] { "Near-zero running cost and attack surface", "Scales automatically" },
            new[] { "No real server-side logic, state or user accounts" },
            "Content is the same for every visitor."),
        new("layered", "Layered monolith", ArchitectureStyle.LayeredMonolith, 1,
            "UI, business and data layers in one deployable.",
            new[] { "Fastest to build and understand", "One thing to deploy and monitor" },
            new[] { "Layers don't stop business areas tangling as the app grows", "Rules leak into controllers and SQL" },
            "One simple business area and a small team."),
        new("vertical-slice", "Vertical slice monolith", ArchitectureStyle.LayeredMonolith, 2,
            "Code organised per feature (request, handler, data access together) instead of per layer.",
            new[] { "A change touches one folder", "Easy for agents and new joiners to extend safely" },
            new[] { "Shared rules can get duplicated across slices", "Needs discipline to keep slices independent" },
            "Many small, independent features in one business area."),
        new("clean", "Clean Architecture monolith", ArchitectureStyle.CleanArchitecture, 2,
            "Domain at the centre; application use cases; infrastructure and UI as replaceable adapters.",
            new[] { "Business rules testable without database or UI", "Swappable adapters (database, AI model, payment provider)" },
            new[] { "More projects and interfaces than a simple app needs", "Doesn't by itself separate several business areas" },
            "One business area with rules that must be right and well tested."),
        new("modular-monolith", "Modular monolith", ArchitectureStyle.ModularMonolith, 3,
            "One deployable split into modules that own their data and talk through public contracts.",
            new[] { "Clear boundaries without distributed-system costs", "Any module can be extracted into a service later" },
            new[] { "Boundaries need enforcement (architecture tests)", "Everything scales together" },
            "Several business areas, one team, one database."),
        new("modular-cqrs", "Modular monolith + CQRS read models", ArchitectureStyle.ModularMonolith, 3,
            "Modular monolith whose heavy queries are served from separate, denormalised read models.",
            new[] { "Reads stay fast no matter how complex writes get", "Read side can be cached or moved to a search index" },
            new[] { "Read models are eventually consistent", "Two models to keep in step" },
            "Several business areas and far more reading than writing."),
        new("event-sourced", "Event-sourced core with CQRS", ArchitectureStyle.ModularMonolith, 4,
            "State is stored as an append-only sequence of events; current state and reports are projections.",
            new[] { "Complete history and point-in-time replay for auditors", "New reports can be built from old events" },
            new[] { "Hardest model to learn and evolve (event versioning)", "Projections and snapshots add moving parts" },
            "Money or regulated records where full history and replay matter."),
        new("serverless", "Serverless functions + queues", ArchitectureStyle.Microservices, 3,
            "Small functions triggered by HTTP, queues, schedules or blobs; the platform runs and scales them.",
            new[] { "Pay per use, scales to zero", "Great for bursty jobs, imports and webhooks" },
            new[] { "Cold starts and execution time limits", "Hard to test end to end and to reason about as a whole" },
            "Bursty or scheduled workloads such as file imports, device data and webhooks."),
        new("microservices", "Microservices (request/response)", ArchitectureStyle.Microservices, 5,
            "Independently deployed services, each owning its data, calling each other over HTTP/gRPC.",
            new[] { "Each part scales and deploys on its own", "Teams can own services end to end" },
            new[] { "Network failures, distributed data and versioning everywhere", "Needs platform, tracing and on-call maturity" },
            "Global load on some parts and several teams to run them."),
        new("event-driven", "Event-driven microservices", ArchitectureStyle.Microservices, 5,
            "Services publish and react to events on a stream; few synchronous calls.",
            new[] { "Services keep working when others are slow or down", "Natural fit for live updates and long workflows" },
            new[] { "Eventual consistency everywhere", "Debugging flows across services needs strong tracing" },
            "Very high, uneven load with live updates or long-running workflows."),
    };

    private static readonly Trait[] ScaleTraits = { GlobalScale, MillionsOfLiveConnections };

    public static IReadOnlyList<CandidateAssessment> Compare(ProjectProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var style = ArchitectureAdvisor.Recommend(profile).Style;
        var pick = PickWithinFamily(profile, style);

        return Candidates
            .Select(c => Assess(c, profile, style, pick))
            .OrderBy(a => a.Fit)
            .ThenBy(a => a.Candidate.Complexity)
            .ToList();
    }

    private static string PickWithinFamily(ProjectProfile p, ArchitectureStyle style) => style switch
    {
        ArchitectureStyle.StaticSite => "static",
        ArchitectureStyle.LayeredMonolith => "layered",
        ArchitectureStyle.CleanArchitecture => "clean",
        ArchitectureStyle.ModularMonolith when p.Has(ReadHeavy) || p.Has(MediaDelivery) => "modular-cqrs",
        ArchitectureStyle.ModularMonolith => "modular-monolith",
        ArchitectureStyle.Microservices when p.Has(LiveTracking) || p.Has(MillionsOfLiveConnections)
                                           || p.Has(LongRunningWorkflows) || p.Has(HighVolumeDeviceData) => "event-driven",
        _ => "microservices",
    };

    private static CandidateAssessment Assess(CandidateDesign c, ProjectProfile p, ArchitectureStyle style, string pick)
    {
        var reasons = new List<string>();
        bool scale = ScaleTraits.Any(p.Has);
        bool meaningful = p.Traits.Any(t => t is not SmallTeam and not StaticContent);

        if (c.Id == pick)
        {
            reasons.Add($"Matches the advisor's recommended style ({style}).");
            AddTraitNotes(c, p, reasons);
            return new(c, Fit.Recommended, reasons);
        }

        // Hard mismatches first: these are the answers to "why not X?".
        if (c.Id == "static" && meaningful)
            reasons.Add("The product has real server-side behaviour and data; a static site can't hold it.");
        if (c.Family is ArchitectureStyle.LayeredMonolith or ArchitectureStyle.CleanArchitecture && p.Has(SeveralBusinessAreas))
            reasons.Add("Several business areas would tangle without module boundaries.");
        if (c.Family is ArchitectureStyle.LayeredMonolith or ArchitectureStyle.CleanArchitecture && scale)
            reasons.Add("Can't scale the hot part independently, and has no seams to extract it later.");
        if (c.Complexity >= 5 && p.Has(SmallTeam))
            reasons.Add("A small team can't run a distributed system well (deployments, tracing, on-call).");
        if (c.Family == ArchitectureStyle.Microservices && c.Id != "serverless" && !scale)
            reasons.Add("Pays the distributed-systems tax with no independent scaling need.");
        if (c.Id == "event-sourced" && !(p.Has(MoneyMovement) || p.Has(RegulatoryAudit) || p.Has(LongRunningWorkflows)))
            reasons.Add("Event sourcing's cost isn't repaid without audit, money or replay needs.");
        if (c.Id == "serverless" && p.Has(MillionsOfLiveConnections))
            reasons.Add("Functions are a poor host for very many long-lived connections.");
        if (c.Id == "serverless" && (p.Has(MoneyMovement) || p.Has(LimitedInventory)))
            reasons.Add("Consistency-critical money or stock operations are awkward across stateless functions.");

        if (reasons.Count > 0)
            return new(c, Fit.Poor, reasons);

        AddTraitNotes(c, p, reasons);
        if (c.Family == style)
        {
            reasons.Insert(0, $"Same family as the recommendation ({style}); a valid variant if its trade-offs suit the team.");
            return new(c, Fit.GoodAlternative, reasons);
        }

        if (reasons.Count == 0) reasons.Add("Workable, but nothing in the profile calls for it.");
        return new(c, Fit.Possible, reasons);
    }

    private static void AddTraitNotes(CandidateDesign c, ProjectProfile p, List<string> reasons)
    {
        if (p.Has(MoneyMovement) && c.Id is "event-sourced" or "modular-monolith" or "modular-cqrs" or "clean")
            reasons.Add("Money: pair it with an append-only ledger and idempotency keys.");
        if (p.Has(RegulatoryAudit) && c.Id == "event-sourced")
            reasons.Add("Audit: the event log is the audit trail, with replay for free.");
        if (p.Has(ReadHeavy) && c.Id is "modular-cqrs" or "microservices" or "event-driven")
            reasons.Add("Read-heavy: separate read models keep queries fast.");
        if (p.Has(MediaDelivery))
            reasons.Add("Media: serve files from a CDN whatever the style.");
        if (p.Has(BatchDataFeeds) && c.Id is "serverless" or "layered")
            reasons.Add("Batch feeds: scheduled, re-runnable import jobs fit well here.");
        if (p.Has(HighVolumeDeviceData) && c.Id is "serverless" or "event-driven")
            reasons.Add("Device data: queue-triggered ingestion absorbs bursts.");
        if (p.Has(LegacySystems) && c.Id is "modular-monolith" or "modular-cqrs")
            reasons.Add("Legacy: modules map well onto capabilities moved one at a time (strangler fig).");
        if (p.Has(LiveTracking) && c.Id is "event-driven" or "modular-monolith" or "modular-cqrs")
            reasons.Add("Live updates: push over WebSockets/SignalR from the module that owns the state.");
        if (p.Has(SmallTeam) && c.Complexity <= 3)
            reasons.Add("Small team: low enough operational load to run comfortably.");
    }
}
