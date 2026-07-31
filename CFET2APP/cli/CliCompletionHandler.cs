using System;
using System.Collections.Generic;
using System.Linq;

namespace Jtext103.CFET2.CFET2App.cli
{
    /// <summary>
    /// cfet2 auto tab complete cli handler
    /// </summary>
    internal class CliCompletionHandler : IAutoCompleteHandler
    {
        private readonly CliParser cli;

        public CliCompletionHandler(CliParser cli)
        {
            this.cli = cli;
        }

        public char[] Separators { get; set; } = new[] { ' ' };

        public string[] GetSuggestions(string text, int index)
        {
            var currentWord = GetCurrentWord(text, index);

            // 获取所有可能的补全项
            var matches = cli.GetCompletionItems(text, text.Length)
                .Select(item => string.IsNullOrEmpty(item.InsertText) ? item.Label : item.InsertText)
                .Where(match =>
                    match.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) ||
                    match.StartsWith("/", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(match => match, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 没有匹配项
            if (matches.Count == 0)
            {
                return null;
            }

            // 只有一个匹配项，直接返回（让库自动补全）
            if (matches.Count == 1)
            {
                return matches.ToArray();
            }

            // 多个匹配项：计算最长公共前缀
            string commonPrefix = GetLongestCommonPrefix(matches);

            // 如果有公共前缀，返回公共前缀（实现部分补全）
            if (commonPrefix.Length > currentWord.Length)
            {
                return new string[] { commonPrefix };
            }

            // 没有公共前缀，返回所有匹配项（回退到菜单补全行为）
            return matches.ToArray();
        }

        private string GetCurrentWord(string text, int index)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            index = Math.Max(0, Math.Min(index, text.Length));
            return text.Substring(index);
        }

        /// <summary>
        /// 计算一组字符串的最长公共前缀
        /// </summary>
        private string GetLongestCommonPrefix(List<string> strings)
        {
            if (strings == null || strings.Count == 0)
                return string.Empty;

            if (strings.Count == 1)
                return strings[0];

            // 取第一个字符串作为基准
            string first = strings[0];

            for (int i = 0; i < first.Length; i++)
            {
                char currentChar = first[i];
                // 检查其他所有字符串的第 i 个字符
                if (strings.Any(s => s.Length <= i ||
                    char.ToUpperInvariant(s[i]) != char.ToUpperInvariant(currentChar)))
                {
                    // 找到不匹配的位置，返回前缀
                    return first.Substring(0, i);
                }
            }

            // 第一个字符串本身就是所有字符串的前缀
            return first;
        }
    }
}
