namespace SuperDock;

internal sealed record AppPlacement(bool OnDock,string[] Groups)
{
    public string Label=>OnDock?"✓ 已添加到 Dock":"▦ 已添加到分组";
    public string Description=>(OnDock?"已添加到 Dock":"")+(Groups.Length>0?(OnDock?"\n":"")+"已添加到分组："+string.Join("、",Groups):"");
}

internal static class ApplicationPlacement
{
    // Compare resolved executable paths, so an installed shortcut and its running
    // executable share the same placement state without reopening the shortcuts.
    public static Dictionary<string,AppPlacement> Read(MainWindow.Settings settings)
    {
        var result=new Dictionary<string,AppPlacement>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in settings.Entries)result[RunningWindows.WindowTarget(entry.Target)]=new(true,[]);
        foreach(var group in settings.Groups)
            foreach(var target in group.Entries.Select(e=>RunningWindows.WindowTarget(e.Target)).Distinct(StringComparer.OrdinalIgnoreCase)){
                result.TryGetValue(target,out var existing);
                result[target]=new(existing?.OnDock==true,(existing?.Groups??[]).Append(group.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }
        return result;
    }
}
