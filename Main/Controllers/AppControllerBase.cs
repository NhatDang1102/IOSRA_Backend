using System;
using Microsoft.AspNetCore.Mvc;
using Main.Common;

namespace Main.Controllers;

[ApiController]
public abstract class AppControllerBase : ControllerBase
{
    protected Guid AccountId => User.GetAccountId();

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
