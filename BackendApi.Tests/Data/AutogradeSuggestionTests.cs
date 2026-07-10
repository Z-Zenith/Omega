using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Data;

/// <summary>
/// Covers #91: confidence/matched_criteria/feedback columns added to
/// autograde_suggestions so AIS-04's advisory output (see
/// services/ai-services/src/autograde.py) has somewhere to persist.
/// </summary>
public class AutogradeSuggestionTests
{
    private static AppDbContext NewDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Confidence_MatchedCriteria_Feedback_RoundTripThroughDb()
    {
        var dbName = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();

        // AIS-04's AutogradeSuggestion TypedDict returns matched_criteria and feedback
        // as JSON arrays of strings — matched_criteria is the list of satisfied rubric
        // criterion names, feedback is one note per criterion.
        const string matchedCriteriaJson = "[\"thesis_statement\",\"citations\"]";
        const string feedbackJson =
            "[\"Criterion 'thesis_statement' satisfied.\",\"Criterion 'citations' not found in submission.\"]";

        using (var db = NewDb(dbName))
        {
            db.Submissions.Add(new Submission
            {
                Id = submissionId,
                AssignmentId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                ContentUrl = "https://example.test/submission.txt",
                SubmittedAt = DateTime.UtcNow,
            });

            db.AutogradeSuggestions.Add(new AutogradeSuggestion
            {
                Id = suggestionId,
                SubmissionId = submissionId,
                SuggestedGrade = 72.5m,
                Confidence = 0.65m,
                MatchedCriteria = matchedCriteriaJson,
                Feedback = feedbackJson,
            });

            await db.SaveChangesAsync();
        }

        // Reload through a fresh context sharing the same in-memory store to prove the
        // values actually round-trip through persistence, not just object identity.
        using (var freshDb = NewDb(dbName))
        {
            var stored = await freshDb.AutogradeSuggestions.SingleAsync(s => s.Id == suggestionId);

            Assert.Equal(72.5m, stored.SuggestedGrade);
            Assert.Equal(0.65m, stored.Confidence);
            Assert.Equal(matchedCriteriaJson, stored.MatchedCriteria);
            Assert.Equal(feedbackJson, stored.Feedback);
            Assert.False(stored.ConfirmedByTeacher);
            Assert.Null(stored.ConfirmedAt);
        }
    }

    [Fact]
    public async Task Confidence_MatchedCriteria_Feedback_AreNullable()
    {
        var dbName = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();

        using (var db = NewDb(dbName))
        {
            db.Submissions.Add(new Submission
            {
                Id = submissionId,
                AssignmentId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                ContentUrl = "https://example.test/submission.txt",
                SubmittedAt = DateTime.UtcNow,
            });

            db.AutogradeSuggestions.Add(new AutogradeSuggestion
            {
                Id = suggestionId,
                SubmissionId = submissionId,
                SuggestedGrade = 40m,
            });

            await db.SaveChangesAsync();
        }

        using (var freshDb = NewDb(dbName))
        {
            var stored = await freshDb.AutogradeSuggestions.SingleAsync(s => s.Id == suggestionId);

            Assert.Null(stored.Confidence);
            Assert.Null(stored.MatchedCriteria);
            Assert.Null(stored.Feedback);
        }
    }
}
