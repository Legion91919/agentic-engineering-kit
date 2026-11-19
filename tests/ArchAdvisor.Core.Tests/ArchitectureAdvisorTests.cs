using ArchAdvisor.Core;
using Xunit;
using static ArchAdvisor.Core.Trait;

namespace ArchAdvisor.Core.Tests;

public class ArchitectureAdvisorTests
{
    [Fact]
    public void Static_content_alone_needs_no_backend()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(StaticContent, SmallTeam));

        Assert.Equal(ArchitectureStyle.StaticSite, rec.Style);
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.ServerlessFunction);
    }

    [Fact]
    public void Simple_internal_tool_is_a_layered_monolith()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(InternalUsersOnly, SmallTeam));
        Assert.Equal(ArchitectureStyle.LayeredMonolith, rec.Style);
    }

    [Fact]
    public void Complex_rules_in_one_area_get_clean_architecture()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(InternalUsersOnly, ComplexBusinessRules));
        Assert.Equal(ArchitectureStyle.CleanArchitecture, rec.Style);
    }

    [Fact]
    public void Several_business_areas_get_a_modular_monolith()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(SeveralBusinessAreas, ReadHeavy));
        Assert.Equal(ArchitectureStyle.ModularMonolith, rec.Style);
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.CachedReadModel);
    }

    [Fact]
    public void Global_scale_with_a_real_team_gets_microservices_and_events()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(GlobalScale, MillionsOfLiveConnections));
        Assert.Equal(ArchitectureStyle.Microservices, rec.Style);
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.EventStream);
    }

    [Fact]
    public void Small_team_at_scale_is_steered_to_a_modular_monolith()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(GlobalScale, SmallTeam));
        Assert.Equal(ArchitectureStyle.ModularMonolith, rec.Style);
        Assert.Contains("small team", rec.StyleReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Money_always_brings_ledger_idempotency_and_outbox()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(MoneyMovement));
        var patterns = rec.Patterns.Select(p => p.Pattern).ToList();

        Assert.Contains(Pattern.AppendOnlyLedger, patterns);
        Assert.Contains(Pattern.IdempotencyKeys, patterns);
        Assert.Contains(Pattern.TransactionalOutbox, patterns);
        Assert.Contains(rec.Risks, r => r.Contains("decimal", StringComparison.Ordinal));
    }

    [Fact]
    public void Document_ingestion_keeps_a_human_in_the_loop()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(DocumentIngestion, RegulatoryAudit));
        var patterns = rec.Patterns.Select(p => p.Pattern).ToList();

        Assert.Contains(Pattern.HumanInTheLoop, patterns);
        Assert.Contains(Pattern.SeparateIngestionPipeline, patterns);
        Assert.Single(patterns, Pattern.AuditTrail); // added by two rules, listed once
    }

    [Fact]
    public void Legacy_systems_are_replaced_with_a_strangler_fig()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(LegacySystems, SeveralBusinessAreas));
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.StranglerFig);
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.AdapterPerPartner);
    }

    [Fact]
    public void Multi_tenant_systems_must_prove_isolation()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(MultiTenant, SeveralBusinessAreas));
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.TenantIsolation);
        Assert.Contains(rec.Risks, r => r.Contains("tenant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Closest_example_for_a_booking_profile_is_a_booking_system()
    {
        var profile = ProjectProfile.Of(LimitedInventory, ReadHeavy, MoneyMovement, ExternalPartnerIntegrations, SeveralBusinessAreas);
        var rec = ArchitectureAdvisor.Recommend(profile);

        Assert.Equal(10, rec.ClosestExamples[0].Example.Id);
        Assert.Equal(1.0, rec.ClosestExamples[0].Similarity);
    }

    [Theory]
    [InlineData(1, ArchitectureStyle.StaticSite)]
    [InlineData(5, ArchitectureStyle.Microservices)]
    [InlineData(6, ArchitectureStyle.ModularMonolith)]
    [InlineData(9, ArchitectureStyle.ModularMonolith)]
    [InlineData(13, ArchitectureStyle.ModularMonolith)]
    [InlineData(14, ArchitectureStyle.Microservices)]
    [InlineData(15, ArchitectureStyle.CleanArchitecture)]
    [InlineData(16, ArchitectureStyle.LayeredMonolith)]
    [InlineData(17, ArchitectureStyle.ModularMonolith)]
    [InlineData(18, ArchitectureStyle.ModularMonolith)]
    [InlineData(19, ArchitectureStyle.Microservices)]
    [InlineData(20, ArchitectureStyle.Microservices)]
    public void Advisor_agrees_with_reference_examples(int exampleId, ArchitectureStyle expected)
    {
        var example = ReferenceCatalogue.Find(exampleId)!;
        var rec = ArchitectureAdvisor.Recommend(new ProjectProfile(example.Traits));
        Assert.Equal(expected, rec.Style);
    }

    [Fact]
    public void Same_input_always_gives_the_same_answer()
    {
        var p = ProjectProfile.Of(MoneyMovement, RegulatoryAudit, PersonalData);
        var a = ArchitectureAdvisor.Recommend(p);
        var b = ArchitectureAdvisor.Recommend(p);

        Assert.Equal(a.Style, b.Style);
        Assert.Equal(a.Patterns.Select(x => x.Pattern), b.Patterns.Select(x => x.Pattern));
    }

    [Fact]
    public void Catalogue_has_twenty_uniquely_numbered_examples()
    {
        Assert.Equal(20, ReferenceCatalogue.All.Count);
        Assert.Equal(20, ReferenceCatalogue.All.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Jaccard_is_zero_for_disjoint_and_one_for_equal_sets()
    {
        var a = new HashSet<Trait> { MoneyMovement };
        var b = new HashSet<Trait> { MediaDelivery };
        Assert.Equal(0, ArchitectureAdvisor.Jaccard(a, b));
        Assert.Equal(1, ArchitectureAdvisor.Jaccard(a, new HashSet<Trait> { MoneyMovement }));
    }

    [Fact]
    public void Every_example_matches_what_the_advisor_would_recommend()
    {
        // #2 lists "layered, or Clean Architecture if the approval rules are complex"; its traits include
        // ComplexBusinessRules, so the advisor picks the second option on purpose.
        foreach (var e in ReferenceCatalogue.All.Where(e => e.Id != 2))
            Assert.Equal(e.Style, ArchitectureAdvisor.Recommend(new ProjectProfile(e.Traits)).Style);
    }

    [Fact]
    public void Document_processing_profile_is_closest_to_the_idp_reference()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(DocumentIngestion, RegulatoryAudit, SmallTeam));
        Assert.Equal(15, rec.ClosestExamples[0].Example.Id);
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.HumanInTheLoop);
    }

    [Fact]
    public void Batch_feeds_get_an_ingestion_pipeline_and_lineage()
    {
        var rec = ArchitectureAdvisor.Recommend(ProjectProfile.Of(BatchDataFeeds, ReadHeavy));
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.SeparateIngestionPipeline);
        Assert.Contains(rec.Patterns, p => p.Pattern == Pattern.AuditTrail);
    }
}
