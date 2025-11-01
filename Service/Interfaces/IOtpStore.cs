using System;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public interface IOtpStore
    {
        Task SaveAsync(string email, string otp, string passwordBcrypt, string username);
        Task<(string Otp, string PasswordBcrypt, string Username)?> GetAsync(string email);
        Task<bool> DeleteAsync(string email);

        Task SaveForgotAsync(string email, string otp, string newPasswordBcrypt);
        Task<(string Otp, string NewPasswordBcrypt)?> GetForgotAsync(string email);
        Task<bool> DeleteForgotAsync(string email);

        Task SaveEmailChangeAsync(Guid accountId, string newEmail, string otp);
        Task<(string NewEmail, string Otp)?> GetEmailChangeAsync(Guid accountId);
        Task<bool> DeleteEmailChangeAsync(Guid accountId);

        Task<bool> CanSendAsync(string email);
    }
}
