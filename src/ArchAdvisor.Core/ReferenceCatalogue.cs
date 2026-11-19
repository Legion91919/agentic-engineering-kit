using static ArchAdvisor.Core.Trait;

namespace ArchAdvisor.Core;

/// <summary>
/// Twenty real-world shapes of system, each with the architecture that fits it and why.
/// Used to explain a recommendation by analogy ("this is closest to a hotel booking system").
/// </summary>
public static class ReferenceCatalogue
{
    private static HashSet<Trait> T(params Trait[] t) => new(t);

    public static IReadOnlyList<ReferenceExample> All { get; } = new List<ReferenceExample>
    {
        new(1, "Simple static site", ArchitectureStyle.StaticSite,
            "No backend at all: static hosting plus one small serverless function for a contact form.",
            "Nothing changes per user, so a server would only add cost and attack surface.",
            T(StaticContent, SmallTeam)),

        new(2, "Employee tool (leave, timesheets)", ArchitectureStyle.LayeredMonolith,
            "Monolith: layered, or Clean Architecture if the approval rules are complex.",
            "Few users, one team, one database. Split only the business rules, not the deployment.",
            T(InternalUsersOnly, SmallTeam, ComplexBusinessRules)),

        new(3, "E-commerce shop", ArchitectureStyle.ModularMonolith,
            "Modular monolith with a separate catalogue read model (document store, cache, CDN); extract search as a service when traffic grows.",
            "Browsing outnumbers buying a hundred to one, and stock is limited.",
            T(SeveralBusinessAreas, ReadHeavy, LimitedInventory, MoneyMovement, MediaDelivery)),

        new(4, "Online gambling", ArchitectureStyle.Microservices,
            "Microservices, with the wallet as an append-only ledger and every operation safe to repeat.",
            "Regulators need full history and money must never be double-spent at very high volume.",
            T(MoneyMovement, RegulatoryAudit, GlobalScale, SeveralBusinessAreas, MillionsOfLiveConnections)),

        new(5, "Video or music streaming", ArchitectureStyle.Microservices,
            "Microservices with a CDN for the media itself and an event stream for playback events.",
            "The bytes go through a CDN; the services handle catalogue, rights and a flood of playback events.",
            T(MediaDelivery, GlobalScale, ReadHeavy, SeveralBusinessAreas)),

        new(6, "Internet provider (fibre/LTE)", ArchitectureStyle.ModularMonolith,
            "Modular monolith, with sagas for weeks-long orders and adapters for the network operators.",
            "Orders wait on outside parties for weeks; each operator has its own API.",
            T(LongRunningWorkflows, ExternalPartnerIntegrations, SeveralBusinessAreas, MoneyMovement)),

        new(7, "Food delivery", ArchitectureStyle.ModularMonolith,
            "Modular monolith, but split live tracking and dispatch into their own service early.",
            "Most of the app is ordinary; live driver positions are the one part with very different load.",
            T(SeveralBusinessAreas, LiveTracking, MoneyMovement, ExternalPartnerIntegrations)),

        new(8, "Car rental", ArchitectureStyle.ModularMonolith,
            "Clean Architecture for practice, a modular monolith for a real product, microservices plus event sourcing at global scale.",
            "Limited cars that must not be double-booked, far more searches than bookings.",
            T(LimitedInventory, ReadHeavy, SeveralBusinessAreas, MoneyMovement, ComplexBusinessRules)),

        new(9, "Banking / payments app", ArchitectureStyle.ModularMonolith,
            "Modular monolith with an append-only ledger.",
            "Money must always balance and every cent must be traceable; retries must never pay twice.",
            T(MoneyMovement, RegulatoryAudit, PersonalData, SeveralBusinessAreas, ComplexBusinessRules)),

        new(10, "Hotel or airline booking", ArchitectureStyle.ModularMonolith,
            "Modular monolith with a separate cached search side; microservices at global scale.",
            "Limited rooms or seats that must not be oversold, and far more searching than booking.",
            T(LimitedInventory, ReadHeavy, MoneyMovement, ExternalPartnerIntegrations, SeveralBusinessAreas)),

        new(11, "Clinic or hospital patient system", ArchitectureStyle.CleanArchitecture,
            "Clean Architecture or modular monolith, with a full audit history.",
            "Privacy law and a record of who saw or changed what; labs and medical aids connect through adapters.",
            T(PersonalData, RegulatoryAudit, ExternalPartnerIntegrations, ComplexBusinessRules)),

        new(12, "Vehicle tracking or smart meters (IoT)", ArchitectureStyle.LayeredMonolith,
            "A separate pipeline that takes in device readings, plus a normal monolith for the customer portal.",
            "Device data is huge and constant; the portal has little traffic.",
            T(HighVolumeDeviceData, LiveTracking)),

        new(13, "Software sold to many companies (CRM)", ArchitectureStyle.ModularMonolith,
            "Modular monolith that keeps each company's data separate.",
            "Every request and query is tied to one company, with tests proving no company can see another's data.",
            T(MultiTenant, SeveralBusinessAreas, PersonalData)),

        new(14, "Chat / messaging app", ArchitectureStyle.Microservices,
            "Microservices built for live connections.",
            "Millions of open connections and heavy message writes need parts that scale on their own.",
            T(MillionsOfLiveConnections, GlobalScale, PersonalData)),

        // Finance and data-heavy back-office shapes.
        new(15, "Document extraction with human review", ArchitectureStyle.CleanArchitecture,
            "Clean Architecture API plus a worker fed by a queue; fields the model is unsure about wait for a person.",
            "Files arrive in bursts, models are sometimes wrong, and every value must point back to the page it came from.",
            T(DocumentIngestion, RegulatoryAudit, ComplexBusinessRules, SmallTeam)),

        new(16, "Reporting warehouse fed by many sources", ArchitectureStyle.LayeredMonolith,
            "One re-runnable import job per source into a single warehouse, and a plain read-only reporting app on top.",
            "The work is in cleaning and reconciling feeds, not in serving traffic, so splitting it into services buys nothing.",
            T(BatchDataFeeds, ReadHeavy, ExternalPartnerIntegrations, SmallTeam)),

        new(17, "Replacing an old system piece by piece", ArchitectureStyle.ModularMonolith,
            "Strangler fig: a routing layer sends each feature to the old system or the new modular monolith, until nothing is left on the old one.",
            "Nobody can pause the business for a rewrite, so each feature moves across only once it is proven.",
            T(LegacySystems, SeveralBusinessAreas, ExternalPartnerIntegrations)),

        new(18, "Lending back office (loans, drawdowns, repayments)", ArchitectureStyle.ModularMonolith,
            "Modular monolith (applications, servicing, cash, statements) with an append-only cash ledger and sagas for drawdowns.",
            "Loans run for years, drawdowns wait on sign-offs, and every cent has to reconcile for auditors.",
            T(MoneyMovement, RegulatoryAudit, LongRunningWorkflows, SeveralBusinessAreas, ComplexBusinessRules)),

        // Social products.
        new(19, "Photo-sharing social feed", ArchitectureStyle.Microservices,
            "At global scale: media, graph, feed and notification services, a CDN for images, and feeds pre-built per follower (fan-out on write) with a pull fallback for very popular accounts.",
            "Reads outnumber posts by orders of magnitude and images dominate bandwidth; at launch the same modules live in one modular monolith.",
            T(MediaDelivery, ReadHeavy, PersonalData, SeveralBusinessAreas, GlobalScale)),

        new(20, "Community social network (profiles, groups, events)", ArchitectureStyle.Microservices,
            "At global scale: graph, groups, feed, events and notification services with an event stream between them; start as a modular monolith with the same module boundaries.",
            "Many feature areas evolve at different speeds and notifications are pushed live; boundaries matter more than early splitting.",
            T(ReadHeavy, PersonalData, SeveralBusinessAreas, LiveTracking, GlobalScale)),
    };

    public static ReferenceExample? Find(int id) => All.FirstOrDefault(e => e.Id == id);
}
