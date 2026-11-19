using static ArchAdvisor.Core.Trait;

namespace ArchAdvisor.Core;

/// <summary>
/// Turns a project profile into an architecture recommendation.
/// Deliberately rule-based and deterministic: an agent can call it, a human can read the
/// rules, and the same input always gives the same answer. The agent adds judgement on top;
/// this class supplies the defensible default.
/// </summary>
public static class ArchitectureAdvisor
{
    private static readonly Trait[] ScaleTraits = { GlobalScale, MillionsOfLiveConnections };

    public static Recommendation Recommend(ProjectProfile profile, int closestCount = 3)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var (style, reason) = ChooseStyle(profile);
        var patterns = ChoosePatterns(profile, style);
        var closest = Closest(profile, closestCount);

        return new Recommendation(
            style,
            reason,
            patterns,
            closest,
            EvolveWhen(profile, style),
            Risks(profile, style));
    }

    private static (ArchitectureStyle, string) ChooseStyle(ProjectProfile p)
    {
        var meaningful = p.Traits.Where(t => t is not SmallTeam and not StaticContent).ToList();

        if (p.Has(StaticContent) && meaningful.Count == 0)
            return (ArchitectureStyle.StaticSite,
                "Content is the same for everyone, so there is nothing for a server to do.");

        bool needsIndependentScaling = ScaleTraits.Any(p.Has);
        if (needsIndependentScaling && !p.Has(SmallTeam))
            return (ArchitectureStyle.Microservices,
                "Parts of the system face load that is orders of magnitude different from the rest, and they must scale and deploy on their own.");

        if (needsIndependentScaling)
            return (ArchitectureStyle.ModularMonolith,
                "The load would justify microservices, but a small team cannot run them. Build strict modules now so the hot module can be extracted later.");

        if (p.Has(SeveralBusinessAreas))
            return (ArchitectureStyle.ModularMonolith,
                "Several business areas need clear boundaries, but one deployment and one database keep it cheap to run and easy to hand over.");

        if (p.Has(ComplexBusinessRules) || p.Has(RegulatoryAudit))
            return (ArchitectureStyle.CleanArchitecture,
                "One business area with rules that must be tested without the database or UI. Keep the domain at the centre.");

        return (ArchitectureStyle.LayeredMonolith,
            "A single, simple business area. Layers are enough; anything more is ceremony.");
    }

    private static List<PatternAdvice> ChoosePatterns(ProjectProfile p, ArchitectureStyle style)
    {
        var list = new List<PatternAdvice>();
        void Add(Pattern pattern, string why)
        {
            if (list.All(x => x.Pattern != pattern)) list.Add(new PatternAdvice(pattern, why));
        }

        if (style == ArchitectureStyle.StaticSite)
            Add(Pattern.ServerlessFunction, "The one dynamic need (a contact form) fits a single small function.");

        if (p.Has(MoneyMovement))
        {
            Add(Pattern.AppendOnlyLedger, "Record debits and credits as new entries instead of overwriting a balance, so money always balances and can be traced.");
            Add(Pattern.IdempotencyKeys, "Every money-moving request carries a key, so a retry can never pay twice.");
            Add(Pattern.TransactionalOutbox, "Save the change and the event in one transaction, so other parts never miss a payment.");
        }
        if (p.Has(RegulatoryAudit))
            Add(Pattern.AuditTrail, "Keep an immutable record of who did what and when; regulators and clients will ask.");
        if (p.Has(LongRunningWorkflows))
            Add(Pattern.Sagas, "Processes that last days or weeks need explicit state and compensating steps, not one long transaction.");
        if (p.Has(ReadHeavy))
            Add(Pattern.CachedReadModel, "Serve searches from a separate, cached read side so reads never slow down writes.");
        if (p.Has(MediaDelivery))
            Add(Pattern.Cdn, "Large files go through a CDN, never through your application servers.");
        if (p.Has(LimitedInventory))
            Add(Pattern.OptimisticConcurrency, "Hold scarce items with a version check and a short reservation, so they are never sold twice.");
        if (p.Has(ExternalPartnerIntegrations))
            Add(Pattern.AdapterPerPartner, "Each outside system gets its own adapter behind an interface, so its quirks stay out of your domain.");
        if (p.Has(LegacySystems))
        {
            Add(Pattern.StranglerFig, "Replace the legacy system one capability at a time behind a facade, never in one big-bang cut-over.");
            Add(Pattern.AdapterPerPartner, "Wrap the legacy system in an adapter that translates its data into your model.");
        }
        if (p.Has(PersonalData))
            Add(Pattern.PersonalDataProtection, "Collect the minimum, encrypt it, log who read it, and be able to delete it (POPIA/GDPR).");
        if (p.Has(MultiTenant))
            Add(Pattern.TenantIsolation, "Tie every request and query to one tenant, with automated tests proving tenants never see each other's data.");
        if (p.Has(BatchDataFeeds))
        {
            Add(Pattern.SeparateIngestionPipeline, "Each feed gets its own scheduled, re-runnable job that lands raw data first, then cleans it; a bad file never corrupts the warehouse.");
            Add(Pattern.CachedReadModel, "Reports read from a consolidated model built for querying, never from the raw feeds.");
            Add(Pattern.AuditTrail, "Keep data lineage: which file or API call every reported number came from.");
        }
        if (p.Has(HighVolumeDeviceData))
            Add(Pattern.SeparateIngestionPipeline, "Take device readings into their own pipeline so the flood never touches the customer portal.");
        if (p.Has(LiveTracking) || p.Has(MillionsOfLiveConnections))
            Add(Pattern.ConnectionGateway, "Hold live connections in a dedicated gateway that scales separately from business logic.");
        if (p.Has(DocumentIngestion))
        {
            Add(Pattern.SeparateIngestionPipeline, "Documents arrive in bursts; queue them and process in a worker so the API stays responsive.");
            Add(Pattern.HumanInTheLoop, "AI extraction is never 100%: route low-confidence fields to a person and learn from their corrections.");
            Add(Pattern.AuditTrail, "Keep the source document, the extracted value and who approved it, so every number can be traced.");
        }
        if (style == ArchitectureStyle.Microservices)
            Add(Pattern.EventStream, "Services talk through events, so one being slow or down does not stop the rest.");

        return list;
    }

    private static List<SimilarExample> Closest(ProjectProfile p, int count) =>
        ReferenceCatalogue.All
            .Select(e => new SimilarExample(e, Jaccard(p.Traits, e.Traits)))
            .Where(s => s.Similarity > 0)
            .OrderByDescending(s => s.Similarity)
            .ThenBy(s => s.Example.Id)
            .Take(count)
            .ToList();

    internal static double Jaccard(IReadOnlySet<Trait> a, IReadOnlySet<Trait> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0;
        int intersection = a.Count(b.Contains);
        int union = a.Count + b.Count - intersection;
        return Math.Round((double)intersection / union, 3);
    }

    private static List<string> EvolveWhen(ProjectProfile p, ArchitectureStyle style)
    {
        var list = new List<string>();
        switch (style)
        {
            case ArchitectureStyle.StaticSite:
                list.Add("Users need accounts or per-user data: add a small API (layered monolith).");
                break;
            case ArchitectureStyle.LayeredMonolith:
                list.Add("Business rules start leaking into controllers or SQL: move to Clean Architecture.");
                list.Add("A second business area appears: split into modules.");
                break;
            case ArchitectureStyle.CleanArchitecture:
                list.Add("A second business area appears with its own language: make it a module of a modular monolith.");
                break;
            case ArchitectureStyle.ModularMonolith:
                list.Add("One module needs to scale or deploy on a different schedule: extract only that module as a service.");
                list.Add("Separate teams own separate modules and block each other's releases.");
                break;
            case ArchitectureStyle.Microservices:
                list.Add("Teams spend more time on infrastructure than features: merge services that always change together.");
                break;
        }
        if (p.Has(ReadHeavy)) list.Add("Search becomes the bottleneck: move it to a dedicated search service.");
        return list;
    }

    private static List<string> Risks(ProjectProfile p, ArchitectureStyle style)
    {
        var list = new List<string>();
        if (style == ArchitectureStyle.Microservices && p.Has(SmallTeam))
            list.Add("A small team running many services spends its time on operations, not features.");
        if (style == ArchitectureStyle.Microservices)
            list.Add("Distributed failures: plan tracing, timeouts and retries from day one.");
        if (style == ArchitectureStyle.ModularMonolith)
            list.Add("Modules decay into a big ball of mud unless boundaries are enforced by tests (no cross-module database access).");
        if (p.Has(MoneyMovement))
            list.Add("Floating-point money or update-in-place balances. Use decimal and ledger entries only.");
        if (p.Has(DocumentIngestion))
            list.Add("Trusting AI output blindly. Validate against a schema and keep a person in the loop.");
        if (p.Has(MultiTenant))
            list.Add("One missing tenant filter leaks a client's data. Enforce it centrally, never per query.");
        return list;
    }
}
