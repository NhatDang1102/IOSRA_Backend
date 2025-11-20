using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment
{
    public class CreateVoicePaymentLinkRequest
    {
        [Required]
        [Range(1, ulong.MaxValue)]
        public ulong Amount { get; set; }
    }
}
