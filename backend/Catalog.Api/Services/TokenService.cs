using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Catalog.Api.DTOs;
using Microsoft.IdentityModel.Tokens;
namespace Catalog.Api.Services;

public class TokenService(IConfiguration config)
{
    public LoginResponseDto Create(int id, string name, string role)
    {
        var expires = DateTime.UtcNow.AddMinutes(60);
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
             new Claim(JwtRegisteredClaimNames.UniqueName, name),
             new Claim(ClaimTypes.Role, role),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            expires: expires, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
