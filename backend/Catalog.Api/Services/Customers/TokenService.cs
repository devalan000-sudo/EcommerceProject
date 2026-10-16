using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Catalog.Api.DTOs;
using Microsoft.IdentityModel.Tokens;
namespace Catalog.Api.Services;

public class TokenService(IConfiguration config)
{
    // Genera un JWT con identidad, rol y vigencia para el usuario autenticado.
    public LoginResponseDto Create(int id, string name, string role, Guid? securityStamp = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(60);
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
             new Claim(JwtRegisteredClaimNames.UniqueName, name),
             new Claim(ClaimTypes.Role, role),
             new Claim("security_stamp", (securityStamp ?? Guid.Empty).ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            expires: expires, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
