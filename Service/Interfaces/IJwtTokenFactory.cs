using Repository.Entities;

namespace Service.Interfaces;

public interface IJwtTokenFactory
{
    string CreateToken(account acc, IEnumerable<string>? roles = null);
}
