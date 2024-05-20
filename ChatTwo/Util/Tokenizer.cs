using System.Text.RegularExpressions;

namespace ChatTwo.Util;

// Modified from: https://jack-vanlightly.com/blog/2016/2/24/a-more-efficient-regex-tokenizer
public static class Tokenizer
{
    public enum TokenType
    {
        CloseParenthesis,
        Comma,
        Dot,
        QuestionMark,
        ExclamationMark,
        Semicolon,
        Whitespace,
        Equals,
        OpenParenthesis,
        StringValue,
        Leftover,
        SequenceTerminator
    }

    public class Token(TokenType tokenType, string value)
    {
        public Token(TokenType tokenType) : this(tokenType, string.Empty) { }

        public TokenType TokenType { get; } = tokenType;
        public string Value { get; } = value;
    }

    public static class PrecedenceBasedRegexTokenizer
    {
        private static readonly List<TokenDefinition> TokenDefinitions;

        static PrecedenceBasedRegexTokenizer()
        {
            TokenDefinitions = new List<TokenDefinition>
            {
                new(TokenType.CloseParenthesis, "\\)", 1),
                new(TokenType.Comma, ",", 1),
                new(TokenType.Dot, "\\.", 1),
                new(TokenType.QuestionMark, "\\?", 1),
                new(TokenType.ExclamationMark, "!", 1),
                new(TokenType.Semicolon, ";", 1),
                new(TokenType.Whitespace, "\\s", 1),
                new(TokenType.Equals, "=", 1),
                new(TokenType.OpenParenthesis, "\\(", 1),
                new(TokenType.StringValue, "\\p{IsBasicLatin}", 2),
                new(TokenType.Leftover, ".", 3)
            };
        }

        public static IEnumerable<Token> Tokenize(string lqlText)
        {
            var tokenMatches = FindTokenMatches(lqlText);

            var groupedByIndex = tokenMatches.GroupBy(x => x.StartIndex)
                .OrderBy(x => x.Key)
                .ToList();

            TokenMatch? lastMatch = null;
            foreach (var t in groupedByIndex)
            {
                var bestMatch = t.OrderBy(x => x.Precedence).First();
                if (lastMatch != null && bestMatch.StartIndex < lastMatch.EndIndex)
                    continue;

                yield return new Token(bestMatch.TokenType, bestMatch.Value);

                lastMatch = bestMatch;
            }

            yield return new Token(TokenType.SequenceTerminator);
        }

        private static List<TokenMatch> FindTokenMatches(string lqlText)
        {
            var tokenMatches = new List<TokenMatch>();

            foreach (var tokenDefinition in TokenDefinitions)
                tokenMatches.AddRange(tokenDefinition.FindMatches(lqlText).ToList());

            return tokenMatches;
        }
    }

    private class TokenDefinition(TokenType returnsToken, string regexPattern, int precedence)
    {
        private readonly Regex Regex = new(regexPattern, RegexOptions.IgnoreCase|RegexOptions.Compiled);

        public IEnumerable<TokenMatch> FindMatches(string inputString)
        {
            var matches = Regex.Matches(inputString);
            for(var i = 0; i < matches.Count; i++)
            {
                yield return new TokenMatch
                {
                    StartIndex = matches[i].Index,
                    EndIndex = matches[i].Index + matches[i].Length,
                    TokenType = returnsToken,
                    Value = matches[i].Value,
                    Precedence = precedence
                };
            }
        }
    }

    private class TokenMatch
    {
        public TokenType TokenType { get; set; }
        public string Value { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public int Precedence { get; set; }
    }
}