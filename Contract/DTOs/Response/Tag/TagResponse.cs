using System;

namespace Contract.DTOs.Response.Tag
{
    public class TagResponse
    {
        public Guid TagId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
