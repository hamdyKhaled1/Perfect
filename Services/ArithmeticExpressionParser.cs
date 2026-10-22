using System.Globalization;
using System.Text;

namespace HagImamAccounts.Services
{
    public static class ArithmeticExpressionParser
    {
        public static bool TryParse(string? expression, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(expression) || expression.Length > 100)
                return false;

            var normalized = Normalize(expression);
            try
            {
                var parser = new Parser(normalized);
                result = parser.Parse();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (DivideByZeroException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static string Normalize(string expression)
        {
            var normalized = new StringBuilder(expression.Length);
            foreach (var character in expression)
            {
                if (char.GetUnicodeCategory(character) == UnicodeCategory.DecimalDigitNumber)
                    normalized.Append((int)char.GetNumericValue(character));
                else if (character is '٫')
                    normalized.Append('.');
                else if (character is not ('٬' or ',' or ' ' or '\t' or '\r' or '\n'))
                    normalized.Append(character);
            }
            return normalized.ToString();
        }

        private sealed class Parser
        {
            private readonly string _expression;
            private int _position;

            public Parser(string expression)
            {
                _expression = expression;
            }

            public decimal Parse()
            {
                var value = ParseExpression(0);
                if (_position != _expression.Length)
                    throw new FormatException();
                return value;
            }

            private decimal ParseExpression(int depth)
            {
                var value = ParseTerm(depth);
                while (TryRead('+') || TryRead('-'))
                {
                    var operation = _expression[_position - 1];
                    var next = ParseTerm(depth);
                    value = operation == '+' ? value + next : value - next;
                }
                return value;
            }

            private decimal ParseTerm(int depth)
            {
                var value = ParseFactor(depth);
                while (TryRead('*') || TryRead('/'))
                {
                    var operation = _expression[_position - 1];
                    var next = ParseFactor(depth);
                    value = operation == '*' ? value * next : value / next;
                }
                return value;
            }

            private decimal ParseFactor(int depth)
            {
                if (depth > 20)
                    throw new FormatException();

                if (TryRead('+'))
                    return ParseFactor(depth + 1);
                if (TryRead('-'))
                    return -ParseFactor(depth + 1);
                if (TryRead('('))
                {
                    var parenthesizedValue = ParseExpression(depth + 1);
                    if (!TryRead(')'))
                        throw new FormatException();
                    return parenthesizedValue;
                }

                var start = _position;
                var decimalPointSeen = false;
                while (_position < _expression.Length)
                {
                    var character = _expression[_position];
                    if (char.IsDigit(character))
                    {
                        _position++;
                        continue;
                    }
                    if (character == '.' && !decimalPointSeen)
                    {
                        decimalPointSeen = true;
                        _position++;
                        continue;
                    }
                    break;
                }

                if (start == _position || !decimal.TryParse(
                        _expression.AsSpan(start, _position - start),
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var value))
                    throw new FormatException();

                return value;
            }

            private bool TryRead(char character)
            {
                if (_position >= _expression.Length || _expression[_position] != character)
                    return false;
                _position++;
                return true;
            }
        }
    }
}