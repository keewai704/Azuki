using Azookey.UI.Candidate;
using Azookey.UI.Ipc;
using Xunit;

namespace Azookey.UI.Tests.Candidate;

public sealed class WindowGeometryTests
{
    private static WorkArea WorkArea() => new(0, 0, 800, 600);

    [Fact]
    public void PlacesWindowBelowWhenThereIsRoom() =>
        Assert.Equal((85, 126), WindowGeometry.CandidateWindowPosition(new WindowRect(100, 100, 120, 180), new WindowSize(240, 120), WorkArea()));

    [Fact]
    public void PlacesWindowAboveNearBottomEdge() =>
        Assert.Equal((85, 434), WindowGeometry.CandidateWindowPosition(new WindowRect(560, 100, 580, 180), new WindowSize(240, 120), WorkArea()));

    [Fact]
    public void ClampsWindowToRightEdge() =>
        Assert.Equal((560, 126), WindowGeometry.CandidateWindowPosition(new WindowRect(100, 760, 120, 780), new WindowSize(240, 120), WorkArea()));

}
