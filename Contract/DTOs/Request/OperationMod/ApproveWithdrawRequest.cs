using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.OperationMod
{
    public class ApproveWithdrawRequest
    {
        [StringLength(200)]
        public string? Note { get; set; }

        [StringLength(64)]
        public string? TransactionCode { get; set; }
    }
}
