using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IMailSender
    {
        Task SendOtpEmailAsync(string toEmail, string otp);
        Task SendOtpForgotEmailAsync(string toEmail, string otp);
        Task SendWelcomeEmailAsync(string toEmail, string name);
        Task SendChangeEmailOtpAsync(string newEmail, string otp);
        Task SendChangeEmailSuccessAsync(string oldEmail, string newEmail);
        Task SendStoryApprovedEmailAsync(string toEmail, string storyTitle);
        Task SendStoryRejectedEmailAsync(string toEmail, string storyTitle, string? note);
    }
}

