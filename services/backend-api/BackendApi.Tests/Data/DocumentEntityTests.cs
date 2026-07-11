using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Data;

// #84 — Document.cs previously had no DocType/OcrStatus properties and AppDbContext had zero
// modelBuilder.Entity<Document>() configuration, so db.Documents.Add(new Document {...}) would
// throw an unhandled NOT NULL violation against doc_type (db/init/01_schema.sql defines
// doc_type NOT NULL with no default). This locks in that Document can be created and persisted
// with DocType/OcrStatus set, and that OcrStatus defaults sensibly like the SQL schema intends.
public class DocumentEntityTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Document_CanBeCreated_WithDocTypeAndOcrStatusSet()
    {
        await using var db = NewDb();
        var owner = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = Guid.NewGuid(),
            Identifier = "owner-1",
            PasswordHash = "hash",
            FullName = "Doc Owner",
            IsActive = true,
            AccountType = AccountType.Student,
        };
        db.Users.Add(owner);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            FileUrl = "https://files.campus.local/doc.pdf",
            DocType = DocType.Pdf,
            OcrStatus = OcrStatus.Pending,
        };
        db.Documents.Add(document);

        await db.SaveChangesAsync();

        var saved = await db.Documents.SingleAsync(d => d.Id == document.Id);
        Assert.Equal(DocType.Pdf, saved.DocType);
        Assert.Equal(OcrStatus.Pending, saved.OcrStatus);
    }
}
