using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.RestServer
{
    internal static class Quantity
    {
        private enum QuantityParseState
        {
            Start,
            Number,
            Suffix,
            DecimalExponent,
        }

        public static bool TryParse(string input, out long quantity)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException(input);
            }
            //See https://github.com/kubernetes/apimachinery/blob/master/pkg/api/resource/quantity.go for grammar
            //Note we only ever parse the canonical form of the number

            quantity = 0L;

            int quantityStart = 0;
            int quantityEnd = input.Length;

            int exponentStart = -1;

            char suffixFirst = '\0';
            char suffixSecond = '\0';

            QuantityParseState state = QuantityParseState.Start;

            for (int i = 0; i < input.Length; i++)
            {
                switch (state)
                {
                    case QuantityParseState.Start:
                        {
                            //Can start with +/-
                            if (char.IsNumber(input[i]))
                            {
                                state = QuantityParseState.Number;
                            }
                            else
                            {
                                ++quantityStart;
                            }
                        }
                        break;
                    case QuantityParseState.Number:
                        if (!char.IsNumber(input[i]))
                        {
                            quantityEnd = i;
                            state = QuantityParseState.Suffix;
                            suffixFirst = input[i];
                        }
                        break;
                    case QuantityParseState.Suffix:
                        if (char.IsNumber(input[i]))
                        {
                            state = QuantityParseState.DecimalExponent;
                            exponentStart = i;
                        }
                        else
                        {
                            suffixSecond = input[i];
                        }
                        break;
                }
            }

            if (state == QuantityParseState.Start)
            {
                return false;
            }

            if (!long.TryParse(input.Substring(quantityStart, quantityEnd), out long baseQuantity))
            {
                return false;
            }

            if (state == QuantityParseState.Number)
            {
                quantity = baseQuantity;
                return true;
            }

            long multiplier = 1;

            if (state == QuantityParseState.Suffix)
            {
                //These are case sensitive
                //Ki | Mi | Gi | Ti | Pi | Ei
                //k | M | G | T | P | E
                //Note n, u, m are also allowed but we don't expect these due to normalization
                switch (suffixSecond)
                {
                    case 'i':
                        switch (suffixFirst)
                        {
                            case 'K':
                                multiplier = 1024;
                                break;
                            case 'M':
                                multiplier = 1024 * 1024;
                                break;
                            case 'G':
                                multiplier = 1024 * 1024 * 1024;
                                break;
                            case 'T':
                                multiplier = 1024L * 1024L * 1024L * 1024L;
                                break;
                            case 'P':
                                multiplier = 1024L * 1024L * 1024L * 1024L * 1024;
                                break;
                            case 'E':
                                multiplier = 1024L * 1024L * 1024L * 1024L * 1024L * 1024L;
                                break;
                        }
                        break;
                    case '\0':
                        switch (suffixFirst)
                        {
                            case 'k':
                                multiplier = 1_000;
                                break;
                            case 'M':
                                multiplier = 1_000_000;
                                break;
                            case 'G':
                                multiplier = 1_000_000_000;
                                break;
                            case 'T':
                                multiplier = 1_000_000_000_000;
                                break;
                            case 'P':
                                multiplier = 1_000_000_000_000_000;
                                break;
                            case 'E':
                                multiplier = 1_000_000_000_000_000_000;
                                break;
                        }
                        break;
                }

                quantity = multiplier * baseQuantity;
                return true;
            }

            if (state == QuantityParseState.DecimalExponent)
            {
                if (!int.TryParse(input.Substring(exponentStart), out int exponent))
                {
                    return false;
                }

                for (int i = 0; i < exponent; i++)
                {
                    multiplier *= 10;
                }

                quantity = multiplier * baseQuantity;

                return true;
            }

            return false;
        }
    }
}
