using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SimpleToDoMemo.Core;

/* Electron 판(1.x~2.x)의 브라우저 저장소(localStorage)를 직접 읽는다.
   Chromium 은 localStorage 를 LevelDB 로 저장한다 - %APPDATA%\SimpleToDoMemo\Local Storage\leveldb
   - *.log : 최근 기록 (쓰기 로그). 32KB 블록 안에 WriteBatch 가 이어진다
   - *.ldb : 오래된 기록을 모은 테이블. 블록마다 Snappy 로 압축돼 있을 수 있다
   같은 키가 여러 번 나오면 순번(sequence)이 가장 큰 것이 최신이다. 지운 키는 삭제 표시로 남는다.
   Electron 앱이 켜져 있어도 읽을 수 있게 파일을 공유 모드로 연다. 쓰지는 않는다. */
public static class ElectronStore
{
    // 키: "_" + 출처 + \0 + (1=Latin-1 | 0=UTF-16) + 이름. 앱은 file:// 에서 돌았다.
    const string Origin = "_file://";

    public sealed class Result
    {
        public string? Memos, Links, Sort;
        public string Source = "";
    }

    // SimpleToDoMemo(2.2.0~) 를 먼저, 없으면 이름이 바뀌기 전의 DateMemo 를 본다
    public static Result? Read(string appData)
    {
        foreach (var name in new[] { "SimpleToDoMemo", "DateMemo" })
        {
            var dir = Path.Combine(appData, name, "Local Storage", "leveldb");
            if (!Directory.Exists(dir)) continue;
            try
            {
                var kv = ReadDb(dir);
                string? Get(string key) =>
                    kv.TryGetValue(Origin + "\0\u0001" + key, out var v) && v != null ? DecodeValue(v) : null;
                var r = new Result { Memos = Get("memo-by-date-v1"), Links = Get("links-v1"), Sort = Get("memo-sort-v1"), Source = dir };
                if (r.Memos != null || r.Links != null) return r;
            }
            catch { }
        }
        return null;
    }

    // 값의 첫 바이트가 인코딩: 0 = UTF-16LE, 1 = Latin-1
    static string DecodeValue(byte[] v)
    {
        if (v.Length == 0) return "";
        return v[0] == 0 ? Encoding.Unicode.GetString(v, 1, v.Length - 1) : Encoding.Latin1.GetString(v, 1, v.Length - 1);
    }

    /* ─── LevelDB ─── 키는 Latin-1 로 풀어 문자열로 다룬다 (바이트가 그대로 한 글자씩 대응한다) */
    public static Dictionary<string, byte[]?> ReadDb(string dir)
    {
        var best = new Dictionary<string, (ulong Seq, byte[]? Val)>();
        void Put(byte[] key, ulong seq, byte[]? val)
        {
            var k = Encoding.Latin1.GetString(key);
            if (!best.TryGetValue(k, out var cur) || seq >= cur.Seq) best[k] = (seq, val);
        }

        foreach (var f in Directory.GetFiles(dir).Where(f => f.EndsWith(".ldb") || f.EndsWith(".sst")))
            ReadTable(ReadShared(f), Put);
        foreach (var f in Directory.GetFiles(dir, "*.log").Where(f => Path.GetFileName(f) != "LOG"))
            ReadLog(ReadShared(f), Put);

        return best.ToDictionary(p => p.Key, p => p.Value.Val);
    }

    static byte[] ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buf = new byte[fs.Length];
        int read = 0;
        while (read < buf.Length) { int n = fs.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
        return read == buf.Length ? buf : buf[..read];
    }

