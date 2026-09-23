using System;

namespace SimpleToDoMemo.Core;

// 화면 문구. 언어를 바꾸면 L.Cur 를 갈아 끼우고 화면을 다시 그린다.
public sealed class Strings
{
    public bool English { get; init; }
    public string[] Dow { get; init; } = Array.Empty<string>();
    public string[] DowShort { get; init; } = Array.Empty<string>();
    public string[] DowLong { get; init; } = Array.Empty<string>();
    public Func<int, int, string> Ym { get; init; } = (_, _) => "";
    public Func<int, string> DayNum { get; init; } = _ => "";
    public Func<int, int, string> Md { get; init; } = (_, _) => "";
    public Func<int, int, int, string, string> Full { get; init; } = (_, _, _, _) => "";

    public string AppTitle { get; init; } = "";
    public string MenuFile { get; init; } = "";
    public string MenuSettings { get; init; } = "";
    public string MenuHelp { get; init; } = "";
    public string OpenLinks { get; init; } = "";
    public string Exit { get; init; } = "";
    public string AlwaysOnTop { get; init; } = "";
    public string MinToTray { get; init; } = "";
    public string DeleteLock { get; init; } = "";
    public string About { get; init; } = "";
    public string ImportElectron { get; init; } = "";
    public string Open { get; init; } = "";
    public string CreatedBy { get; init; } = "";
    public string AboutBody { get; init; } = "";

    public string LinkSettings { get; init; } = "";
    public string LinkTitle { get; init; } = "";
    public string LinkSub { get; init; } = "";
    public string LinkUrl { get; init; } = "";
    public string LinkParam { get; init; } = "";
    public string LinkOptional { get; init; } = "";
    public string LinkPreview { get; init; } = "";
    public string LinkOff { get; init; } = "";
    public string LinkBadUrl { get; init; } = "";
    public string LinkBadParam { get; init; } = "";
    public string Cancel { get; init; } = "";
    public string Save { get; init; } = "";

    public string OpenCalendar { get; init; } = "";
    public string CloseCalendar { get; init; } = "";
    public string PrevDay { get; init; } = "";
    public string NextDay { get; init; } = "";
    public string PrevMonth { get; init; } = "";
    public string NextMonth { get; init; } = "";
    public string Today { get; init; } = "";
    public string SearchTip { get; init; } = "";
    public string LinksTip { get; init; } = "";
    public string SortAsc { get; init; } = "";
    public string SortDesc { get; init; } = "";
    public string AddPlaceholder { get; init; } = "";
    public string AddTip { get; init; } = "";
    public string EmptyTitle { get; init; } = "";
    public string EmptySub { get; init; } = "";
    public string DragTip { get; init; } = "";
    public string Edit { get; init; } = "";
    public string Del { get; init; } = "";
    public string SetColor { get; init; } = "";
    public string NoColor { get; init; } = "";
    public Func<string, string> ColorIs { get; init; } = s => s;
    public string Pin { get; init; } = "";
    public string Unpin { get; init; } = "";
    public Func<int, string> Pinned { get; init; } = _ => "";
    public string Prev { get; init; } = "";
    public string Next { get; init; } = "";
    public Func<string, string> AddedOn { get; init; } = s => s;
    public Func<string, string> GoTo { get; init; } = s => s;
    public Func<int, int, string> CountTip { get; init; } = (_, _) => "";
    public Func<string, string> CarriedFrom { get; init; } = s => s;
    public string SearchPlaceholder { get; init; } = "";
    public string Close { get; init; } = "";
    public Func<int, string> Hits { get; init; } = _ => "";
    public string SearchTitle { get; init; } = "";
    public string SearchSub { get; init; } = "";
    public string NoHitTitle { get; init; } = "";
    public string NoHitSub { get; init; } = "";
    public string ClickToOpen { get; init; } = "";
    public Func<string, string> ColorName { get; init; } = s => s;

