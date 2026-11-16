using Ltl.Modules.Routing.Domain;
using Xunit;

namespace Ltl.UnitTests.Routing;

public class RouteSpecTests
{
    private static readonly Guid Origin = Guid.NewGuid();
    private static readonly Guid Via = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();

    [Fact]
    public void ValidStops_DirectRoute_ShouldPass()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Origin, 0),
            new RouteSpec.StopSpec(Destination, 1),
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidStops_WithViaStop_ShouldPass()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Origin, 0),
            new RouteSpec.StopSpec(Via, 1),
            new RouteSpec.StopSpec(Destination, 2),
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void TooFewStops_ShouldFail()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Origin, 0),
        });

        Assert.Contains(errors, e => e.Contains("至少"));
    }

    [Fact]
    public void GapInSequence_ShouldFail()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Origin, 0),
            new RouteSpec.StopSpec(Via, 2),   // 缺 1
            new RouteSpec.StopSpec(Destination, 3),
        });

        Assert.Contains(errors, e => e.Contains("连续"));
    }

    [Fact]
    public void DuplicateBranch_ShouldFail()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Origin, 0),
            new RouteSpec.StopSpec(Origin, 1),  // 重复
            new RouteSpec.StopSpec(Destination, 2),
        });

        Assert.Contains(errors, e => e.Contains("不得出现多次"));
    }

    [Fact]
    public void FirstStopNotOrigin_ShouldFail()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Via, 0),
            new RouteSpec.StopSpec(Destination, 1),
        });

        Assert.Contains(errors, e => e.Contains("起点"));
    }

    [Fact]
    public void LastStopNotDestination_ShouldFail()
    {
        var errors = RouteSpec.Validate(Origin, Destination, new[]
        {
            new RouteSpec.StopSpec(Origin, 0),
            new RouteSpec.StopSpec(Via, 1),
        });

        Assert.Contains(errors, e => e.Contains("终点"));
    }
}

public class TransportPlanSpecTests
{
    private static readonly Guid Origin = Guid.NewGuid();
    private static readonly Guid Via = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();
    private static readonly Guid ForeignBranch = Guid.NewGuid();

    private static readonly IReadOnlyCollection<Guid> Stops = [Origin, Via, Destination];

    [Fact]
    public void ValidDirectPlan_ShouldPass()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, Destination, 1),
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidMultiSegmentPlan_ShouldPass()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, Via, 1),
            new TransportPlanSpec.SegmentSpec(Via, Destination, 2),
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void DisconnectedSegments_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, Via, 1),
            new TransportPlanSpec.SegmentSpec(Origin, Destination, 2),  // 未与上一段相连
        });

        Assert.Contains(errors, e => e.Contains("相连"));
    }

    [Fact]
    public void SegmentOutsideRouteStops_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, ForeignBranch, 1),
        });

        Assert.Contains(errors, e => e.Contains("不在线路站点中"));
    }

    [Fact]
    public void FirstSegmentNotFromOrigin_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Via, Destination, 1),
        });

        Assert.Contains(errors, e => e.Contains("线路起点"));
    }

    [Fact]
    public void LastSegmentNotToDestination_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, Via, 1),
        });

        Assert.Contains(errors, e => e.Contains("线路终点"));
    }

    [Fact]
    public void SameFromTo_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, Origin, 1),
        });

        Assert.Contains(errors, e => e.Contains("同一网点"));
    }

    [Fact]
    public void GapInSegmentSequence_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, new[]
        {
            new TransportPlanSpec.SegmentSpec(Origin, Via, 1),
            new TransportPlanSpec.SegmentSpec(Via, Destination, 3),  // 缺 2
        });

        Assert.Contains(errors, e => e.Contains("连续"));
    }

    [Fact]
    public void EmptySegments_ShouldFail()
    {
        var errors = TransportPlanSpec.Validate(Origin, Destination, Stops, Array.Empty<TransportPlanSpec.SegmentSpec>());

        Assert.Contains(errors, e => e.Contains("至少"));
    }
}
