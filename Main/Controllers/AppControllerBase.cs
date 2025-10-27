using Microsoft.AspNetCore.Mvc;
using Main.Common;

namespace Main.Controllers;

[ApiController]
public abstract class AppControllerBase : ControllerBase
{
    protected ulong AccountId => User.GetAccountId();
}
