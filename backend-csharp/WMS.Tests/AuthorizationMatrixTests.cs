using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.API.Controllers;
using Xunit;

namespace WMS.Tests;

/// <summary>
/// The authorization matrix, derived from the controllers themselves. Adding an action without
/// [Authorize] (or an anonymous one that is not on the allow-list) fails this test, so the
/// matrix cannot rot the way a hand-checked list does.
/// </summary>
public class AuthorizationMatrixTests
{
    // Anonymous on purpose. Anything else reachable without a token is a bug.
    private static readonly HashSet<string> AnonymousAllowList = new() { "AuthController.Login" };

    // Actions that need a role, not just a valid token.
    private static readonly Dictionary<string, string> RoleRequired = new() { ["AuthController.Register"] = "Admin" };

    private record Action(string Name, bool Anonymous, bool Authorized, string? Roles);

    private static IEnumerable<Action> Actions()
    {
        var controllers = typeof(OrdersController).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var c in controllers)
        foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>(true).Any() == false) continue;

            var anonymous = m.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            var onMethod = m.GetCustomAttributes<AuthorizeAttribute>(true).ToList();
            var onClass = c.GetCustomAttributes<AuthorizeAttribute>(true).ToList();
            var all = onMethod.Concat(onClass).ToList();
            yield return new Action($"{c.Name}.{m.Name}", anonymous, all.Count > 0,
                string.Join(",", all.Select(a => a.Roles).Where(r => !string.IsNullOrEmpty(r))));
        }
    }

    [Fact]
    public void The_matrix_is_not_vacuous()
    {
        var names = Actions().Select(a => a.Name).ToList();
        Assert.True(names.Count >= 30, $"only {names.Count} actions discovered");
        Assert.Contains("OrdersController.ReserveOrder", names);
        Assert.Contains("AuthController.Login", names);
    }

    [Fact]
    public void Every_action_requires_authentication_except_the_allow_list()
    {
        var open = Actions().Where(a => a.Anonymous || !a.Authorized).Select(a => a.Name).OrderBy(n => n).ToList();
        Assert.Equal(AnonymousAllowList.OrderBy(n => n), open);
    }

    [Fact]
    public void Role_restricted_actions_keep_their_role()
    {
        foreach (var (name, role) in RoleRequired)
        {
            var action = Actions().Single(a => a.Name == name);
            Assert.False(action.Anonymous);
            Assert.Contains(role, (action.Roles ?? "").Split(','));
        }
    }

    [Fact]
    public void Only_the_listed_actions_are_role_restricted()
    {
        var restricted = Actions().Where(a => !string.IsNullOrEmpty(a.Roles)).Select(a => a.Name).OrderBy(n => n);
        Assert.Equal(RoleRequired.Keys.OrderBy(n => n), restricted);
    }
}
