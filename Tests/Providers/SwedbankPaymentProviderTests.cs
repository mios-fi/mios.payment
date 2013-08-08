using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mios.Payment.Providers;
using Xunit;

namespace Tests.Providers {
	public class SwedbankPaymentProviderTests {
		private SwedbankPaymentProvider provider;
		private NameValueCollection successfulReturnValues;
		public SwedbankPaymentProviderTests() {
			string privateKey, bankCertificate;
			using(var reader = new StreamReader(typeof(SwedbankPaymentProviderTests).Assembly.GetManifestResourceStream("Tests.Resources.kaupmees_priv.pem"))) {
				privateKey = Uri.EscapeDataString(reader.ReadToEnd());
			}
			using(var reader = new StreamReader(typeof(SwedbankPaymentProviderTests).Assembly.GetManifestResourceStream("Tests.Resources.eyp_cert.pem"))) {
				bankCertificate = Uri.EscapeDataString(reader.ReadToEnd());
			}
			var url = Uri.EscapeDataString("https://www.seb.ee/cgi-bin/dv.sh/un3min.r");
			provider = new SwedbankPaymentProvider("account=10002050618003&merchantId=testvpos&receiverName=Keegi&url="+url+"&privateKey="+privateKey+"&bankCertificate="+bankCertificate);

			successfulReturnValues = new NameValueCollection {
				{ "VK_SERVICE", "1101" },
				{ "VK_VERSION", "008" },
				{ "VK_SND_ID", "EYP" },
				{ "VK_REC_ID", "testvpos" },
				{ "VK_STAMP", "12345" },
				{ "VK_T_NO", "3677" },
				{ "VK_AMOUNT", "100" },
				{ "VK_CURR", "EUR" },
				{ "VK_REC_ACC", "10002050618003" },
				{ "VK_REC_NAME", "ALLAS ALLAR" },
				{ "VK_SND_ACC", "10010046155012" },
				{ "VK_SND_NAME", "TIIGER Leopaold" },
				{ "VK_REF", "123453" },
				{ "VK_MSG", "Sample message" },
				{ "VK_T_DATE", "18.12.2012" },
				{ "VK_MAC", "ZxhObV/W83Rhc3h8c+E1v6CF7WPmOqJY7PvgktGpJCQTT7LuIFLfz0VE2Ic7Y/da+EC/+LBnnMoPeZxyh9j9nePZUIBVkH4VC6aWbvhtXjXrtivWDZmKYGttOaWYmtBZJaAjZ6j3uklxcPI8pPCMQI6c5Jr5AJWMU+r9smLUSGw=" },
				{ "VK_LANG", "ENG" },
				{ "VK_RETURN", "http://localhost:50075/index.cshtml" },
				{ "VK_AUTO", "N" }
			};
		}
		[Fact]
		public void ProducesExpectedHMAC() {
			var details = provider.GenerateDetails("123", "88", 33.00m, "http://kool.kng.edu.ee/est/i.php", "http://kool.kng.edu.ee/est/i.php", "Porgandid");
			var expected = "TsDpZ4xgw1RAnVUCDybzcSXrrF5jTsjKKaxGK6H8zN3U637kCwcY6u1lfX/WtcZ7+JbNIRjzWfDFsndJ98ndCwsETb07AWnV7LVwVKt5IWHFMYzwhjq9bMkk6BohAR4VJ5ngmc1rFmOLDiOCbLZPwxFJEI7jeTDlFjJSUtWzkbQ=";
			Assert.Equal(expected, details.Fields["VK_MAC"]);
		}

		[Fact]
		public void VerifiesSuccessfulReturnHMAC() {
			Assert.True(provider.VerifyResponse("12345", 100m, successfulReturnValues));
		}
		[Fact]
		public void RejectsMismatchedAmountPaid() {
			// This doesn't really verify that we catch mismatched amounts. Since we dont update the HMAC, 
			// which we cant easily do without the secret part of the bank certificate, HMAC will always fail.
			var values = new NameValueCollection(successfulReturnValues) { 
				{ "VK_AMOUNT", "103.05" }
			};
			Assert.False(provider.VerifyResponse("12345", 100m, values));
		}
		[Fact]
		public void RejectsMismatchedStamp() {
			// This doesn't really verify that we catch mismatched stamp. Since we dont update the HMAC, 
			// which we cant easily do without the secret part of the bank certificate, HMAC will always fail.
			var values = new NameValueCollection(successfulReturnValues) { 
				{ "VK_STAMP", "11111" }
			};
			Assert.False(provider.VerifyResponse("12345", 100m, values));
		}
	}
}
