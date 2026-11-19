using ArchAdvisor.Core;
using Xunit;
using static ArchAdvisor.Core.Trait;

namespace ArchAdvisor.Core.Tests;

public class ArchitectureComparisonTests
{
    [Fact]
    public void Every_reference_system_gets_exactly_one_recommended_option_in_the_advisors_family()
    {
        foreach (var e in ReferenceCatalogue.All)
        {
            var profile = new ProjectProfile(e.Traits);
            var results = ArchitectureComparison.Compare(profile);
            var recommended = Assert.Single(results, r => r.Fit == Fit.Recommended);
            Assert.Equal(ArchitectureAdvisor.Recommend(profile).Style, recommended.Candidate.Family);
            Assert.Equal(ArchitectureComparison.Candidates.Count, results.Count);
        }
    }

    [Fact]
    public void Static_content_recommends_the_static_site_and_rejects_microservices()
    {
        var r = ArchitectureComparison.Compare(ProjectProfile.Of(StaticContent));
        Assert.Equal("static", r[0].Candidate.Id);
        Assert.Equal(Fit.Poor, r.Single(a => a.Candidate.Id == "microservices").Fit);
    }

    [Fact]
    public void Global_live_product_without_small_team_recommends_event_driven_services()
    {
        var r = ArchitectureComparison.Compare(ProjectProfile.Of(MillionsOfLiveConnections, GlobalScale, PersonalData));
        Assert.Equal("event-driven", r[0].Candidate.Id);
        Assert.Equal(Fit.Poor, r.Single(a => a.Candidate.Id == "layered").Fit);
        Assert.Equal(Fit.Poor, r.Single(a => a.Candidate.Id == "serverless").Fit);
    }

    [Fact]
    public void Small_team_at_scale_gets_a_modular_monolith_and_microservices_are_a_poor_fit()
    {
        var r = ArchitectureComparison.Compare(ProjectProfile.Of(MediaDelivery, ReadHeavy, GlobalScale, SmallTeam));
        Assert.Equal("modular-cqrs", r[0].Candidate.Id);
        var micro = r.Single(a => a.Candidate.Id == "microservices");
        Assert.Equal(Fit.Poor, micro.Fit);
        Assert.Contains(micro.Reasons, x => x.Contains("small team", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Event_sourcing_is_poor_without_money_audit_or_workflows_but_viable_with_them()
    {
        var plain = ArchitectureComparison.Compare(ProjectProfile.Of(SeveralBusinessAreas, SmallTeam));
        Assert.Equal(Fit.Poor, plain.Single(a => a.Candidate.Id == "event-sourced").Fit);

        var bank = ArchitectureComparison.Compare(ProjectProfile.Of(MoneyMovement, RegulatoryAudit, SeveralBusinessAreas, SmallTeam));
        var es = bank.Single(a => a.Candidate.Id == "event-sourced");
        Assert.Equal(Fit.GoodAlternative, es.Fit);
        Assert.Contains(es.Reasons, x => x.StartsWith("Audit:", StringComparison.Ordinal));
    }

    [Fact]
    public void Results_are_ordered_by_fit_then_complexity_and_every_option_has_a_reason()
    {
        var r = ArchitectureComparison.Compare(ProjectProfile.Of(ComplexBusinessRules, DocumentIngestion, SmallTeam));
        for (int i = 1; i < r.Count; i++)
            Assert.True(r[i - 1].Fit < r[i].Fit || (r[i - 1].Fit == r[i].Fit && r[i - 1].Candidate.Complexity <= r[i].Candidate.Complexity));
        Assert.All(r, a => Assert.NotEmpty(a.Reasons));
    }
}
