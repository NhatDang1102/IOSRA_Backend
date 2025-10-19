using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Service.Helpers;

public interface IMailSender
{
    Task SendOtpEmailAsync(string toEmail, string otp);
    Task SendOtpForgotEmailAsync(string toEmail, string otp);
    Task SendWelcomeEmailAsync(string toEmail, string name);
}

public class MailSender : IMailSender
{
    private readonly SmtpSettings _smtp;
    public MailSender(IOptions<SmtpSettings> smtpOptions) => _smtp = smtpOptions.Value;

    private SmtpClient CreateClient() => new(_smtp.Host, _smtp.Port)
    {
        EnableSsl = _smtp.EnableSsl,
        Credentials = new NetworkCredential(_smtp.FromEmail, _smtp.AppPassword)
    };

    public async Task SendOtpEmailAsync(string toEmail, string otp)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = "Xác nhận đăng ký tài khoản - IOSRA",
            Body = $"Mã OTP của bạn là: {otp}\nMã sẽ hết hạn sau 5 phút.",
            IsBodyHtml = false
        };
        msg.To.Add(toEmail);
        using var client = CreateClient();
        await client.SendMailAsync(msg);
    }

    public async Task SendOtpForgotEmailAsync(string toEmail, string otp)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = "Yêu cầu đặt lại mật khẩu - IOSRA",
            Body = $"Mã OTP đặt lại mật khẩu của bạn là: {otp}\nMã sẽ hết hạn sau 5 phút.",
            IsBodyHtml = false
        };
        msg.To.Add(toEmail);
        using var client = CreateClient();
        await client.SendMailAsync(msg);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string name)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = "Chào mừng đến với IOSRA!",
            Body = $"Xin chào {name},\nCảm ơn bạn đã đăng ký thành công tài khoản tại IOSRA.",
            IsBodyHtml = false
        };
        msg.To.Add(toEmail);
        using var client = CreateClient();
        await client.SendMailAsync(msg);
    }
}
