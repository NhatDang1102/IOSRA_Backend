using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Author
{
    public class SubmitAuthorUpgradeRequest
    {
        [StringLength(2000)]
        public string? Commitment { get; set; }
    }
}
