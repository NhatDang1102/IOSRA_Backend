using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IReaderRepository
    {
        Task<reader> AddAsync(reader entity, CancellationToken ct = default);
    }
}
