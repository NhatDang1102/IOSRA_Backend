using System;
using Contract.DTOs.Response.Auth;

namespace Service.Models
{
    public class AuthResult
    {
        public AuthResult(LoginResponse response, string refreshToken, DateTime refreshTokenExpiresAt)
        {
            Response = response;
            RefreshToken = refreshToken;
            RefreshTokenExpiresAt = refreshTokenExpiresAt;
        }

        public LoginResponse Response { get; }

        public string RefreshToken { get; }

        public DateTime RefreshTokenExpiresAt { get; }
    }
}
