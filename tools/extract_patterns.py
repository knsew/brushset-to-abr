#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从本地 Photoshop 安装提取 11 个内置真实 pattern，生成 PatternData.cs。

背景：近似纹理方案引用 Photoshop 自带的内置 pattern（Towel/Stone/冷压等）。
这些 pattern 的像素与"尾部"（专有校验和）来自 Adobe 随附素材，有版权，不能随
本仓库再分发，因此 PatternData.cs 在开源版里是空数组。
本脚本让你在自己的 Photoshop 上提取这些 pattern，生成本机可用的 PatternData.cs，
重新编译即可恢复近似纹理功能。生成物仅供个人使用，请勿再分发生成的 pattern 数据。

用法：
    python tools/extract_patterns.py [Photoshop安装目录] [-o 输出.cs]
默认目录探测：C:/Program Files/Adobe/Adobe Photoshop */ 与 D:/Adobe/Adobe Photoshop */
"""
import struct, sys, os, glob

DEFAULT_SUBS = ["Towel", "KraftPaper", "PebbleBoard", "FineGrain", "Stone=Stone",
                "ColdPress", "Laid-horizontal", "Laid-vertical",
                "BlackMarble", "Stones=Stones", "Gravel=Gravel"]


def r32(d, o): return struct.unpack('>I', d[o:o+4])[0]
def r16(d, o): return struct.unpack('>H', d[o:o+2])[0]


def gather_pat(path):
    data = open(path, 'rb').read()
    if data[:4] != b'8BPT':
        return []
    count = r32(data, 6)
    o = 10
    out = []
    guard = 0
    while len(out) < count and guard < count * 3:
        guard += 1
        if o + 12 > len(data):
            break
        o0 = o
        version = r32(data, o); o += 4
        mode = r32(data, o); o += 4
        h, w = struct.unpack('>HH', data[o:o+4]); o += 4
        namec = r32(data, o); o += 4
        if namec < 2 or namec > 300:
            break
        name = data[o:o+(namec-1)*2].decode('utf-16-be'); o += (namec-1)*2 + 2
        idlen = data[o]; o += 1
        pid = data[o:o+idlen].decode('latin1'); o += idlen
        if mode == 3 or mode == 1:
            ch = 3 if mode == 3 else 1
            body = data[o0:o+60+h*w*ch+153]
            out.append((name, pid, bytes(body)))
            o += 60 + h*w*ch + 153
        elif mode == 8:
            j = data.find(b'\x00\x00\x00\x01', o)
            if j < 0:
                break
            o = j
        else:
            break
    return out


def gather_abr(path):
    data = open(path, 'rb').read()
    pos = 4
    blocks = {}
    while pos + 12 <= len(data):
        k = data[pos+4:pos+8].decode('latin1')
        ln = r32(data, pos+8)
        blocks[k] = (pos, ln)
        pos += 12 + ln
    if 'patt' not in blocks:
        return []
    patt = data[blocks['patt'][0]+12:blocks['patt'][0]+12+blocks['patt'][1]]
    out = []
    # Default Brushes.abr 内嵌的 256x256 真实 pattern 条目（长度/偏移由 ABR patt 块结构决定）
    for off, L in [(1204, 196962), (198172, 196940), (436668, 196966),
                   (641448, 196958), (634764, 6678)]:
        body = patt[off+4:off+4+L]
        if len(body) < L:
            continue
        namec = r32(body, 12)
        name = body[16:16+(namec-1)*2].decode('utf-16-be', 'replace')
        idpos = 16 + namec*2
        idlen = body[idpos]
        pid = body[idpos+1:idpos+1+idlen].decode('latin1')
        out.append((name, pid, bytes(body)))
    return out


def find_photoshop_dir():
    patterns = [r'C:/Program Files/Adobe/Adobe Photoshop */',
                r'D:/Adobe/Adobe Photoshop */',
                r'C:/Program Files/Adobe/Photoshop */']
    for p in patterns:
        hits = sorted(glob.glob(p))
        if hits:
            return hits[-1]
    return None


def emit_cs(sel, out_path):
    lines = []
    lines.append("// 自动生成（tools/extract_patterns.py）：真实 PS pattern 库，含合法尾部。勿手改。")
    lines.append("// 注意：生成物含 Adobe 随附素材，仅供个人使用，勿再分发。")
    lines.append("using System;")
    lines.append("")
    lines.append("namespace BrushStudio")
    lines.append("{")
    lines.append("    internal static class PatternData")
    lines.append("    {")
    lines.append("        public static readonly string[] Names = new string[]")
    lines.append("        {")
    for name, _pid, _b in sel:
        lines.append("            \"%s\"," % name.replace('\\', '\\\\').replace('"', '\\"'))
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly string[] Ids = new string[]")
    lines.append("        {")
    for _name, pid, _b in sel:
        lines.append("            \"%s\"," % pid)
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly byte[][] Entries = new byte[][]")
    lines.append("        {")
    for _name, _pid, body in sel:
        lines.append("            new byte[] {")
        for i in range(0, len(body), 24):
            chunk = body[i:i+24]
            lines.append("                " + ",".join("0x%02x" % b for b in chunk) + ",")
        lines.append("            },")
    lines.append("        };")
    lines.append("    }")
    lines.append("}")
    with open(out_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write("\n".join(lines))
    print("wrote", out_path)


def main():
    args = sys.argv[1:]
    out_path = None
    ps_dir = None
    if '-o' in args:
        i = args.index('-o')
        out_path = args[i+1]
        args = args[:i] + args[i+2:]
    if args:
        ps_dir = args[0]
    if not ps_dir:
        ps_dir = find_photoshop_dir()
    if not ps_dir or not os.path.isdir(ps_dir):
        print("未找到 Photoshop 安装目录。请指定：python tools/extract_patterns.py <PS目录>")
        sys.exit(1)
    if not out_path:
        out_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'PatternData.cs')

    cand = []
    presets = os.path.join(ps_dir, 'Presets', 'Patterns')
    for name in ('Erodible Textures.pat', 'Color Paper.pat', 'Rock Patterns.pat'):
        p = os.path.join(presets, name)
        if os.path.exists(p):
            cand += gather_pat(p)
    abr = os.path.join(ps_dir, 'Required', 'Default Brushes.abr')
    if os.path.exists(abr):
        cand += gather_abr(abr)
    if not cand:
        print("未从 %s 提取到任何 pattern。" % ps_dir)
        sys.exit(1)

    def pick(sub):
        for c in cand:
            if sub in c[0]:
                return c
        raise KeyError(sub)

    sel = []
    for sub in DEFAULT_SUBS:
        sel.append(pick(sub))
    print('selected %d patterns from %s:' % (len(sel), ps_dir))
    for name, pid, body in sel:
        print('  %-55s id=%s body=%d' % (name[:55], pid[:8], len(body)))
    emit_cs(sel, out_path)


if __name__ == '__main__':
    main()
