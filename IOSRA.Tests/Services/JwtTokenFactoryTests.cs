using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Contract.DTOs.Response.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Repository.Entities;
using Service.Implementations; // JwtTokenFactory
using Service.Interfaces;      // IJwtTokenFactory
using Service.Models;          // JwtTokenResult
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class JwtTokenFactoryTests
    {
        private readonly IJwtTokenFactory _factory;

        public JwtTokenFactoryTests()
        {
            // Config in-memory cho phần Jwt
            var dict = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-key-for-jwt-tests-123456", // đủ dài cho HMAC
                ["Jwt:Issuer"] = "iosra-test-issuer",
                ["Jwt:Audience"] = "iosra-test-audience",
                ["Jwt:ExpiresMinutes"] = "120"
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(dict)
                .Build();

            _factory = new JwtTokenFactory(config);
        }

        private static account MakeAccount(Guid id) => new account
        {
            account_id = id,
            email = "user@test.com",
            username = "user01",
            status = "unbanned",
            strike = 0,
            password_hash = "hash"
        };

        // CASE: Tạo token đầy đủ claim + issuer/audience + roles
        [Fact]
        public void CreateToken_Should_Include_Account_Claims_And_Roles()
        {
            var accId = Guid.NewGuid();
            var acc = MakeAccount(accId);
            var roles = new[] { "reader", "author" };

            // Act
            var tokenResult = _factory.CreateToken(acc, roles);

            // Assert cơ bản: token không rỗng
            tokenResult.Token.Should().NotBeNullOrWhiteSpace();
            tokenResult.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokenResult.Token);

            // Issuer / Audience từ config
            jwt.Issuer.Should().Be("iosra-test-issuer");
            jwt.Audiences.Should().ContainSingle("iosra-test-audience");

            // Claims chính
            var claims = jwt.Claims.ToList();

            claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)
                  ?.Value.Should().Be(accId.ToString());

            claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)
                  ?.Value.Should().Be("user@test.com");

            claims.FirstOrDefault(c => c.Type == "username")
                  ?.Value.Should().Be("user01");

            // Jti phải có và không rỗng
            claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)
                  ?.Value.Should().NotBeNullOrWhiteSpace();

            // Roles
            var roleClaims = claims.Where(c => c.Type == ClaimTypes.Role)
                                   .Select(c => c.Value)
                                   .ToList();
            roleClaims.Should().BeEquivalentTo(roles);

            // Hạn token nằm trong tương lai
            jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
        }

        // CASE: Roles = null -> không sinh claim Role
        [Fact]
        public void CreateToken_Should_Not_Contain_Role_Claims_When_Roles_Null()
        {
            var acc = MakeAccount(Guid.NewGuid());

            var tokenResult = _factory.CreateToken(acc, roles: null);

            tokenResult.Token.Should().NotBeNullOrWhiteSpace();

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokenResult.Token);

            jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Should().BeEmpty();
        }
    }
}
