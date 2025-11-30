using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.AIChat
{
    public class AiChatSendRequest
    {
        [Required]
        [StringLength(2000, ErrorMessage = "Message must be less than 2000 characters.")]
        public string Message { get; set; } = string.Empty;
    }
}
