using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

using VinAudit;

namespace UnitTests
{
    [TestClass]
    public class VinDecoderTests
    {
        [TestMethod]
        public void ValidateVin_ValidVariations()
        {
            // All variations have same expected result.
            string[] variations =
            {
                "11111111111111111",
                "JNKCP11A8XT010806",
                "1VXBR12EXCP544329",
                "2G1FP32G022152856"
            };

            for (int i = 0; i < variations.Length; i++)
            {
                var info = VinDecoder.ValidateVin(variations[i]);
                Assert.AreEqual(info.HasValidCharacters, true);
                Assert.AreEqual(info.IsChecksumValid, true);
                Assert.AreEqual(info.IsChecksumValidAfterCanonicalization, true);
                Assert.AreEqual(info.IsCorrectLength, true);
                Assert.AreEqual(info.IsValid, true);
                Assert.AreEqual(info.CanonicalizedString, variations[i]);
            }
        }

        [TestMethod]
        public void ValidateVin_CanonicalizeSuccess()
        {
            string[] variations =
            {
                "I1I1i1ii1III1i111",
                "JNKCP11A8XTq1O8o6",
                "2GiFP32GQ22I52856"
            }; 
            
            string[] canonicalized =
            {
                "11111111111111111",
                "JNKCP11A8XT010806",
                "2G1FP32G022152856"
            };


            for (int i = 0; i < variations.Length; i++)
            {
                var info = VinDecoder.ValidateVin(variations[i]);
                Assert.AreEqual(info.HasValidCharacters, false);
                Assert.AreEqual(info.IsChecksumValid, false);
                Assert.AreEqual(info.IsChecksumValidAfterCanonicalization, true);
                Assert.AreEqual(info.IsCorrectLength, true);
                Assert.AreEqual(info.IsValid, false);
                Assert.AreEqual(info.CanonicalizedString, canonicalized[i]);
            }
        }

        [TestMethod]
        public void ValidateVin_BadLengths()
        {
            string[] variations =
            {
                "1111111111111111",
                "JNKCP11A8XT0108061",
                "",
                "129481"
            };

            for (int i = 0; i < variations.Length; i++)
            {
                var info = VinDecoder.ValidateVin(variations[i]);
                Assert.AreEqual(info.HasValidCharacters, true);
                Assert.AreEqual(info.IsChecksumValid, false);
                Assert.AreEqual(info.IsChecksumValidAfterCanonicalization, false);
                Assert.AreEqual(info.IsCorrectLength, false);
                Assert.AreEqual(info.IsValid, false);
                Assert.AreEqual(info.CanonicalizedString, variations[i]);
            }
        }

        [TestMethod]
        public void ValidateVin_Junk()
        {
            string[] variations =
            {
                null,
                "\\],.;';/;"
            };

            for (int i = 0; i < variations.Length; i++)
            {
                var info = VinDecoder.ValidateVin(variations[i]);
                Assert.AreEqual(info.HasValidCharacters, false);
                Assert.AreEqual(info.IsChecksumValid, false);
                Assert.AreEqual(info.IsChecksumValidAfterCanonicalization, false);
                Assert.AreEqual(info.IsCorrectLength, false);
                Assert.AreEqual(info.IsValid, false);
                Assert.AreEqual(info.CanonicalizedString, null);
            }
        }
    }
}
