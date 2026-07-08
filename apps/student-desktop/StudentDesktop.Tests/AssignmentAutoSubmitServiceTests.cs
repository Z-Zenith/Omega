using StudentDesktop.Services;

namespace StudentDesktop.Tests;

// SDA-22: IsAssignmentOpen is the single signal both auto-submit (SDA-11) and the
// clipboard block key off, so it must accurately reflect BeginSession/EndSession.
public class AssignmentAutoSubmitServiceTests
{
    private static ActiveAssignmentSession NewSession(Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        SubmissionFormat: "text/plain",
        SubmissionWindowStart: DateTime.UtcNow.AddMinutes(-5),
        SubmissionWindowEnd: DateTime.UtcNow.AddMinutes(30),
        GetCurrentContentUrl: () => "content");

    [Fact]
    public void IsAssignmentOpen_FalseByDefault()
    {
        var service = new AssignmentAutoSubmitService(new ApiClient());

        Assert.False(service.IsAssignmentOpen);
    }

    [Fact]
    public void IsAssignmentOpen_TrueAfterBeginSession()
    {
        var service = new AssignmentAutoSubmitService(new ApiClient());

        service.BeginSession(NewSession());

        Assert.True(service.IsAssignmentOpen);
    }

    [Fact]
    public void IsAssignmentOpen_FalseAfterEndSession()
    {
        var service = new AssignmentAutoSubmitService(new ApiClient());
        var assignmentId = Guid.NewGuid();

        service.BeginSession(NewSession(assignmentId));
        service.EndSession(assignmentId);

        Assert.False(service.IsAssignmentOpen);
    }

    [Fact]
    public void IsAssignmentOpen_UnaffectedByEndSessionForADifferentAssignment()
    {
        var service = new AssignmentAutoSubmitService(new ApiClient());
        service.BeginSession(NewSession());

        service.EndSession(Guid.NewGuid());

        Assert.True(service.IsAssignmentOpen);
    }
}
