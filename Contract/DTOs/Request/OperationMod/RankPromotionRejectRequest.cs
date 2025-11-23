using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.OperationMod
{
    public class RankPromotionRejectRequest
    {
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
