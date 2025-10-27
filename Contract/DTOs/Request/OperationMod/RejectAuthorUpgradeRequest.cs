using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.OperationMod
{
    public class RejectAuthorUpgradeRequest
    {
        [StringLength(1000)]
        public string? Reason { get; set; }
    }
}
