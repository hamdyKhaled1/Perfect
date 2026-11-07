using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HagImamAccounts.Services;

public sealed class ReadOnlyAccessFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var isLogout = context.RouteData.Values.TryGetValue("controller", out var controller) &&
            string.Equals(controller?.ToString(), "Access", StringComparison.OrdinalIgnoreCase) &&
            context.RouteData.Values.TryGetValue("action", out var action) &&
            string.Equals(action?.ToString(), "Logout", StringComparison.OrdinalIgnoreCase);

        var allowsAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        var isReadOnlyRequest = HttpMethods.IsPost(context.HttpContext.Request.Method) ||
            HttpMethods.IsPut(context.HttpContext.Request.Method) ||
            HttpMethods.IsPatch(context.HttpContext.Request.Method) ||
            HttpMethods.IsDelete(context.HttpContext.Request.Method);

        if (isReadOnlyRequest &&
            !isLogout &&
            !allowsAnonymous &&
            !context.HttpContext.User.IsInRole(PortalRoles.SuperAdmin))
        {
            context.Result = new ForbidResult();
        }

        return Task.CompletedTask;
    }
}
