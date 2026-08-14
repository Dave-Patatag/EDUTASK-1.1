namespace EDUTASK_1._1.Models;

public sealed class MenuItem
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string TargetPage { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
