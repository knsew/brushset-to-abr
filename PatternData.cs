// 真实 PS pattern 库（可选）。本文件由 tools/extract_patterns.py 从你的本地 Photoshop 安装
// 生成——从 Adobe 随附的 .pat / Default Brushes 中提取 11 个内置真实 pattern（Towel/Stone/...）
// 的像素数据与合法尾部，作为近似纹理方案的内嵌来源。
//
// 开源版不包含 Adobe 版权素材：数组为空，近似纹理功能自动降级（匹配不到 pattern 的笔刷
// 不再写纹理，笔刷数量与其它功能不受影响）。
// 想恢复该功能：在装有 Photoshop 的本机运行 tools/extract_patterns.py，它会生成含真实数据的
// 本文件，重新编译即可。生成物仅供个人使用，请勿再分发生成的 pattern 数据。
using System;

namespace BrushStudio
{
    internal static class PatternData
    {
        public static readonly string[] Names = new string[] { };
        public static readonly string[] Ids = new string[] { };
        public static readonly byte[][] Entries = new byte[][] { };
    }
}
