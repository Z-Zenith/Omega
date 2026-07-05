using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController(AppDbContext db, IPasswordHasher passwordHasher, ITotpService totpService) : ControllerBase
{
    // AWA-09: account creation. Returns the TOTP provisioning URI once, at creation time,
    // since SDA-02/TWA-03 login always requires a TOTP code alongside password.
    [HttpPost]
    public async Task<ActionResult<CreateUserResponse>> Create(CreateUserRequest request)
    {
        var totpSecret = totpService.GenerateSecret();

        var user = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = request.CollegeId,
            AccountType = request.AccountType,
            Identifier = request.Identifier,
            PasswordHash = passwordHasher.Hash(request.InitialPassword),
            TotpSecret = totpSecret,
            FullName = request.FullName,
            DepartmentId = request.DepartmentId,
            IsActive = true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var provisioningUri = totpService.BuildProvisioningUri(totpSecret, request.Identifier, "Campus Platform");
        return CreatedAtAction(nameof(Create), new { id = user.Id }, new CreateUserResponse(user.Id, provisioningUri, totpSecret));
    }

    // AWA-07, AWA-08
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Identifier,
            AccountType = user.AccountType.ToString(),
            user.CollegeId,
            user.DepartmentId,
            user.IsActive,
        });
    }

    // AWA-10
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] string newPassword)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
