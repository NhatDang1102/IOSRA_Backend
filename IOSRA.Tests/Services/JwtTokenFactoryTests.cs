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
using Xunit;

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
        var tokenString = _factory.CreateToken(acc, roles);

        // Assert cơ bản: token không rỗng và parse được
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

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
        var roleClaims = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        roleClaims.Should().BeEquivalentTo(roles);

        // Hạn token nằm trong tương lai (không check chính xác 120 phút để tránh phụ thuộc clock)
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    // CASE: Roles = null -> không sinh claim Role
    [Fact]
    public void CreateToken_Should_Not_Contain_Role_Claims_When_Roles_Null()
    {
        var acc = MakeAccount(Guid.NewGuid());

        var tokenString = _factory.CreateToken(acc, roles: null);

        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Should().BeEmpty();
    }
}
