using System;
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
        Task SendChapterApprovedEmailAsync(string toEmail, string storyTitle, string chapterTitle);
        Task SendChapterRejectedEmailAsync(string toEmail, string storyTitle, string chapterTitle, string? note);
        Task SendStrikeWarningEmailAsync(string toEmail, string username, string reason, byte strikeCount, DateTime? restrictedUntil);
        Task SendAuthorBanNotificationAsync(string toEmail, string username, long balance, long pending);
    }
}

