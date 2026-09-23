using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SimpleToDoMemo.Core;

public static class LinkUtil
{
    // 괄호 안에 다섯 자리 이상 숫자만 있으면 그 숫자에 링크를 단다
    public static readonly Regex NumberId = new(@"\((\d{5,})\)", RegexOptions.Compiled);
    public static readonly Regex UrlOk = new(@"^https?://\S+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static readonly Regex ParamOk = new(@"^[\w.~-]+$", RegexOptions.Compiled);

    // 파라미터 이름이 없으면 주소 끝에 숫자를 그대로 붙인다 (…/pages/12345 같은 경로형 주소)
    public static string Build(string url, string param, string id)
    {
        if (string.IsNullOrEmpty(param)) return url + id;
        var sep = !url.Contains('?') ? "?" : Regex.IsMatch(url, @"[?&]$") ? "" : "&";
        return $"{url}{sep}{param}={id}";
    }

    // 'example.com' 처럼 스킴 없이 넣어도 https 로 받아 준다. 브라우저로 넘길 수 있는 http(s) 만 허용한다.
    public static string? Normalize(string s)
    {
        s = s.Trim();
        if (s.Length == 0 || Regex.IsMatch(s, @"\s")) return null;
        if (!Regex.IsMatch(s, @"^[a-z][\w+.-]*://", RegexOptions.IgnoreCase)) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return null;
        if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) return null;
        if (!u.Host.Contains('.') && u.Host != "localhost") return null;
        return u.AbsoluteUri;
    }

    // 목록에 보일 짧은 주소 - 스킴, www., 끝의 / 를 뗀다
    public static string Short(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var u)) return href;
        var host = u.IsDefaultPort ? u.Host : $"{u.Host}:{u.Port}";
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host[4..];
        var rest = (u.AbsolutePath + u.Query + u.Fragment).TrimEnd('/');
        return host + Uri.UnescapeDataString(rest);
    }

    public static string DefaultName(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var u)) return href;
        return u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host[4..] : u.Host;
    }

    public static void OpenInBrowser(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
