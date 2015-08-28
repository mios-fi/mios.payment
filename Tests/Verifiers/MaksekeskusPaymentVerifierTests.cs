using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mios.Payment.Providers;
using Mios.Payment;
using Xunit;
using Newtonsoft.Json;

namespace Tests.Providers {
	public class MaksekeskusPaymentVerifierTests {
		private MaksekeskusPaymentProvider provider;
		private MaksekeskusPaymentProvider.ReturnModel details;
		public MaksekeskusPaymentVerifierTests() {
			provider = new MaksekeskusPaymentProvider("account=xyz&secret=1234567890");
			details = new MaksekeskusPaymentProvider.ReturnModel {
				shopId = "xyz", paymentId = "123456", amount = "12.25", status = "PAID",
				signature = ("123456"+"12.25"+"PAID"+"1234567890").Hash("SHA512").ToUpperInvariant()
			};
		}
		[Fact]
		public void AcceptsValidPayment() {
			Assert.True(provider.VerifyResponse("123456", 12.25m, new NameValueCollection {
				{ "json", JsonConvert.SerializeObject(details) }
			}));
		}
		[Fact]
		public void RejectsMissingJson() {
			var provider = new MaksekeskusPaymentProvider();
			Assert.False(provider.VerifyResponse("", 0m, new NameValueCollection()));
			Assert.False(provider.VerifyResponse("", 0m, new NameValueCollection { { "json", "" } }));
		}
		[Fact]
		public void RejectsMismatchingIdentifier() {
			var provider = new MaksekeskusPaymentProvider();
			Assert.False(provider.VerifyResponse("111111", 12.25m, new NameValueCollection {
				{ "json", JsonConvert.SerializeObject(details) }
			}));
		}
		[Fact]
		public void RejectsMismatchingStatus() {
			details.status = "XXXXX";
			Assert.False(provider.VerifyResponse("123456", 12.25m, new NameValueCollection {
				{ "json", JsonConvert.SerializeObject(details) }
			}));
		}
		[Fact]
		public void RejectsMismatchingAmount() {
			details.status = "XXXXX";
			Assert.False(provider.VerifyResponse("123456", 100.25m, new NameValueCollection {
				{ "json", JsonConvert.SerializeObject(details) }
			}));
		}
	}
}
