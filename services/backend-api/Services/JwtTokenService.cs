using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BackendApi.Data.Entities;
using Microsoft.IdentityModel.Tokens;

namespace BackendApi.Services;

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string IssueToken(User user, Guid sessionId)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("session_id", sessionId.ToString()),
            new Claim("account_type", user.AccountType.ToString()),
            new Claim("college_id", user.CollegeId.ToString()),
        };

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
