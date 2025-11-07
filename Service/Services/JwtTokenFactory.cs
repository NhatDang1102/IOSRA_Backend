using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Repository.Entities;
using Service.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Service.Implementations
{
    // Service tạo JWT token với các claims cần thiết
    public class JwtTokenFactory : IJwtTokenFactory
    {
        private readonly IConfiguration _config;
        public JwtTokenFactory(IConfiguration config) => _config = config;

        // Tạo JWT token từ account và roles
        public string CreateToken(account acc, IEnumerable<string>? roles = null)
        {
            // Lấy cấu hình JWT từ appsettings
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Tạo các claims cho token
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, acc.account_id.ToString()), // Subject: AccountId
                new(JwtRegisteredClaimNames.Email, acc.email), // Email
                new("username", acc.username), // Username (custom claim)
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID (dùng cho blacklist khi logout)
            };
            // Thêm roles vào claims
            if (roles != null)
                claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            // Thời gian hết hạn của token (mặc định 60 phút)
            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpiresMinutes"] ?? "60"));

            // Tạo JWT token
            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            // Serialize token thành string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
