using Avalonia.Collections;
using ColorTextBlock.Avalonia;
using ELOR.Laney.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ELOR.Laney.Helpers {
    enum MatchType { User, Group, LinkInText, Mail, Url }

    class MatchInfo {
        public int Start { get; private set; }
        public int Length { get; private set; }
        public MatchType Type { get; private set; }
        public Match Match { get; private set; }

        public MatchInfo(int start, int length, MatchType type, Match match) {
            Start = start;
            Length = length;
            Type = type;
            Match = match;
        }
    }

    public class TextParser {
        private enum InlineFormatKind { Bold, Italic, Code, Strike }

        private readonly struct InlineFormatMatch {
            public int Start { get; }
            public int End { get; }
            public string Marker { get; }
            public InlineFormatKind Kind { get; }

            public InlineFormatMatch(int start, int end, string marker, InlineFormatKind kind) {
                Start = start;
                End = end;
                Marker = marker;
                Kind = kind;
            }
        }

        #region Internal parsing methods

        private static Tuple<string, string> ParseBracketWord(Match match) {
            return new Tuple<string, string>($"https://vk.com/{match.Groups[1]}{match.Groups[2]}", match.Groups[3].Value);
        }

        private static Tuple<string, string> ParseLinkInBracketWord(Match match) {
            return new Tuple<string, string>(match.Groups[1].Value, match.Groups[2].Value);
        }

        private static List<Tuple<string, string>> GetRaw(string plain, bool dontParseUrls = false) {
            plain = plain.Trim();
            List<Tuple<string, string>> raw = new List<Tuple<string, string>>();
            List<MatchInfo> allMatches = new List<MatchInfo>();

            var userMatches = CompiledRegularExpressions.UserMention().Matches(plain).Cast<Match>().ToList();
            foreach (var m in CollectionsMarshal.AsSpan(userMatches)) allMatches.Add(new MatchInfo(m.Index, m.Length, MatchType.User, m));

            var groupMatches = CompiledRegularExpressions.GroupMention().Matches(plain).Cast<Match>().ToList();
            foreach (var m in CollectionsMarshal.AsSpan(groupMatches)) allMatches.Add(new MatchInfo(m.Index, m.Length, MatchType.Group, m));

            var linkMatches = CompiledRegularExpressions.LinkInText().Matches(plain).Cast<Match>().ToList();
            foreach (var m in CollectionsMarshal.AsSpan(linkMatches)) allMatches.Add(new MatchInfo(m.Index, m.Length, MatchType.LinkInText, m));

            if (!dontParseUrls) {
                var emailMatches = CompiledRegularExpressions.Email().Matches(plain).Cast<Match>().ToList();
                foreach (var m in CollectionsMarshal.AsSpan(emailMatches)) allMatches.Add(new MatchInfo(m.Index, m.Length, MatchType.Mail, m));

                var urlMatches = CompiledRegularExpressions.URL().Matches(plain).Cast<Match>().ToList();
                foreach (var m in CollectionsMarshal.AsSpan(urlMatches)) allMatches.Add(new MatchInfo(m.Index, m.Length, MatchType.Url, m));
            }

            allMatches = allMatches.OrderBy(m => m.Start).ToList();

            string word = String.Empty;
            for (int i = 0; i < plain.Length; i++) {
                var matchInfo = allMatches.Where(m => m.Start == i).FirstOrDefault();
                if (matchInfo != null) {
                    raw.Add(new Tuple<string, string>(null, word));
                    word = String.Empty;

                    Match match = matchInfo.Match;
                    switch (matchInfo.Type) {
                        case MatchType.User:
                        case MatchType.Group: raw.Add(ParseBracketWord(match)); break;
                        case MatchType.LinkInText: raw.Add(ParseLinkInBracketWord(match)); break;
                        case MatchType.Mail: raw.Add(new Tuple<string, string>($"mailto:{match}", match.Value)); break;
                        case MatchType.Url:
                            string url = match.Value;
                            if (!url.StartsWith("https://") && !url.StartsWith("http://")) url = $"https://{url}";
                            raw.Add(new Tuple<string, string>(url, match.Value));
                            break;
                    }

                    i = i + matchInfo.Length - 1;
                } else {
                    word += plain[i];
                }
            }
            raw.Add(new Tuple<string, string>(null, word));

            return raw;
        }

        #endregion

        #region For CTextBlock

        private static CRun BuildCRunForRTBStyle(string text) {
            return new CRun {
                Text = text
            };
        }

        private static void AddFormattedText(AvaloniaList<CInline> content, string text, long peerId) {
            foreach (CInline inline in BuildFormattedInlines(text, peerId)) {
                content.Add(inline);
            }
        }

        private static List<CInline> BuildFormattedInlines(string text, long peerId) {
            List<CInline> result = new List<CInline>();
            ParseFormattedText(result, text, peerId);
            return result;
        }

        private static void ParseFormattedText(List<CInline> output, string text, long peerId) {
            if (String.IsNullOrEmpty(text)) return;

            int index = 0;
            while (index < text.Length) {
                if (!TryFindNextFormat(text, index, out InlineFormatMatch match)) {
                    AddPlainText(output, text.Substring(index), peerId);
                    return;
                }

                if (match.Start > index) AddPlainText(output, text.Substring(index, match.Start - index), peerId);

                string inner = text.Substring(match.Start + match.Marker.Length, match.End - match.Start - match.Marker.Length);
                output.Add(BuildFormattedInline(match.Kind, inner, peerId));
                index = match.End + match.Marker.Length;
            }
        }

        private static CInline BuildFormattedInline(InlineFormatKind kind, string inner, long peerId) {
            List<CInline> inlines = kind == InlineFormatKind.Code
                ? new List<CInline> { BuildCRunForRTBStyle(inner) }
                : BuildFormattedInlines(inner, peerId);

            return kind switch {
                InlineFormatKind.Bold => new CBold(inlines),
                InlineFormatKind.Italic => new CItalic(inlines),
                InlineFormatKind.Code => new CCode(inlines),
                InlineFormatKind.Strike => new CStrikethrough(inlines),
                _ => BuildCRunForRTBStyle(inner)
            };
        }

        private static void AddPlainText(List<CInline> output, string text, long peerId) {
            if (String.IsNullOrEmpty(text)) return;
            if (!EmojiSpriteStore.HasSprites(peerId)) {
                output.Add(BuildCRunForRTBStyle(text));
                return;
            }

            int index = 0;
            int textStart = 0;
            while (index < text.Length) {
                if (!EmojiSpriteStore.TryMatch(text, index, peerId, out EmojiSpriteMatch match)) {
                    index++;
                    continue;
                }

                if (index > textStart) output.Add(BuildCRunForRTBStyle(text.Substring(textStart, index - textStart)));
                output.Add(BuildEmojiSpriteInline(match));
                index += match.Length;
                textStart = index;
            }

            if (textStart < text.Length) output.Add(BuildCRunForRTBStyle(text.Substring(textStart)));
        }

        private static CInline BuildEmojiSpriteInline(EmojiSpriteMatch match) {
            return new CImage(EmojiSpriteStore.LoadImageAsync(match.FilePath), EmojiSpriteStore.PlaceholderImage) {
                LayoutWidth = 18,
                LayoutHeight = 18,
                SaveAspectRatio = true,
                FittingWhenProtrude = false,
                TextVerticalAlignment = TextVerticalAlignment.Center
            };
        }

        private static bool TryFindNextFormat(string text, int startIndex, out InlineFormatMatch match) {
            match = default;
            ReadOnlySpan<(string Marker, InlineFormatKind Kind)> formats = [
                ("**", InlineFormatKind.Bold),
                ("__", InlineFormatKind.Italic),
                ("`", InlineFormatKind.Code),
                ("~~", InlineFormatKind.Strike)
            ];

            bool found = false;
            foreach (var format in formats) {
                int start = text.IndexOf(format.Marker, startIndex, StringComparison.Ordinal);
                if (start < 0) continue;

                int contentStart = start + format.Marker.Length;
                int end = text.IndexOf(format.Marker, contentStart, StringComparison.Ordinal);
                if (end <= contentStart) continue;

                if (!found || start < match.Start || start == match.Start && format.Marker.Length > match.Marker.Length) {
                    match = new InlineFormatMatch(start, end, format.Marker, format.Kind);
                    found = true;
                }
            }

            return found;
        }

        private static string StripFormatting(string text) {
            if (String.IsNullOrEmpty(text)) return text;
            StringBuilder sb = StringBuilderCache.Acquire(text.Length);
            int index = 0;

            while (index < text.Length) {
                if (!TryFindNextFormat(text, index, out InlineFormatMatch match)) {
                    sb.Append(text, index, text.Length - index);
                    break;
                }

                if (match.Start > index) sb.Append(text, index, match.Start - index);
                string inner = text.Substring(match.Start + match.Marker.Length, match.End - match.Start - match.Marker.Length);
                sb.Append(match.Kind == InlineFormatKind.Code ? inner : StripFormatting(inner));
                index = match.End + match.Marker.Length;
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static CHyperlink BuildCHyperlinkForRTBStyle(string text, string link, CTextBlock rtb, Action<string> clickedCallback) {
            CHyperlink hl = new CHyperlink(new List<CInline> {
                new CRun() {
                    Text = text
                }
            }) {
                CommandParameter = link
            };
            hl.Command = (a) => clickedCallback?.Invoke(a);
            return hl;
        }

        public static void SetText(string plain, CTextBlock rtb, Action<string> linksClickedCallback = null, long peerId = 0) {
            rtb.Content = new AvaloniaList<CInline>();
            if (string.IsNullOrEmpty(plain)) return;

            foreach (var token in GetRaw(plain)) {
                if (string.IsNullOrEmpty(token.Item1)) {
                    AddFormattedText(rtb.Content, token.Item2, peerId);
                } else {
                    CHyperlink h = BuildCHyperlinkForRTBStyle(token.Item2, token.Item1, rtb, linksClickedCallback);
                    rtb.Content.Add(h);
                }
            }
        }

        #endregion

        public static string GetParsedText(string plain) {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            StringBuilder sb = StringBuilderCache.Acquire(plain.Length);

            foreach (var token in CollectionsMarshal.AsSpan(GetRaw(plain))) {
                sb.Append(StripFormatting(token.Item2));
            }
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public static IReadOnlyList<string> GetLinks(string plain) {
            if (String.IsNullOrWhiteSpace(plain)) return Array.Empty<string>();

            return GetRaw(plain)
                .Where(token => !String.IsNullOrWhiteSpace(token.Item1))
                .Select(token => token.Item1)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static long GetMentionId(string plain) {
            var u = CompiledRegularExpressions.UserMention().Match(plain);
            if (u.Success) {
                return long.Parse(u.Groups[2].Value);
            } else {
                var g = CompiledRegularExpressions.GroupMention().Match(plain);
                if (g.Success) return -long.Parse(g.Groups[2].Value);
            }
            return 0;
        }
    }
}
