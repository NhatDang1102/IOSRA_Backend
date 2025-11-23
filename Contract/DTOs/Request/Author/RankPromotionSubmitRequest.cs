using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Author
{
    public class RankPromotionSubmitRequest
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Commitment { get; set; } = string.Empty;
    }
}
