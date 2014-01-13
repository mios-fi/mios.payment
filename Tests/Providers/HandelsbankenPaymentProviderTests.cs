using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mios.Payment.Providers;
using Xunit;

namespace Tests.Providers {
	public class HandelsbankenPaymentProviderTests {
		private static readonly NameValueCollection positiveTestVector 
			= new NameValueCollection { 
				{"butikid", "9999"},
				{"ordernummer", "ABCD000001"},
				{"orderbelopp", "1100"},
				{"status", "0"},
				{"timestamp", "20011015121000"},
				{"kontrollsumma", "1f3caf897286c3159b65a705cf880570"},
		};

		private static readonly NameValueCollection negativeTestVector 
			= new NameValueCollection(positiveTestVector) { 
				{"status", "1"},
				{"kontrollsumma", "b6914cca7702f96983a06c694b00ce34"},
		};

		[Fact]
		public void GeneratedValuesShouldAgreeWithTestVectors() {
			var now = new DateTimeOffset(new DateTime(2001, 10, 14, 12, 15, 00));
			var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
				{"entryid","switch"},
				{"appaction","doDirectPay"},
				{"switchaction","3"},
				{"handOverDatatype","1"},
				{"appname","ssse"},
				{"language","sv"},
				{"country","se"},
				{"butikid","9999"},
				{"ordernummer","ABCD000001"},
				{"orderbelopp","1100"},
				{"senastebokningstid", "20011015121500"},
				{"retururl","https://www.butik.com/cgi/checkorder"},
				{"kontrollsumma","26efb0517cdfbbacb13a61e91feae16d"}
			};
			var provider = new HandelsbankenPaymentProvider("account=9999&secret=aaaabbbb") {
				Clock = () => now, Language = "sv", Country = "se"
			};
			var actual = provider.GenerateDetails(expected["ordernummer"], 1100m, expected["retururl"], expected["retururl"], "ignored");
			Assert.Equal("https://secure.handelsbanken.se/bb/glss/servlet/ssco_dirapp", actual.Url);
			foreach(var key in expected.Keys) {
				Assert.Equal(expected[key], actual.Fields[key]);
			}
		}
		[Fact]
		public void ValidationShouldBeSuccessfulForPositiveTestVectors() {
			var provider = new HandelsbankenPaymentProvider("account=9999&secret=aaaabbbb");
			Assert.True(provider.VerifyResponse(positiveTestVector["ordernummer"], 1100m, positiveTestVector));
		}
		[Fact]
		public void ValidationShouldFailForNegativeTestVectors() {
			var provider = new HandelsbankenPaymentProvider("account=9999&secret=aaaabbbb");
			Assert.False(provider.VerifyResponse(negativeTestVector["ordernummer"], 1100m, negativeTestVector));
		}
		[Fact]
		public void ValidationShouldFailForMismatchedHash() {
			var values = new NameValueCollection(positiveTestVector) {
				{"kontrollsumma", "111111117286c3159b65a705cf880570"}
			};
			var provider = new HandelsbankenPaymentProvider("account=9999&secret=aaaabbbb");
			Assert.False(provider.VerifyResponse(values["ordernummer"], 1100m, values));
		}
		[Fact]
		public void ValidationShouldFailForMismatchedIdentifier() {
			var provider = new HandelsbankenPaymentProvider("account=9999&secret=aaaabbbb");
			Assert.False(provider.VerifyResponse("0000000001", 1100m, positiveTestVector));
		}
	}
}
