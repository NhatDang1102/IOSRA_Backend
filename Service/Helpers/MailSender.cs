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
                "Verify Your IOSRA Account",
                $"Your OTP code is: {otp}\nThe code expires in 5 minutes.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendOtpForgotEmailAsync(string toEmail, string otp)
        {
            using var message = BuildMessage(
                "Reset Your IOSRA Password",
                $"Your password reset OTP is: {otp}\nThe code expires in 5 minutes.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string name)
        {
            using var message = BuildMessage(
                "Welcome to IOSRA",
                $"Hello {name},\nThank you for registering an IOSRA account.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendChangeEmailOtpAsync(string newEmail, string otp)
        {
            using var message = BuildMessage(
                "Confirm Your IOSRA Email Change",
                $"You (or someone else) requested to change the IOSRA login email to this address.\n" +
                $"OTP: {otp}\nThe code expires in 5 minutes.\n\nIf you did not request this change, please ignore this email.",
                newEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendChangeEmailSuccessAsync(string oldEmail, string newEmail)
        {
            using var message = BuildMessage(
                "Your IOSRA Email Was Updated",
                $"Your IOSRA login email has been changed to: {newEmail}\n" +
                $"If you did not perform this action, contact support immediately.",
                oldEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendStoryApprovedEmailAsync(string toEmail, string storyTitle)
        {
            using var message = BuildMessage(
                "Your Story Was Approved",
                $"Congratulations! Your story \"{storyTitle}\" has been approved and is now live for readers.",
                toEmail);
            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public async Task SendStoryRejectedEmailAsync(string toEmail, string storyTitle, string? note)
        {
            var body = $"Your story \"{storyTitle}\" was rejected by our moderation team.";
            if (!string.IsNullOrWhiteSpace(note))
            {
                body += $"\nModerator note: {note}";
            }
            body += "\nPlease review the content, make adjustments, and resubmit when ready.";

            using var message = BuildMessage("Your Story Was Rejected", body, toEmail);
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