    // 링크 모음 창
    public string LinksTitle { get; init; } = "";
    public Func<int, string> LinksTotal { get; init; } = _ => "";
    public string NewGroup { get; init; } = "";
    public string NewGroupName { get; init; } = "";
    public string Ungrouped { get; init; } = "";
    public string LinksSearch { get; init; } = "";
    public string Clear { get; init; } = "";
    public string NamePh { get; init; } = "";
    public string UrlPh { get; init; } = "";
    public string LinkAddTip { get; init; } = "";
    public string AddTo { get; init; } = "";
    public string BadUrl { get; init; } = "";
    public string LinksEmptyTitle { get; init; } = "";
    public string LinksEmptySub { get; init; } = "";
    public string DropHere { get; init; } = "";
    public string OpenInBrowser { get; init; } = "";
    public string EditLink { get; init; } = "";
    public string CopyUrl { get; init; } = "";
    public string Rename { get; init; } = "";
    public string MoveUp { get; init; } = "";
    public string MoveDown { get; init; } = "";
    public string DelGroup { get; init; } = "";
    public string DelGroupTip { get; init; } = "";
    public string MoveTo { get; init; } = "";
    public string Fold { get; init; } = "";
    public string Unfold { get; init; } = "";
    public string DragMoveTip { get; init; } = "";
    public string EditLinkTitle { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";

    public string ImportDone { get; init; } = "";
    public string ImportNone { get; init; } = "";
    public string ImportAsk { get; init; } = "";

    static readonly string[] Months = { "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December" };

    public static readonly Strings Ko = new()
    {
        English = false,
        Dow = new[] { "일", "월", "화", "수", "목", "금", "토" },
        DowShort = new[] { "일", "월", "화", "수", "목", "금", "토" },
        DowLong = new[] { "일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일" },
        Ym = (y, m) => $"{y}년 {m}월",
        DayNum = d => $"{d}일",
        Md = (m, d) => $"{m}월 {d}일",
        Full = (y, m, d, w) => $"{y}년 {m}월 {d}일 ({w})",
        AppTitle = "간단한 할일 메모",
        MenuFile = "파일(_F)", MenuSettings = "설정(_S)", MenuHelp = "도움말(_H)",
        OpenLinks = "링크 모음(_L)", Exit = "종료(_X)", AlwaysOnTop = "항상 위에 표시(_T)",
        MinToTray = "최소화 시 트레이로(_M)", DeleteLock = "삭제 잠금(_D)", About = "정보(_A)",
        ImportElectron = "v2.x 이하 버전 데이터 가져오기(_I)…",
        Open = "열기", CreatedBy = "제작자", AboutBody = "하루 단위로 메모를 남기고 완료 표시를 할 수 있습니다.",
        LinkSettings = "링크 설정(_K)…", LinkTitle = "링크 설정",
        LinkSub = "할 일에 (12345) 처럼 괄호 안에 다섯 자리 이상 숫자가 있으면 그 숫자에 이 주소로 링크를 답니다.",
        LinkUrl = "주소", LinkParam = "파라미터 이름", LinkOptional = "(선택)",
        LinkPreview = "예시", LinkOff = "주소를 비워 두면 링크를 달지 않습니다.",
        LinkBadUrl = "주소는 http:// 또는 https:// 로 시작해야 하고 빈칸이 없어야 합니다.",
        LinkBadParam = "파라미터 이름에는 영문, 숫자, _ . ~ - 만 쓸 수 있습니다.",
        Cancel = "취소", Save = "저장",
        OpenCalendar = "달력 열기", CloseCalendar = "달력 접기 (Esc)",
        PrevDay = "이전 날 (←)", NextDay = "다음 날 (→)", PrevMonth = "이전 달", NextMonth = "다음 달",
        Today = "오늘", SearchTip = "검색 (Ctrl+F)", LinksTip = "링크 모음 (Ctrl+L)",
        SortAsc = "등록순 · 오래된 것부터", SortDesc = "등록순 · 최근 것부터",
        AddPlaceholder = "할 일을 입력하세요", AddTip = "추가 (Enter)",
        EmptyTitle = "이 날은 비어 있습니다", EmptySub = "아래에 입력해 할 일을 추가하세요",
        DragTip = "드래그해서 순서 변경", Edit = "수정", Del = "삭제",
        SetColor = "색상 지정", ColorIs = c => $"색상: {c}", NoColor = "색상 없음",
        Pin = "상단에 고정", Unpin = "고정 해제", Pinned = n => $"고정됨 {n}", Prev = "이전", Next = "다음",
        AddedOn = s => $"{s}에 추가됨", GoTo = s => $"{s}로 이동",
        CountTip = (n, done) => $"메모 {n}개 · 처리 {done}개",
        CarriedFrom = s => $"{s}에서 넘어옴 · 눌러서 그 날짜로 이동",
        SearchPlaceholder = "전체 메모에서 검색", Close = "닫기 (Esc)", Hits = n => $"{n}건",
        SearchTitle = "전체 메모에서 검색", SearchSub = "모든 날짜의 항목을 찾습니다",
        NoHitTitle = "검색 결과가 없습니다", NoHitSub = "다른 낱말로 찾아보세요", ClickToOpen = "눌러서 이동",
        ColorName = c => c switch
        {
            "red" => "빨강", "orange" => "주황", "amber" => "노랑", "green" => "초록",
            "teal" => "청록", "blue" => "파랑", "purple" => "보라", "gray" => "회색", _ => c,
        },
        LinksTitle = "링크 모음", LinksTotal = n => n > 0 ? $"{n}개" : "",
        NewGroup = "새 그룹", NewGroupName = "새 그룹", Ungrouped = "그룹 없음",
        LinksSearch = "이름이나 주소로 검색", Clear = "지우기 (Esc)",
        NamePh = "이름", UrlPh = "주소", LinkAddTip = "추가 (Enter)", AddTo = "추가할 그룹",
        BadUrl = "주소가 올바르지 않습니다. 예: example.com",
        LinksEmptyTitle = "아직 모은 링크가 없습니다", LinksEmptySub = "아래에 이름과 주소를 넣어 추가하세요",
        DropHere = "링크를 여기로 끌어 놓으세요",
        OpenInBrowser = "브라우저에서 열기", EditLink = "수정…", CopyUrl = "주소 복사",
        Rename = "이름 바꾸기", MoveUp = "위로 옮기기", MoveDown = "아래로 옮기기",
        DelGroup = "그룹 삭제", DelGroupTip = "그룹 삭제 · 링크는 '그룹 없음'으로 옮겨집니다", MoveTo = "다른 그룹으로 옮기기",
        Fold = "접기", Unfold = "펼치기", DragMoveTip = "드래그해서 옮기기",
        EditLinkTitle = "링크 수정", Name = "이름", Url = "주소",
        ImportAsk = "v2.x 이하 버전의 메모와 링크를 찾았습니다.\n지금 가져올까요? 이 버전에 있는 메모와 링크는 가져온 내용으로 바뀝니다.",
        ImportDone = "v2.x 이하 버전의 메모와 링크를 가져왔습니다.",
        ImportNone = "v2.x 이하 버전의 메모와 링크를 찾지 못했습니다.\n이 PC에서 v2.x 이하 버전을 쓴 적이 없거나, 자료 폴더(%APPDATA%\\SimpleToDoMemo)가 지워진 것 같습니다.",
    };

    public static readonly Strings En = new()
    {
        English = true,
        Dow = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" },
        DowShort = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" },
        DowLong = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" },
        Ym = (y, m) => $"{Months[m - 1]} {y}",
        DayNum = d => $"{d}",
        Md = (m, d) => $"{Months[m - 1][..3]} {d}",
        Full = (y, m, d, w) => $"{w}, {Months[m - 1]} {d}, {y}",
        AppTitle = "Simple To-Do Memo",
        MenuFile = "_File", MenuSettings = "_Settings", MenuHelp = "_Help",
        OpenLinks = "_Links", Exit = "E_xit", AlwaysOnTop = "Always on _Top",
        MinToTray = "_Minimize to Tray", DeleteLock = "_Delete Lock", About = "_About",
        ImportElectron = "_Import Data from v2.x or Earlier…",
        Open = "Open", CreatedBy = "Created by", AboutBody = "Keep a to-do list one day at a time.",
        LinkSettings = "Lin_k Settings…", LinkTitle = "Link Settings",
        LinkSub = "When a task has a number of five or more digits in parentheses, like (12345), that number becomes a link to this address.",
        LinkUrl = "URL", LinkParam = "Parameter name", LinkOptional = "(optional)",
        LinkPreview = "Example", LinkOff = "Leave the URL empty to turn links off.",
        LinkBadUrl = "The URL must start with http:// or https:// and contain no spaces.",
        LinkBadParam = "The parameter name may only use letters, digits and _ . ~ -",
        Cancel = "Cancel", Save = "Save",
        OpenCalendar = "Open calendar", CloseCalendar = "Close calendar (Esc)",
        PrevDay = "Previous day (←)", NextDay = "Next day (→)", PrevMonth = "Previous month", NextMonth = "Next month",
        Today = "Today", SearchTip = "Search (Ctrl+F)", LinksTip = "Links (Ctrl+L)",
        SortAsc = "By date added · oldest first", SortDesc = "By date added · newest first",
        AddPlaceholder = "Add a task", AddTip = "Add (Enter)",
        EmptyTitle = "Nothing here yet", EmptySub = "Add a task below",
        DragTip = "Drag to reorder", Edit = "Edit", Del = "Delete",
        SetColor = "Set color", ColorIs = c => $"Color: {c}", NoColor = "No color",
        Pin = "Pin to top", Unpin = "Unpin", Pinned = n => $"Pinned {n}", Prev = "Previous", Next = "Next",
        AddedOn = s => $"Added on {s}", GoTo = s => $"Go to {s}",
        CountTip = (n, done) => $"{n} item{(n > 1 ? "s" : "")} · {done} settled",
        CarriedFrom = s => $"Carried from {s} · click to open that date",
        SearchPlaceholder = "Search all notes", Close = "Close (Esc)", Hits = n => $"{n} result{(n == 1 ? "" : "s")}",
        SearchTitle = "Search all notes", SearchSub = "Looks through every date",
        NoHitTitle = "No results", NoHitSub = "Try a different word", ClickToOpen = "click to open",
        ColorName = c => c switch
        {
            "red" => "Red", "orange" => "Orange", "amber" => "Yellow", "green" => "Green",
            "teal" => "Teal", "blue" => "Blue", "purple" => "Purple", "gray" => "Gray", _ => c,
        },
        LinksTitle = "Links", LinksTotal = n => n > 0 ? $"{n}" : "",
        NewGroup = "New group", NewGroupName = "New group", Ungrouped = "Ungrouped",
        LinksSearch = "Search by name or URL", Clear = "Clear (Esc)",
        NamePh = "Name", UrlPh = "URL", LinkAddTip = "Add (Enter)", AddTo = "Add to",
        BadUrl = "That URL does not look right. e.g. example.com",
        LinksEmptyTitle = "No links yet", LinksEmptySub = "Enter a name and URL below to add one",
        DropHere = "Drag links here",
        OpenInBrowser = "Open in browser", EditLink = "Edit…", CopyUrl = "Copy URL",
        Rename = "Rename", MoveUp = "Move up", MoveDown = "Move down",
        DelGroup = "Delete group", DelGroupTip = "Delete group · its links move to Ungrouped", MoveTo = "Move to group",
        Fold = "Collapse", Unfold = "Expand", DragMoveTip = "Drag to move",
        EditLinkTitle = "Edit link", Name = "Name", Url = "URL",
        ImportAsk = "Found notes and links from version 2.x or earlier.\nImport them now? Notes and links in this version will be replaced.",
        ImportDone = "Imported notes and links from version 2.x or earlier.",
        ImportNone = "No notes or links from version 2.x or earlier were found.\nVersion 2.x or earlier may never have been used on this PC, or its data folder (%APPDATA%\\SimpleToDoMemo) was removed.",
    };
}

public static class L
{
    public static Strings Cur { get; private set; } = Strings.Ko;
    public static void Set(string? lang) => Cur = lang == "en" ? Strings.En : Strings.Ko;
    // 메뉴의 _ (단축 글자 표시)를 뺀 문구 - 툴팁·창 제목에 쓴다
    public static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s.Replace("_", ""), @"\([A-Z]\)", "");
}
