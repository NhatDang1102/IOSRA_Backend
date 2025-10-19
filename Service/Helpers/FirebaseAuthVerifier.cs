    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Service.Interfaces;

namespace Service.Helpers;

public class FirebaseAuthVerifier : IFirebaseAuthVerifier
{
    private readonly FirebaseApp _app;
    public FirebaseAuthVerifier(FirebaseApp app) => _app = app;

    public async Task<FirebaseUserInfo> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        var auth = FirebaseAuth.GetAuth(_app);
        var decoded = await auth.VerifyIdTokenAsync(idToken, ct);

        decoded.Claims.TryGetValue("email", out var emailObj);
        decoded.Claims.TryGetValue("name", out var nameObj);
        decoded.Claims.TryGetValue("picture", out var pictureObj);

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

