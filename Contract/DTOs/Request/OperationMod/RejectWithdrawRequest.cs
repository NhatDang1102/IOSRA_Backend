using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.OperationMod
{
    public class RejectWithdrawRequest
    {
        [Required]
        [StringLength(300)]
        public string Note { get; set; } = null!;
    }
}
