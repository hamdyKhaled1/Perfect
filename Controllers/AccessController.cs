using System.Security.Claims;
using HagImamAccounts.Services;
using HagImamAccounts.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HagImamAccounts.Controllers;

public class AccessController : Controller
{
    private readonly PortalAccountAuthenticator _authenticator;

    public AccessController(PortalAccountAuthenticator authenticator)
    {
        _authenticator = authenticator;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Clients");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var account = _authenticator.Authenticate(model.UserName, model.Password);
        if (account == null)
        {
            ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة.");
            model.Password = string.Empty;
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, account.UserName),
            new Claim(ClaimTypes.Role, account.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
                AllowRefresh = true
            });

        return Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl!)
            : RedirectToAction("Index", "Clients");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Denied() => View();
}
