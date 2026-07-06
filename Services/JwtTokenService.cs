using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BackendApi.Data.Entities;
using Microsoft.IdentityModel.Tokens;

namespace BackendApi.Services;

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("session_id", sessionId.ToString()),
            new("account_type", user.AccountType.ToString()),
            new("college_id", user.CollegeId.ToString()),
        };
        if (wardStudentId is not null)
        {
            // PRT-01: scopes a parent's session to a single ward. Marks/fees endpoints
            // must check this claim against the requested studentId, never trust the route alone.
            claims.Add(new Claim("ward_id", wardStudentId.Value.ToString()));
        }

        var expiryMinutes = int.Parse(jwtSection["ExpiryMinutes"]!);
        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
