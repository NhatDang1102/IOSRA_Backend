using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Repository.Entities;
using Service.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Service.Implementations
{
    public class JwtTokenFactory : IJwtTokenFactory
    {
        private readonly IConfiguration _config;
        public JwtTokenFactory(IConfiguration config) => _config = config;

        public string CreateToken(account acc, IEnumerable<string>? roles = null)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, acc.account_id.ToString()),
                new(JwtRegisteredClaimNames.Email, acc.email),
                new("username", acc.username),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            if (roles != null)
                claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpiresMinutes"] ?? "60"));

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
