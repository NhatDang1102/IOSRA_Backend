using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Tag
{
    public sealed class TagPagedItem
    {
        public Guid TagId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Usage { get; init; } 
    }
}