    static ulong Varint(byte[] b, ref int p)
    {
        ulong v = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            byte x = b[p++];
            v |= (ulong)(x & 0x7F) << shift;
            if ((x & 0x80) == 0) return v;
        }
        throw new InvalidDataException("varint");
    }

    // 쓰기 로그: 32KB 블록, 레코드 머리 7바이트(체크섬 4, 길이 2, 종류 1). 종류 1=통째, 2=처음, 3=가운데, 4=끝
    static void ReadLog(byte[] data, Action<byte[], ulong, byte[]?> put)
    {
        const int Block = 32768;
        var rec = new List<byte>();
        int p = 0;
        while (p + 7 <= data.Length)
        {
            int left = Block - p % Block;
            if (left < 7) { p += left; continue; }
            int len = data[p + 4] | data[p + 5] << 8;
            byte type = data[p + 6];
            p += 7;
            if (type == 0 && len == 0) { p += left - 7; continue; }   // 미리 잡아 둔 빈 자리
            if (p + len > data.Length) break;                          // 쓰다 만 꼬리
            var frag = new ArraySegment<byte>(data, p, len);
            p += len;
            switch (type)
            {
                case 1: ApplyBatch(frag.ToArray(), put); rec.Clear(); break;
                case 2: rec.Clear(); rec.AddRange(frag); break;
                case 3: rec.AddRange(frag); break;
                case 4: rec.AddRange(frag); ApplyBatch(rec.ToArray(), put); rec.Clear(); break;
            }
        }
    }

    // WriteBatch: 순번 8바이트, 개수 4바이트, 그 뒤로 (1=넣기 키 값 | 0=지우기 키)
    static void ApplyBatch(byte[] b, Action<byte[], ulong, byte[]?> put)
    {
        if (b.Length < 12) return;
        ulong seq = BitConverter.ToUInt64(b, 0);
        int count = BitConverter.ToInt32(b, 8);
        int p = 12;
        for (int i = 0; i < count && p < b.Length; i++)
        {
            byte tag = b[p++];
            int klen = (int)Varint(b, ref p);
            var key = b[p..(p + klen)]; p += klen;
            if (tag == 1)
            {
                int vlen = (int)Varint(b, ref p);
                put(key, seq + (ulong)i, b[p..(p + vlen)]);
                p += vlen;
            }
            else put(key, seq + (ulong)i, null);
        }
    }

    // 테이블: 끝 48바이트가 꼬리말(메타 인덱스·인덱스 위치 + 매직). 인덱스가 가리키는 데이터 블록을 모두 읽는다.
    static void ReadTable(byte[] t, Action<byte[], ulong, byte[]?> put)
    {
        if (t.Length < 48) return;
        int p = t.Length - 48;
        Varint(t, ref p); Varint(t, ref p);                  // 메타 인덱스는 쓰지 않는다
        int idxOff = (int)Varint(t, ref p), idxLen = (int)Varint(t, ref p);
        foreach (var (_, handle) in BlockEntries(ReadBlock(t, idxOff, idxLen)))
        {
            int q = 0;
            int off = (int)Varint(handle, ref q), len = (int)Varint(handle, ref q);
            foreach (var (ikey, val) in BlockEntries(ReadBlock(t, off, len)))
            {
                if (ikey.Length < 8) continue;
                // 내부 키 = 사용자 키 + (순번 << 8 | 종류). 종류 1=값, 0=삭제
                ulong trailer = BitConverter.ToUInt64(ikey, ikey.Length - 8);
                put(ikey[..^8], trailer >> 8, (trailer & 0xFF) == 1 ? val : null);
            }
        }
    }

    // 블록 뒤에 종류 1바이트(0=그대로, 1=Snappy)와 체크섬 4바이트가 붙는다
    static byte[] ReadBlock(byte[] t, int off, int len)
    {
        var raw = t[off..(off + len)];
        return t[off + len] switch
        {
            0 => raw,
            1 => Snappy(raw),
            _ => throw new InvalidDataException("compression " + t[off + len]),
        };
    }

    // 블록 항목은 앞 키와 겹치는 길이만큼 줄여 적는다 (공유 길이, 새 길이, 값 길이). 끝에 재시작 지점 표가 있다.
    static IEnumerable<(byte[] Key, byte[] Val)> BlockEntries(byte[] b)
    {
        int restarts = BitConverter.ToInt32(b, b.Length - 4);
        int end = b.Length - 4 - restarts * 4;
        int p = 0;
        var key = Array.Empty<byte>();
        while (p < end)
        {
            int shared = (int)Varint(b, ref p), fresh = (int)Varint(b, ref p), vlen = (int)Varint(b, ref p);
            var k = new byte[shared + fresh];
            Array.Copy(key, k, shared);
            Array.Copy(b, p, k, shared, fresh);
            p += fresh;
            key = k;
            yield return (k, b[p..(p + vlen)]);
            p += vlen;
        }
    }

    // Snappy 풀기: 원래 길이(varint) 다음에 글자 그대로 / 앞에서 베끼기 조각이 이어진다
    static byte[] Snappy(byte[] s)
    {
        int p = 0;
        var o = new byte[(int)Varint(s, ref p)];
        int w = 0;
        while (p < s.Length)
        {
            byte tag = s[p++];
            int len, off;
            switch (tag & 3)
            {
                case 0:
                    len = tag >> 2;
                    if (len >= 60)
                    {
                        int n = len - 59;
                        len = 0;
                        for (int i = 0; i < n; i++) len |= s[p++] << (8 * i);
                    }
                    len += 1;
                    Array.Copy(s, p, o, w, len);
                    p += len; w += len;
                    continue;
                case 1: len = ((tag >> 2) & 7) + 4; off = (tag >> 5) << 8 | s[p++]; break;
                case 2: len = (tag >> 2) + 1; off = s[p] | s[p + 1] << 8; p += 2; break;
                default: len = (tag >> 2) + 1; off = BitConverter.ToInt32(s, p); p += 4; break;
            }
            // 겹쳐 베낄 수 있어 한 바이트씩 옮긴다
            for (int i = 0; i < len; i++, w++) o[w] = o[w - off];
        }
        return o;
    }
}
