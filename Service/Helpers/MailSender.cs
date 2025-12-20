using System;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using System.Net;
using System.Net.Mail;

namespace Service.Helpers
{
    public class MailSender : IMailSender
    {
        private readonly SmtpSettings _smtp;

        public MailSender(IOptions<SmtpSettings> smtpOptions)
        {
            _smtp = smtpOptions.Value;
        }

        private SmtpClient CreateClient() => new(_smtp.Host, _smtp.Port)
        {
            EnableSsl = _smtp.EnableSsl,
            Credentials = new NetworkCredential(_smtp.FromEmail, _smtp.AppPassword)
        };

        public async Task SendOtpEmailAsync(string toEmail, string otp)
        {
            using var message = BuildMessage(
                "Xác minh OTP",
                $"Mã của bạn là : {otp}\nHết hạn sau 5 phút.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendOtpForgotEmailAsync(string toEmail, string otp)
        {
            using var message = BuildMessage(
                "Quên mật khẩu",
                $"Mã của bạn là : {otp}\nHết hạn sau 5 phút.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string name)
        {
            using var message = BuildMessage(
                "Chào mừng đến với IOSRA - Toranovel",
                $"Cảm ơn {name},\nđã tham gia hệ thống của chúng tôi. Chúc bạn có những trải nghiệm đọc và viết tuyệt vời",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendChangeEmailOtpAsync(string newEmail, string otp)
        {
            using var message = BuildMessage(
                "Xác minh đổi mail",
                $"Bạn hoặc 1 người nào đó đã yêu cầu đổi mail.\n" +
                $"Mã của bạn là: {otp}\nHết hạn sau 5 phút..\n\nNếu bạn không yêu cầu, hãy bỏ qua mail này",
                newEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendChangeEmailSuccessAsync(string oldEmail, string newEmail)
        {
            using var message = BuildMessage(
                "Mail của bạn đã được thay đổi thành công",
                $"Mail mới của bạn: {newEmail}\n" +
                $"Nếu bạn không thực hiện yêu cầu này, hãy liên hệ chúng tôi ngay.",
                oldEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendStoryApprovedEmailAsync(string toEmail, string storyTitle)
        {
            using var message = BuildMessage(
                "Truyện của bạn đã được phê duyệt",
                $"Chúc mừng, truyện \"{storyTitle}\" của bạn đã được phê duyệt và xuất bản thành công.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendStoryRejectedEmailAsync(string toEmail, string storyTitle, string? note)
        {
            var body = $"Rất tiếc, truyện \"{storyTitle}\" đã bị từ chối.";
            if (!string.IsNullOrWhiteSpace(note))
            {
                body += $"\nNote của Moderator: {note}";
            }
            body += "\nBạn có thể rút lại bản nháp, điều chỉnh theo note của chúng tôi, và sau đó nộp lại. Thân ái!";

            using var message = BuildMessage("Truyện đã bị từ chối", body, toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendChapterApprovedEmailAsync(string toEmail, string storyTitle, string chapterTitle)
        {
            using var message = BuildMessage(
                "Chương của bạn đã được phê duyệt",
                $"Chúc mừng, chương \"{chapterTitle}\" của truyện \"{storyTitle}\" đã được xuất bản thành công!.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendChapterRejectedEmailAsync(string toEmail, string storyTitle, string chapterTitle, string? note)
        {
            var body = $"Chương \"{chapterTitle}\" của truyện \"{storyTitle}\" đã bị chúng tôi từ chối.";
            if (!string.IsNullOrWhiteSpace(note))
            {
                body += $"\nNote của Moderator: {note}";
            }
            body += "\nBạn có thể rút lại bản nháp và điều chỉnh lại, sau đó đăng lại cho chúng tôi kiểm duyệt.";

            using var message = BuildMessage("Chương đã bị từ chối", body, toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendStrikeWarningEmailAsync(string toEmail, string username, string reason, byte strikeCount, DateTime? restrictedUntil)
        {
            var body = $"Thân chào {username},\n"
                       + $"{reason}\n"
                       + $"Mức độ hạn chế hiện tại: {strikeCount}.";

            if (restrictedUntil.HasValue)
            {
                body += $"\nTài khoản của bạn bị hạn chế đăng truyện, chương, bình luận mới đến {restrictedUntil.Value:yyyy-MM-dd HH:mm:ss} (GMT+7).";
            }

            body += "\nNếu muốn kháng cáo, hãy liên hệ chúng tôi";

            using var message = BuildMessage("Thông báo hạn chế!", body, toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        private MailMessage BuildMessage(string subject, string body, string toEmail)
        {
            var message = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(toEmail);
            return message;
        }
    }
}
