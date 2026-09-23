using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleToDoMemo.Core;

/* 저장 형식은 Electron 판과 같다 - 예전 자료를 그대로 읽어 들일 수 있다.
   memos.json : { "2026-09-23": [ { text, state, settledOn?, pinnedAt?, color? } ] } */

public static class ItemState
{
    public const int Open = 0, Done = 1, Dropped = 2;
}

public sealed class TodoItem
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("state")] public int State { get; set; }

    // 처리(완료·드랍)한 날짜. 넘어온 항목은 이 날짜까지만 따라온다.
    [JsonPropertyName("settledOn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SettledOn { get; set; }

    [JsonPropertyName("pinnedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PinnedAt { get; set; }

    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    // 아주 예전 자료는 done 불리언을 썼다 - 읽을 때만 쓰고 저장하지 않는다
    [JsonPropertyName("done")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyDone { get; set; }
}

public sealed class LinkItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}

public sealed class LinkGroup
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("collapsed")] public bool Collapsed { get; set; }
    [JsonPropertyName("links")] public List<LinkItem> Links { get; set; } = new();
}

public sealed class LinksFile
{
    [JsonPropertyName("groups")] public List<LinkGroup> Groups { get; set; } = new();
}

public sealed class Bounds
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

public sealed class NumberLink
{
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("param")] public string Param { get; set; } = "";
}

public sealed class Settings
{
    [JsonPropertyName("lang")] public string? Lang { get; set; }
    [JsonPropertyName("alwaysOnTop")] public bool AlwaysOnTop { get; set; }
    [JsonPropertyName("minimizeToTray")] public bool MinimizeToTray { get; set; } = true;
    [JsonPropertyName("deleteLock")] public bool DeleteLock { get; set; } = true;
    [JsonPropertyName("bounds")] public Bounds? Bounds { get; set; }
    [JsonPropertyName("linksBounds")] public Bounds? LinksBounds { get; set; }
    [JsonPropertyName("link")] public NumberLink Link { get; set; } = new();
    // 등록일 정렬 - false 면 오래된 것이 위
    [JsonPropertyName("newestFirst")] public bool NewestFirst { get; set; }
    [JsonPropertyName("linksTarget")] public string LinksTarget { get; set; } = "inbox";
    [JsonPropertyName("migratedFromElectron")] public bool MigratedFromElectron { get; set; }
}
