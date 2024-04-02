using System.Threading.Tasks;
using AeFinder.Login;
using AeFinder.Login.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AeFinder.Controllers;

[ControllerName("Account")]
[Route("/api/login/account")]
public class LoginAccountController : AeFinderController
{
    private readonly ILoginAccountAppService _loginAccountAppService;

    public LoginAccountController(ILoginAccountAppService loginAccountAppService)
    {
        _loginAccountAppService = loginAccountAppService;
    }

    [HttpPost("register")]
    public virtual Task<RegisterWithNameDto> RegisterAsync(RegisterWithNameInput input)
    {
        return _loginAccountAppService.RegisterAsync(input);
    }

    [HttpPost("request-token/by-password")]
    public virtual Task<string> RequestTokenByPasswordAsync(RequestTokenByPasswordInput input)
    {
        return _loginAccountAppService.RequestTokenByPasswordAsync(input);
    }

    // [HttpPost("refresh-token")]
    // public virtual Task<string> RefreshTokenAsync(RefreshTokenInput input)
    // {
    //     return _loginAccountAppService.RefreshTokenAsync(input);
    // }
}