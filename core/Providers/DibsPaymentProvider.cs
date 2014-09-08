using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NLog;

namespace Mios.Payment.Providers {
	public class DibsPaymentProvider : IPaymentProvider {
		static readonly Logger log = LogManager.GetCurrentClassLogger();
		
		public string MerchantId { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public string Currency { get; set; }
		public string PaymentTypes { get; set; }
		public bool TestMode { get; set; }

		public DibsPaymentProvider() {
			Url = "https://sat1.dibspayment.com/dibspaymentwindow/entrypoint";
			Currency = "EUR";
		}
		public DibsPaymentProvider(string parameterString) : this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing 'secret' parameter in initialization string.");
			}
			MerchantId = parameters["account"];
			Secret = parameters["secret"];
			PaymentTypes = parameters["paymentTypes"];
			Url = parameters["url"] ?? Url;
			Currency = parameters["currency"] ?? Currency;
			TestMode = "true".Equals(parameters["test"]);
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var details = new PaymentDetails {
				Url = Url,
				Fields = new NameValueCollection(StringComparer.Ordinal) {
					{"orderId", identifier},
					{"merchant", MerchantId},
					{"amount", (amount*100).ToString("F0", CultureInfo.InvariantCulture)},
					{"currency", Currency},
					{"payType", PaymentTypes},
					{"acceptReturnUrl", returnUrl},
					{"cancelReturnUrl", errorUrl},
					{"language", CultureInfo.CurrentCulture.Name}
				}
			};
			if(TestMode) { 
				details.Fields["test"] = "1";
			}
			details.Fields["MAC"] = ComputeHash(details.Fields);
			return details;				
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var expected = ComputeHash(fields);
			if(String.IsNullOrEmpty(fields["MAC"])) 
				return false;
			if(!expected.Equals(fields["MAC"], StringComparison.OrdinalIgnoreCase)) {
				log.Warn(
					"Hash check failed when verifying response, expected {0}... found {1}...",
					expected.Substring(0, 10),
					fields["MAC"].Substring(0, 10)
				);
				return false;
			}
			decimal paidAmount;
			if(String.IsNullOrEmpty(fields["amount"]) || !decimal.TryParse(fields["amount"], out paidAmount) || amount != paidAmount/100m) { 
				log.Warn("Invalid amount paid, expected {0} found {1}", amount, fields["amount"]);
				return false;
			}
			if(!"ACCEPTED".Equals(fields["status"])) {
				log.Warn("Expected status ACCEPTED in response, but received {0}", fields["status"]);
				return false;
			}
			if(!identifier.Equals(fields["orderId"])) {
				log.Warn("Expected identifier {0} in response, but received {1}", identifier, fields["orderId"]);
				return false;
			}
			if(!Currency.Equals(fields["currency"])) {
				log.Warn("Expected currency {0} in response, but received {1}", Currency, fields["currency"]);
				return false;
			}
			return true;
		}

		private string ComputeHash(NameValueCollection fields) {
			var sortedFields = fields.Keys
				.Cast<string>()
				.Where(t => t!="MAC")
				.OrderBy(t => t, StringComparer.Ordinal)
				.Select(t => t+"="+fields[t]);
			log.Trace("Hashing {0}", String.Join("&", sortedFields));
			return String.Join("&", sortedFields).HMAC("HMACSHA256", Secret);
		}
	}
}
