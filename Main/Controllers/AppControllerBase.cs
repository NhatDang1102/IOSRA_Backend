using System;
using Microsoft.AspNetCore.Mvc;
using Main.Common;

namespace Main.Controllers;

[ApiController]
public abstract class AppControllerBase : ControllerBase
{
    protected Guid AccountId => User.GetAccountId();
}
