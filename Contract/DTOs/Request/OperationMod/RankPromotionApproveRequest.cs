using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.OperationMod
{
    public class RankPromotionApproveRequest
    {
        [StringLength(500)]
        public string? Note { get; set; }
    }
}
