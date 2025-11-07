using System;
using Microsoft.AspNetCore.Mvc;
using Main.Common;

namespace Main.Controllers;

// Base controller cho tất cả API controllers, cung cấp tiện ích lấy AccountId từ JWT
[ApiController]
public abstract class AppControllerBase : ControllerBase
{
    // Property lấy AccountId từ JWT claims - throw exception nếu không tìm thấy
    protected Guid AccountId => User.GetAccountId();

    // Method lấy AccountId an toàn - trả về null nếu user chưa đăng nhập hoặc token không hợp lệ
    protected Guid? TryGetAccountId()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        try
        {
            return User.GetAccountId();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
