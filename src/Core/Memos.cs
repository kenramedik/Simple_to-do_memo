using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SimpleToDoMemo.Core;

public sealed record Entry(TodoItem Item, string From, bool Carried, int Index);

/* 날짜별 메모와 '넘어오는 항목' 규칙. Electron 판 index.html 의 itemsFor 와 같은 규칙이다.
   - 미확인 항목은 등록일 다음 평일부터 매 평일에 계속 나타난다.
   - 처리한 항목은 처리한 날짜까지만 따라온다.
   - 앞으로는 오늘 기준 평일 3일까지만 미리 보여준다. */
public sealed class Memos
{
    public const int LookaheadBizDays = 3;

    public Dictionary<string, List<TodoItem>> Data { get; private set; }

    public Memos(Dictionary<string, List<TodoItem>> data) { Data = data; }

    public void Save() => Storage.SaveMemos(Data);

    public static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static DateTime Parse(string k) => DateTime.ParseExact(k, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Shift(string k, int days) => Key(Parse(k).AddDays(days));
    public static bool IsWeekend(DateTime d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static string AddBizDays(string k, int n)
    {
        var d = Parse(k);
        for (int added = 0; added < n;)
        {
            d = d.AddDays(1);
            if (!IsWeekend(d)) added++;
        }
        return Key(d);
    }

    public List<Entry> ItemsFor(string k, string today, bool newestFirst)
    {
        var own = Data.TryGetValue(k, out var list)
            ? list.Select((it, i) => new Entry(it, k, false, i)).ToList()
            : new List<Entry>();
        List<Entry> Order(List<Entry> arr) { if (newestFirst) arr.Reverse(); return arr; }

        if (IsWeekend(Parse(k))) return Order(own);
        if (string.CompareOrdinal(k, AddBizDays(today, LookaheadBizDays)) > 0) return Order(own);

        var carried = new List<Entry>();
        foreach (var (date, items) in Data.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (string.CompareOrdinal(date, k) >= 0) continue;
            foreach (var it in items)
            {
                bool live = it.State == ItemState.Open
                    || (it.SettledOn != null && string.CompareOrdinal(k, it.SettledOn) <= 0);
                if (live) carried.Add(new Entry(it, date, true, -1));
            }
        }
        carried.AddRange(own);
        return Order(carried);
    }

    // 미확인 -> 완료 -> 드랍 -> 미확인. 어느 날짜에서 처리했는지 남겨야 그날 목록에서 사라지지 않는다.
    public void Cycle(TodoItem it, string cur)
    {
        it.State = (it.State + 1) % 3;
        it.SettledOn = it.State == ItemState.Open ? null : cur;
        Save();
    }

    public void Add(string date, string text)
    {
        if (!Data.TryGetValue(date, out var list)) Data[date] = list = new List<TodoItem>();
        list.Add(new TodoItem { Text = text, State = ItemState.Open });
        Save();
    }

    public void Delete(string date, TodoItem it)
    {
        if (!Data.TryGetValue(date, out var list)) return;
        list.Remove(it);
        if (list.Count == 0) Data.Remove(date);
        Save();
    }

    public void Move(string date, int from, int to)
    {
        if (!Data.TryGetValue(date, out var arr) || from == to || from + 1 == to) return;
        var it = arr[from];
        arr.RemoveAt(from);
        arr.Insert(to > from ? to - 1 : to, it);
        Save();
    }

    public List<(string Date, TodoItem Item)> Search(string q, bool newestFirst)
    {
        var outList = new List<(string, TodoItem)>();
        foreach (var (date, items) in Data.OrderBy(p => p.Key, StringComparer.Ordinal))
            foreach (var it in items)
                if (it.Text.Contains(q, StringComparison.OrdinalIgnoreCase)) outList.Add((date, it));
        if (newestFirst) outList.Reverse();
        return outList;
    }

    public List<(string Date, TodoItem Item)> Pinned() =>
        Data.SelectMany(p => p.Value.Where(it => it.PinnedAt != null).Select(it => (p.Key, it)))
            .OrderByDescending(x => x.it.PinnedAt)
            .Select(x => (x.Key, x.it))
            .ToList();
}
