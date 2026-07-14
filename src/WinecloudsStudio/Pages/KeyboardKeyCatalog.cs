namespace WinecloudsStudio.Pages;

internal sealed record KeyboardKeyOption(string Name, uint VirtualKey);

internal sealed record KeyboardKeyCategory(string Name, IReadOnlyList<KeyboardKeyOption> Keys);

internal static class KeyboardKeyCatalog
{
    public static IReadOnlyList<KeyboardKeyCategory> Categories { get; } = BuildCategories();

    public static string GetDisplayName(uint virtualKey)
    {
        return Categories
                   .SelectMany(category => category.Keys)
                   .FirstOrDefault(key => key.VirtualKey == virtualKey)?.Name
               ?? $"VK 0x{virtualKey:X2}";
    }

    private static IReadOnlyList<KeyboardKeyCategory> BuildCategories()
    {
        var categories = new List<KeyboardKeyCategory>
        {
            new("字母", Enumerable.Range(0, 26)
                .Select(index => Key(((char)('A' + index)).ToString(), (uint)(0x41 + index)))
                .ToArray()),
            new("数字行", Enumerable.Range(0, 10)
                .Select(index => Key(index.ToString(), (uint)(0x30 + index)))
                .ToArray()),
            new("功能键", Enumerable.Range(0, 24)
                .Select(index => Key($"F{index + 1}", (uint)(0x70 + index)))
                .ToArray()),
            new("小键盘", new[]
            {
                Key("Num 0", 0x60), Key("Num 1", 0x61), Key("Num 2", 0x62),
                Key("Num 3", 0x63), Key("Num 4", 0x64), Key("Num 5", 0x65),
                Key("Num 6", 0x66), Key("Num 7", 0x67), Key("Num 8", 0x68),
                Key("Num 9", 0x69), Key("Num *", 0x6A), Key("Num +", 0x6B),
                Key("Num Separator", 0x6C), Key("Num -", 0x6D), Key("Num .", 0x6E),
                Key("Num /", 0x6F),
            }),
            new("导航与编辑", new[]
            {
                Key("Backspace", 0x08), Key("Tab", 0x09), Key("Enter", 0x0D),
                Key("Pause", 0x13), Key("Caps Lock", 0x14), Key("Esc", 0x1B),
                Key("Space", 0x20), Key("Page Up", 0x21), Key("Page Down", 0x22),
                Key("End", 0x23), Key("Home", 0x24), Key("←", 0x25),
                Key("↑", 0x26), Key("→", 0x27), Key("↓", 0x28),
                Key("Print Screen", 0x2C), Key("Insert", 0x2D), Key("Delete", 0x2E),
                Key("Help", 0x2F),
            }),
            new("修饰、锁定与系统", new[]
            {
                Key("Shift", 0x10), Key("Ctrl", 0x11), Key("Alt", 0x12),
                Key("左 Win", 0x5B), Key("右 Win", 0x5C), Key("菜单键", 0x5D),
                Key("Sleep", 0x5F), Key("Num Lock", 0x90), Key("Scroll Lock", 0x91),
            }),
            new("符号键", new[]
            {
                Key("; :", 0xBA), Key("= +", 0xBB), Key(", <", 0xBC),
                Key("- _", 0xBD), Key(". >", 0xBE), Key("/ ?", 0xBF),
                Key("` ~", 0xC0), Key("[ {", 0xDB), Key("\\ |", 0xDC),
                Key("] }", 0xDD), Key("' \"", 0xDE), Key("OEM 102", 0xE2),
            }),
            new("浏览器与媒体", new[]
            {
                Key("浏览器 后退", 0xA6), Key("浏览器 前进", 0xA7),
                Key("浏览器 刷新", 0xA8), Key("浏览器 停止", 0xA9),
                Key("浏览器 搜索", 0xAA), Key("浏览器 收藏", 0xAB),
                Key("浏览器 主页", 0xAC), Key("静音", 0xAD),
                Key("音量减", 0xAE), Key("音量加", 0xAF),
                Key("下一曲", 0xB0), Key("上一曲", 0xB1),
            }),
        };

        return categories;
    }

    private static KeyboardKeyOption Key(string name, uint virtualKey) => new(name, virtualKey);
}
