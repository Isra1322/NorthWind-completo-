using Microsoft.AspNetCore.Components;

namespace NorthWind.RazorComponents.NavBars;

public partial class NavMenu
{
    [Parameter] public RenderFragment? NavBarBrand { get; set; }
    [Parameter] public RenderFragment? NavBarItems { get; set; }
    public bool CollapseNavMenu { get; set; } = true;
    public string? NavMenuCssClass => CollapseNavMenu ? "collapse" : null;
    public void ToggleNavMenu() => CollapseNavMenu = !CollapseNavMenu;
}