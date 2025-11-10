using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Tag
{
    public sealed class TagResolveRequest
    {
        public List<Guid> Ids { get; init; } = new();
    }
}
