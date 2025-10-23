using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Service.Helpers;

public interface IMailSender
{
    Task SendOtpEmailAsync(string toEmail, string otp);                 // Đăng ký tài khoản
    Task SendOtpForgotEmailAsync(string toEmail, string otp);           // Quên mật khẩu
    Task SendWelcomeEmailAsync(string toEmail, string name);            // Chào mừng đăng ký

    Task SendChangeEmailOtpAsync(string newEmail, string otp);          // ⬅️ MỚI: OTP đổi email (gửi tới email MỚI)
    Task SendChangeEmailSuccessAsync(string oldEmail, string newEmail); // ⬅️ MỚI: Thông báo đổi email thành công (gửi tới email CŨ)
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

    // ⬇️ MỚI: gửi OTP đổi email tới email MỚI
    public async Task SendChangeEmailOtpAsync(string newEmail, string otp)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = "Xác nhận đổi email tài khoản - IOSRA",
            Body = $"Bạn (hoặc ai đó) vừa yêu cầu đổi email đăng nhập IOSRA sang địa chỉ này.\n" +
                   $"Mã OTP xác nhận: {otp}\n" +
                   $"Mã sẽ hết hạn sau 5 phút.\n\n" +
                   $"Nếu không phải bạn, hãy bỏ qua email này.",
            IsBodyHtml = false
        };
        msg.To.Add(newEmail);
        using var client = CreateClient();
        await client.SendMailAsync(msg);
    }

    // ⬇️ MỚI: thông báo đổi email thành công tới email CŨ (giúp cảnh báo nếu bị đổi trái phép)
    public async Task SendChangeEmailSuccessAsync(string oldEmail, string newEmail)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = "Email tài khoản IOSRA của bạn đã được thay đổi",
            Body = $"Email đăng nhập của bạn đã được đổi thành công sang: {newEmail}\n" +
                   $"Nếu bạn KHÔNG thực hiện thay đổi này, vui lòng liên hệ hỗ trợ ngay lập tức.",
            IsBodyHtml = false
        };
        msg.To.Add(oldEmail);
        using var client = CreateClient();
        await client.SendMailAsync(msg);
    }
}
