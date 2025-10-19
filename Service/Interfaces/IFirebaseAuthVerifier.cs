using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interfaces;

public class FirebaseUserInfo
{
    public string Uid { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

public interface IFirebaseAuthVerifier
{
    Task<FirebaseUserInfo> VerifyIdTokenAsync(string idToken, CancellationToken ct = default);
}
