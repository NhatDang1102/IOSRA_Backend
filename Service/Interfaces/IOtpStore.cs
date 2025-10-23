namespace Service.Helpers
{
    public interface IOtpStore
    {
        // Đăng ký
        Task SaveAsync(string email, string otp, string passwordBcrypt, string username);
        Task<(string Otp, string PasswordBcrypt, string Username)?> GetAsync(string email);
        Task<bool> DeleteAsync(string email);

        // Quên mật khẩu
        Task SaveForgotAsync(string email, string otp, string newPasswordBcrypt);
        Task<(string Otp, string NewPasswordBcrypt)?> GetForgotAsync(string email);
        Task<bool> DeleteForgotAsync(string email);

        // Đổi email
        Task SaveEmailChangeAsync(ulong accountId, string newEmail, string otp);
        Task<(string NewEmail, string Otp)?> GetEmailChangeAsync(ulong accountId);
        Task<bool> DeleteEmailChangeAsync(ulong accountId);

        // Giới hạn gửi
        Task<bool> CanSendAsync(string email);
    }
}
