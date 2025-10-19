namespace Service.Interfaces;

public interface IOtpStore
{
    Task SaveAsync(string email, string otp, string passwordBcrypt, string username);
    Task<(string Otp, string PasswordBcrypt, string Username)?> GetAsync(string email);
    Task<bool> DeleteAsync(string email);
    Task<bool> CanSendAsync(string email); 
}
