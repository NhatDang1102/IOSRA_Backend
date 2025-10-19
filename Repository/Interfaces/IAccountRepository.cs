using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAccountRepository
    {
        Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default);
        Task<account> AddAsync(account entity, CancellationToken ct = default);


        Task<account?> FindByIdentifierAsync(string identifier, CancellationToken ct = default); // email hoặc username

    }
}
