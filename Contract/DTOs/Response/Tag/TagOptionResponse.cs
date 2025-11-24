using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Tag
{
    public sealed class TagOptionResponse
    {
        public Guid Value { get; init; }
        public string Label { get; init; } = string.Empty;
        public int? Usage { get; init; }
    }
}
