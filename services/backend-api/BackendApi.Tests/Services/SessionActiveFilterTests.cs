using System.Security.Claims;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Services;

public class SessionActiveFilterTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AuthorizationFilterContext NewContext(ClaimsPrincipal user, bool allowAnonymous = false)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var filters = allowAnonymous
            ? new List<IFilterMetadata> { new AllowAnonymousFilter() }
            : new List<IFilterMetadata>();
        return new AuthorizationFilterContext(actionContext, filters);
    }

    private static ClaimsPrincipal AuthenticatedUser(Guid userId, Guid sessionId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("session_id", sessionId.ToString())],
        "TestAuth"));

    // #77 — a token from a revoked (IsActive=false) session must be rejected globally, not
    // just when AuthController happens to re-check it.
    [Fact]
    public async Task RejectsRequest_WhenSessionIsRevoked()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = userId, IsActive = false });
        await db.SaveChangesAsync();

        var filter = new SessionActiveFilter(new SessionActivityService(db));
        var context = NewContext(AuthenticatedUser(userId, sessionId));

        await filter.OnAuthorizationAsync(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(context.Result);
    }

    // #77 — a session row belonging to a different user than the token's subject claim must
    // also be rejected (defends against a session_id claim being replayed with a mismatched sub).
    [Fact]
    public async Task RejectsRequest_WhenSessionBelongsToDifferentUser()
    {
        await using var db = NewDb();
        var sessionOwnerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = sessionOwnerId, IsActive = true });
        await db.SaveChangesAsync();

        var filter = new SessionActiveFilter(new SessionActivityService(db));
        var context = NewContext(AuthenticatedUser(callerId, sessionId));

        await filter.OnAuthorizationAsync(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task AllowsRequest_WhenSessionIsActiveAndOwnedByCaller()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = userId, IsActive = true });
        await db.SaveChangesAsync();

        var filter = new SessionActiveFilter(new SessionActivityService(db));
        var context = NewContext(AuthenticatedUser(userId, sessionId));

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task SkipsCheck_ForAllowAnonymousEndpoints()
    {
        await using var db = NewDb();
        var filter = new SessionActiveFilter(new SessionActivityService(db));
        // No session row exists at all for this session_id — would fail the check if it ran.
        var context = NewContext(AuthenticatedUser(Guid.NewGuid(), Guid.NewGuid()), allowAnonymous: true);

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task SkipsCheck_ForUnauthenticatedRequests()
    {
        await using var db = NewDb();
        var filter = new SessionActiveFilter(new SessionActivityService(db));
        var context = NewContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }
}
