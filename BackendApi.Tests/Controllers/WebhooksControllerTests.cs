using System.Text.Json;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using BackendApi.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BackendApi.Tests.Controllers;

public class WebhooksControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static WebhooksController NewController(AppDbContext db, FakeCopyleaksClient? copyleaks = null, string secret = "test-secret")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Copyleaks:WebhookSecret"] = secret })
            .Build();
        return new WebhooksController(db, configuration, copyleaks ?? new FakeCopyleaksClient());
    }

    private static Submission NewSubmission() => new()
    {
        Id = Guid.NewGuid(),
        AssignmentId = Guid.NewGuid(),
        StudentId = Guid.NewGuid(),
        ContentUrl = "an essay",
        SubmittedAt = DateTime.UtcNow,
        IsLate = false,
        IsAutosubmitted = false,
    };

    // AIS-02: an arbitrary caller without the configured secret must not be able to
    // inject a fake plagiarism result into a submission's report.
    [Fact]
    public async Task Ais02_CopyleaksResult_RejectsRequestsWithTheWrongSecret()
    {
        await using var db = NewDb();
        var submission = NewSubmission();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.CopyleaksResult(
            submission.Id.ToString("N"), "completed", "wrong-secret", JsonDocument.Parse("{}").RootElement);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(await db.PlagiarismReports.ToListAsync());
    }

    [Fact]
    public async Task Ais02_CopyleaksResult_IgnoresNonCompletedStatuses()
    {
        await using var db = NewDb();
        var submission = NewSubmission();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.CopyleaksResult(
            submission.Id.ToString("N"), "error", "test-secret", JsonDocument.Parse("{}").RootElement);

        Assert.IsType<OkResult>(result);
        Assert.Empty(await db.PlagiarismReports.ToListAsync());
    }

    [Fact]
    public async Task Ais02_CopyleaksResult_ReturnsNotFound_ForAnUnknownSubmission()
    {
        await using var db = NewDb();
        var controller = NewController(db);

        var result = await controller.CopyleaksResult(
            Guid.NewGuid().ToString("N"), "completed", "test-secret", JsonDocument.Parse("{}").RootElement);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Ais02_CopyleaksResult_PersistsTheParsedReport_OnCompletion()
    {
        await using var db = NewDb();
        var submission = NewSubmission();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var fakeCopyleaks = new FakeCopyleaksClient
        {
            WebhookResult = new PlagiarismScanResult(0.42, ["https://example.com/a", "https://example.com/b"]),
        };
        var controller = NewController(db, fakeCopyleaks);

        var result = await controller.CopyleaksResult(
            submission.Id.ToString("N"), "completed", "test-secret", JsonDocument.Parse("{}").RootElement);

        Assert.IsType<OkResult>(result);
        var report = Assert.Single(await db.PlagiarismReports.ToListAsync());
        Assert.Equal(submission.Id, report.SubmissionId);
        Assert.Equal(0.42m, report.SimilarityScore);
        Assert.Equal(submission.Id.ToString("N"), report.CopyleaksScanId);
    }

    [Fact]
    public async Task Ais02_CopyleaksResult_ReCheckReplacesThePreviousReport()
    {
        await using var db = NewDb();
        var submission = NewSubmission();
        db.Submissions.Add(submission);
        db.PlagiarismReports.Add(new PlagiarismReport
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            SimilarityScore = 0.99m,
            CopyleaksScanId = submission.Id.ToString("N"),
            CheckedAt = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var fakeCopyleaks = new FakeCopyleaksClient { WebhookResult = new PlagiarismScanResult(0.05, []) };
        var controller = NewController(db, fakeCopyleaks);

        await controller.CopyleaksResult(submission.Id.ToString("N"), "completed", "test-secret", JsonDocument.Parse("{}").RootElement);

        var report = Assert.Single(await db.PlagiarismReports.ToListAsync());
        Assert.Equal(0.05m, report.SimilarityScore);
    }
}
