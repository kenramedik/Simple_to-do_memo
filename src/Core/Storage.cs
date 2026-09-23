using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SimpleToDoMemo.Core;

// %APPDATA%\SimpleToDoMemoWpf 에 JSON 세 개로 나눠 저장한다.
// Electron 판의 폴더(SimpleToDoMemo)와 따로 두어 두 판을 함께 써도 서로 덮어쓰지 않는다.
public static class Storage
{
    // 테스트할 때는 SIMPLETODOMEMO_APPDATA 로 AppData 자리를 바꿔 실제 자료를 건드리지 않는다
    public static readonly string AppData =
        Environment.GetEnvironmentVariable("SIMPLETODOMEMO_APPDATA") is { Length: > 0 } dir ? dir
        : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static readonly string Dir = Path.Combine(AppData, "SimpleToDoMemoWpf");

    static string MemosFile => Path.Combine(Dir, "memos.json");
    static string LinksFile => Path.Combine(Dir, "links.json");
    static string SettingsFile => Path.Combine(Dir, "settings.json");

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // 한글을 \uXXXX 로 바꾸지 않아 파일을 열어 봐도 읽을 수 있다
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static bool HasData => File.Exists(MemosFile) || File.Exists(LinksFile);

    public static T? Read<T>(string path) where T : class
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null; }
        catch { return null; }
    }

    // 임시 파일에 쓴 뒤 바꿔 끼운다 - 쓰는 도중에 꺼져도 기존 파일이 반쯤 잘려 남지 않는다
    static void Write<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch { }
    }

    public static Dictionary<string, List<TodoItem>> LoadMemos() => Normalize(Read<Dictionary<string, List<TodoItem>>>(MemosFile));
    public static void SaveMemos(Dictionary<string, List<TodoItem>> data) => Write(MemosFile, data);

    public static List<LinkGroup> LoadLinks() => NormalizeLinks(Read<LinksFile>(LinksFile));
    public static void SaveLinks(List<LinkGroup> groups) => Write(LinksFile, new LinksFile { Groups = groups });

    public static Settings LoadSettings() => Read<Settings>(SettingsFile) ?? new Settings();
    public static void SaveSettings(Settings s) => Write(SettingsFile, s);

    public static Dictionary<string, List<TodoItem>> Normalize(Dictionary<string, List<TodoItem>>? raw)
    {
        var data = new Dictionary<string, List<TodoItem>>();
        if (raw == null) return data;
        foreach (var (date, items) in raw)
        {
            if (items == null || items.Count == 0) continue;
            foreach (var it in items)
            {
                if (it.LegacyDone != null) { if (it.State == 0 && it.LegacyDone == true) it.State = ItemState.Done; it.LegacyDone = null; }
                if (it.State is < 0 or > 2) it.State = ItemState.Open;
            }
            data[date] = items;
        }
        return data;
    }

    public const string Inbox = "inbox";

    // 맨 앞 그룹은 늘 '그룹 없음' 칸이다
    public static List<LinkGroup> NormalizeLinks(LinksFile? raw)
    {
        var groups = raw?.Groups ?? new List<LinkGroup>();
        groups.RemoveAll(g => g == null);
        foreach (var g in groups) g.Links ??= new List<LinkItem>();
        if (groups.Count == 0 || groups[0].Id != Inbox)
        {
            var inbox = groups.Find(g => g.Id == Inbox);
            if (inbox != null) groups.Remove(inbox);
            groups.Insert(0, inbox ?? new LinkGroup { Id = Inbox });
        }
        return groups;
    }

    public static string NewId() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x") + Guid.NewGuid().ToString("N")[..5];
}
