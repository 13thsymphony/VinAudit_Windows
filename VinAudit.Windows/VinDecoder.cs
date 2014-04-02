using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VinAudit
{
    // Validation based on info from: http://en.wikipedia.org/wiki/Vehicle_identification_number
    public struct VinValidationInfo
    {
        // IsValid = (IsCorrectLength && IsChecksumValid && HasValidCharacters).
        public bool IsValid;
        public bool IsCorrectLength;
        public bool HasValidCharacters;
        public bool IsChecksumValid;

        // We attempt to apply basic corrections such as capitalization and converting invalid characters.
        public bool IsChecksumValidAfterCanonicalization;

        // null if canonicalization was not possible.
        public string CanonicalizedString;
    }

    /// <summary>
    /// Performs best effort (basic) processing a VIN:
    /// - Validity check
    /// - VIN-encoded vehicle info (not implememented yet)
    /// - Autocorrection
    /// </summary>
    public static class VinDecoder
    {
        private const int VIN_EXPECTED_LENGTH = 17;
        private const int VIN_CHECKSUM_MODULUS = 11;

        // Zero-based array index of checksum position.
        private const int VIN_CHECKSUM_INDEX = 8;

        // Maps a char to the VIN checksum value (based on EBCDIC).
        // http://en.wikipedia.org/wiki/EBCDIC
        // Only contains entries for strictly valid characters
        // (uppercase letters and digits).
        // We define strict validity as "canonical".
        private static Dictionary<char, int> m_vinCharToEbcdic;

        // Maps an index into the VIN string to checksum weight.
        private static int[] m_checksumWeights = 
        {
            // Position:
            // 1  2  3  4  5  6  7  8   9 10 11 12 13 14 15 16 17
               8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2
        };

        // Maps the checksum remainder to the VIN check digit.
        private static char[] m_remainderToCheckDigit = 
        {
            // Remainder value:
            // 0    1    2    3    4    5    6    7    8    9    10
              '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'X'
        };

        static VinDecoder()
        {
            Debug.Assert(m_remainderToCheckDigit.Length + 1 == VIN_CHECKSUM_MODULUS);

            m_vinCharToEbcdic = new Dictionary<char, int>();
            m_vinCharToEbcdic.Add('A', 1);
            m_vinCharToEbcdic.Add('B', 2);
            m_vinCharToEbcdic.Add('C', 3);
            m_vinCharToEbcdic.Add('D', 4);
            m_vinCharToEbcdic.Add('E', 5);
            m_vinCharToEbcdic.Add('F', 6);
            m_vinCharToEbcdic.Add('G', 7);
            m_vinCharToEbcdic.Add('H', 8);
            // "I" is intentionally omitted, invalid VIN character.
            m_vinCharToEbcdic.Add('J', 1);
            m_vinCharToEbcdic.Add('K', 2);
            m_vinCharToEbcdic.Add('L', 3);
            m_vinCharToEbcdic.Add('M', 4);
            m_vinCharToEbcdic.Add('N', 5);
            // "O" is intentionally omitted, invalid VIN character.
            m_vinCharToEbcdic.Add('P', 7);
            // "Q" is intentionally omitted, invalid VIN character.
            m_vinCharToEbcdic.Add('R', 9);
            // "S" intentionally skips 1 value.
            m_vinCharToEbcdic.Add('S', 2);
            m_vinCharToEbcdic.Add('T', 3);
            m_vinCharToEbcdic.Add('U', 4);
            m_vinCharToEbcdic.Add('V', 5);
            m_vinCharToEbcdic.Add('W', 6);
            m_vinCharToEbcdic.Add('X', 7);
            m_vinCharToEbcdic.Add('Y', 8);
            m_vinCharToEbcdic.Add('Z', 9);
            m_vinCharToEbcdic.Add('0', 0);
            m_vinCharToEbcdic.Add('1', 1);
            m_vinCharToEbcdic.Add('2', 2);
            m_vinCharToEbcdic.Add('3', 3);
            m_vinCharToEbcdic.Add('4', 4);
            m_vinCharToEbcdic.Add('5', 5);
            m_vinCharToEbcdic.Add('6', 6);
            m_vinCharToEbcdic.Add('7', 7);
            m_vinCharToEbcdic.Add('8', 8);
            m_vinCharToEbcdic.Add('9', 9);
        }

        public static VinValidationInfo ValidateVin(string vin)
        {
            VinValidationInfo info = new VinValidationInfo();

            if (vin == null)
            {
                return info;
            }

            info.IsCorrectLength = (vin.Length == VIN_EXPECTED_LENGTH);
            info.HasValidCharacters = hasValidCharacters(vin);

            // Checksum can only be valid if preceding conditions are correct.
            if (info.IsCorrectLength && info.HasValidCharacters)
            {
                info.IsChecksumValid = (vin[VIN_CHECKSUM_INDEX] == getCheckDigit(vin));
            }

            info.CanonicalizedString = canonicalizeVinString(vin);
            if (info.CanonicalizedString != null && info.IsCorrectLength)
            {
                info.IsChecksumValidAfterCanonicalization =
                    (info.CanonicalizedString[VIN_CHECKSUM_INDEX] == getCheckDigit(info.CanonicalizedString));
            }

            info.IsValid = (info.IsCorrectLength && info.IsChecksumValid && info.HasValidCharacters);

            return info;
        }

        private static bool hasValidCharacters(string vin)
        {
            for (int i = 0; i < vin.Length; i++)
            {
                if (!m_vinCharToEbcdic.ContainsKey(vin[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Capitalizes letters and replaces I, O, and Q (invalid VIN characters).
        /// </summary>
        /// <param name="input"></param>
        /// <returns>null if canonicalization could not be done. This string may be identical to the input 
        /// if it already was canonical.</returns>
        private static string canonicalizeVinString(string input)
        {
            char[] output = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                c = Char.ToUpper(c);

                // Very simple conversion here.
                switch (c)
                {
                    case 'I':
                        c = '1';
                        break;
                    case 'O':
                        c = '0';
                        break;
                    case 'Q':
                        c = '0';
                        break;
                }

                if (m_vinCharToEbcdic.ContainsKey(c))
                {
                    output[i] = c;
                }
                else
                {
                    return null;
                }
            }

            return new string(output);
        }

        /// <summary>
        /// Calculates check digit (checksum) of VIN string.
        /// </summary>
        /// <param name="vin">Only accepts canonical VIN characters.</param>
        /// <returns>'\0' means the checksum could not be calculated (is invalid).</returns>
        private static char getCheckDigit(string vin)
        {
            if (vin.Length != VIN_EXPECTED_LENGTH)
            {
                return '\0';
            }

            int accumulated = 0;
            for (int i = 0; i < VIN_EXPECTED_LENGTH; i++)
            {
                int value;
                if (m_vinCharToEbcdic.TryGetValue(vin[i], out value))
                {
                    accumulated += value * m_checksumWeights[i];
                }
                else
                {
                    return '\0';
                }
            }

            int remainder = accumulated % VIN_CHECKSUM_MODULUS;
            char checksum = m_remainderToCheckDigit[remainder];
            return checksum;
        }
    }
}
