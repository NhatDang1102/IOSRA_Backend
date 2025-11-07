    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Service.Interfaces;

namespace Service.Helpers;

// Service xác thực Google ID Token thông qua Firebase Admin SDK
public class FirebaseAuthVerifier : IFirebaseAuthVerifier
{
    private readonly FirebaseApp _app;
    public FirebaseAuthVerifier(FirebaseApp app) => _app = app;

    // Xác thực Google ID Token và trích xuất thông tin user
    public async Task<FirebaseUserInfo> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        // Lấy Firebase Auth instance
        var auth = FirebaseAuth.GetAuth(_app);
        // Verify token với Firebase - throw exception nếu token không hợp lệ
        var decoded = await auth.VerifyIdTokenAsync(idToken, ct);

        // Trích xuất các claims từ token
        decoded.Claims.TryGetValue("email", out var emailObj);
        decoded.Claims.TryGetValue("name", out var nameObj);
        decoded.Claims.TryGetValue("picture", out var pictureObj);

        // Email là bắt buộc
        var email = emailObj?.ToString();
        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("Firebase token không có email hợp lệ.");

        return new FirebaseUserInfo
        {
            Uid = decoded.Uid,
            Email = email,
            Name = nameObj?.ToString(),
            Picture = pictureObj?.ToString()
        };
    }
}

