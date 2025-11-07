using Repository.Entities;

namespace Service.Interfaces;

// Interface cho factory tạo JWT token
public interface IJwtTokenFactory
{
    // Tạo JWT token từ thông tin account và roles
    string CreateToken(account acc, IEnumerable<string>? roles = null);
}
