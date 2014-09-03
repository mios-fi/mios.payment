using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mios.Payment.Providers;
using Mios.Payment;
using Xunit;

namespace Tests.Providers {
	public class DibsPaymentProviderTests {
		private DibsPaymentProvider provider;
		private NameValueCollection details;
		private string secret = "1234567890abcdef";

		public DibsPaymentProviderTests() { 
			this.provider = new DibsPaymentProvider() { Secret = secret, Currency = "EUR", MerchantId = "12345678" };
			this.details = new NameValueCollection {
				{"acceptReturnUrl","http://localhost:50075/"},
				{"acquirer","TEST"},
				{"actionCode","d100"},
				{"amount","10025"},
				{"cancelReturnUrl","http://localhost:50075/?error"},
				{"cardNumberMasked","471110XXXXXX0000"},
				{"cardTypeName","VISA"},
				{"currency","EUR"},
				{"expMonth","06"},
				{"expYear","24"},
				{"language","sv-FI"},
				{"merchant","12345678"},
				{"orderId","12345"},
				{"status","ACCEPTED"},
				{"test","1"},
				{"transaction","1234567890"}
			};
			this.details["MAC"] = Hash(this.details);
		}

		[Fact]
		public void AcceptsValidPayment() {
			Assert.True(provider.VerifyResponse("12345", 100.25m, details));
		}
		[Fact]
		public void RejectsMismatchingIdentifier() {
			Assert.False(provider.VerifyResponse("111111", 100.25m, details));
		}
		[Fact]
		public void RejectsMismatchingStatus() {
			details["status"] = "DECLINED";
			details["MAC"] = Hash(this.details);
			Assert.False(provider.VerifyResponse("12345", 100.25m, details));
			details["status"] = "CANCELLED";
			details["MAC"] = Hash(this.details);
			Assert.False(provider.VerifyResponse("12345", 100.25m, details));
			details["status"] = "PENDING";
			details["MAC"] = Hash(this.details);
			Assert.False(provider.VerifyResponse("12345", 100.25m, details));
			details["status"] = "GARBAGE";
			details["MAC"] = Hash(this.details);
			Assert.False(provider.VerifyResponse("12345", 100.25m, details));
		}
		[Fact]
		public void RejectsMismatchingAmount() {
			Assert.False(provider.VerifyResponse("12345", 80.70m, details));
		}
		[Fact]
		public void RejectsMismatchingCurrency() {
			details["currency"] = "XXX";
			details["MAC"] = Hash(details);
			Assert.False(provider.VerifyResponse("12345", 100.25m, details));
		}

		string Hash(NameValueCollection items) { 
			var sortedPairs = items.Keys
				.Cast<string>()
				.Where(t => t!="MAC")
				.OrderBy(t => t, StringComparer.Ordinal)
				.Select(t => t+"="+items[t]);
			return String.Join("&", sortedPairs).HMAC("HMACSHA256", secret);
		}
	}
}
