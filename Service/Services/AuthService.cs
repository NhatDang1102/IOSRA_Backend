using BCrypt.Net;
using Contract.DTOs.Request;
using Contract.DTOs.Respond;
using Repository.Entities;
using Repository.Interfaces;
using Service.Helpers;
using Service.Interfaces;
using System.Security.Cryptography;

namespace Service.Implementations;

public class AuthService : IAuthService
{
    private readonly IAccountRepository _accounts;
    private readonly IReaderRepository _readers;
    private readonly IRoleRepository _roles;
    private readonly IAccountRoleRepository _accountRoles;
    private readonly IJwtTokenFactory _jwt;
    private readonly IOtpStore _otpStore;
    private readonly IMailSender _mail;
    private readonly IFirebaseAuthVerifier _fb;

    public AuthService(
        IAccountRepository accounts,
        IReaderRepository readers,
        IRoleRepository roles,
        IAccountRoleRepository accountRoles,
        IJwtTokenFactory jwt,
        IOtpStore otpStore,
        IMailSender mail,
        IFirebaseAuthVerifier fb)
    {
        _accounts = accounts;
        _readers = readers;
        _roles = roles;
        _accountRoles = accountRoles;
        _jwt = jwt;
        _otpStore = otpStore;
        _mail = mail;
        _fb = fb;
    }

    public async Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            throw new ArgumentException("Thiếu thông tin.");

        if (await _accounts.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
            throw new InvalidOperationException("Email hoặc username đã tồn tại.");

        if (!await _otpStore.CanSendAsync(req.Email))
            throw new InvalidOperationException("Bạn đã vượt quá số lần gửi OTP trong 1 giờ.");

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); // 6 số
        var bcrypt = BCrypt.Net.BCrypt.HashPassword(req.Password);

        await _otpStore.SaveAsync(req.Email, otp, bcrypt, req.Username);
        await _mail.SendOtpEmailAsync(req.Email, otp);
    }

    public async Task<string> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default)
    {
        var entry = await _otpStore.GetAsync(req.Email);
        if (entry == null) throw new InvalidOperationException("OTP hết hạn hoặc không tồn tại.");

        var (otpStored, pwdHashStored, usernameStored) = entry.Value;
        if (otpStored != req.Otp) throw new InvalidOperationException("OTP không đúng.");

        var acc = new account
        {
            username = usernameStored,
            email = req.Email,
            password_hash = pwdHashStored,
            status = "unbanned",
            strike = 0
        };
        await _accounts.AddAsync(acc, ct);
        await _readers.AddAsync(new reader { account_id = acc.account_id }, ct);

        var readerRoleId = await _roles.GetRoleIdByCodeAsync("reader", ct);
        await _accountRoles.AddAsync(acc.account_id, readerRoleId, ct);

        await _otpStore.DeleteAsync(req.Email);
        _ = _mail.SendWelcomeEmailAsync(req.Email, usernameStored);

        return _jwt.CreateToken(acc, new[] { "reader" });
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var acc = await _accounts.FindByIdentifierAsync(req.Identifier, ct);
        if (acc == null) throw new UnauthorizedAccessException("Tài khoản không tồn tại.");

        if (acc.status == "banned")
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");

        var ok = BCrypt.Net.BCrypt.Verify(req.Password, acc.password_hash);
        if (!ok) throw new UnauthorizedAccessException("Sai mật khẩu.");

        var roles = await _roles.GetRoleCodesOfAccountAsync(acc.account_id, ct);
        var token = _jwt.CreateToken(acc, roles);

        return new LoginResponse
        {
            Username = acc.username,
            Email = acc.email,
            Token = token
        };
    }
    public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default)
    {
        FirebaseUserInfo user;
        try
        {
            user = await _fb.VerifyIdTokenAsync(req.IdToken, ct);
        }
        catch
        {
            throw new UnauthorizedAccessException("Token Google không hợp lệ hoặc hết hạn.");
        }

        var acc = await _accounts.FindByIdentifierAsync(user.Email, ct);
        if (acc == null)
        {
            throw new InvalidOperationException("AccountNotRegistered");
        }

        if (acc.status == "banned")
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");

        var roles = await _roles.GetRoleCodesOfAccountAsync(acc.account_id, ct);
        var token = _jwt.CreateToken(acc, roles);

        return new LoginResponse
        {
            Username = acc.username,
            Email = acc.email,
            Token = token
        };
    }

    public async Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default)
    {
        FirebaseUserInfo user;
        try
        {
            user = await _fb.VerifyIdTokenAsync(req.IdToken, ct);
        }
        catch
        {
            throw new UnauthorizedAccessException("Token Google không hợp lệ hoặc hết hạn.");
        }

        if (await _accounts.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
            throw new InvalidOperationException("Email hoặc username đã tồn tại.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                req.Password, @"^(?=.*[A-Za-z])(?=.*\d).{6,20}$"))
            throw new ArgumentException("Mật khẩu phải có ít nhất 1 chữ và 1 số, dài 6–20 ký tự.");

        var pwdHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        var acc = new account
        {
            username = req.Username,
            email = user.Email,
            password_hash = pwdHash,
            status = "unbanned",
            strike = 0,
            avatar_url = user.Picture
        };
        await _accounts.AddAsync(acc, ct);
        await _readers.AddAsync(new reader { account_id = acc.account_id }, ct);

        var readerRoleId = await _roles.GetRoleIdByCodeAsync("reader", ct);
        await _accountRoles.AddAsync(acc.account_id, readerRoleId, ct);

        _ = _mail.SendWelcomeEmailAsync(user.Email, req.Username);

        var token = _jwt.CreateToken(acc, new[] { "reader" });
        return new LoginResponse { Username = acc.username, Email = acc.email, Token = token };
    }
}
