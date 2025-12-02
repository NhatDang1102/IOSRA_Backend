using Repository.Entities;
using Service.Models;

namespace Service.Interfaces;

public interface IJwtTokenFactory
{
    JwtTokenResult CreateToken(account acc, IEnumerable<string>? roles = null);
}
