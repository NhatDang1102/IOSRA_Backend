using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;


namespace Main.Tests.Common
{
    public static class ControllerContextFactory
    {
        public static ControllerContext WithUser(ulong? sub = null, Dictionary<string, string>? extra = null)
        {
            var claims = new List<Claim>();
            if (sub.HasValue) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub.Value.ToString()));
            if (extra != null) claims.AddRange(extra.Select(kv => new Claim(kv.Key, kv.Value)));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            var httpContext = new DefaultHttpContext { User = principal };
            return new ControllerContext { HttpContext = httpContext };
        }

        public static ControllerContext EmptyUser()
        {
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            return new ControllerContext { HttpContext = httpContext };
        }
    }
}
