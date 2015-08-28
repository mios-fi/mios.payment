using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using NLog;

namespace Mios.Payment.Providers {
	public class MaksekeskusPaymentProvider : IPaymentProvider {
		private static Logger Log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public string Country { get; set; }
		public string Locale { get; set; }

		public MaksekeskusPaymentProvider() {
			Url = "https://payment.maksekeskus.ee/pay/1/signed.html";
			Country = "ee";
			Locale = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
		}
		public MaksekeskusPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing required 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing required 'secret' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
			Url = parameters["url"] ?? Url;
		}

		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var json = JsonConvert.SerializeObject(new {
				shop = Account,
				amount = amount.ToString("F2", CultureInfo.InvariantCulture),
				reference = identifier,
				locale = Locale,
				country = Country
			});
			return new PaymentDetails {
				Url = Url, Fields = new NameValueCollection {
					{"json", json },
					{"mac", (json+Secret).Hash("SHA512").ToUpperInvariant() }
				}
			};
		}

		public bool VerifyResponse(string identifier, decimal amount, System.Collections.Specialized.NameValueCollection fields) {
			var json = fields["json"];
			if(String.IsNullOrWhiteSpace(json)) 
				return false;
			var returnModel = JsonConvert.DeserializeObject<ReturnModel>(json);

			var expectedHash = (returnModel.paymentId+returnModel.amount+returnModel.status+Secret).Hash("SHA512").ToUpperInvariant();
			if(!expectedHash.Equals(returnModel.signature)) {
				Log.Warn("Unable to verify payment, expected signature {0} but found {1}, computed from {2}+{3}+{4}+SECRET", expectedHash, returnModel.signature, returnModel.paymentId, returnModel.amount, returnModel.status);
				return false;
			}
			if(returnModel.paymentId!=identifier) {
				Log.Warn("Unable to verify payment, expected identifier {0} but received {1}", identifier, returnModel.paymentId);
				return false;
			}
			decimal parsedAmount;
			if(returnModel.amount==null || !decimal.TryParse(returnModel.amount, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedAmount)) {
				Log.Warn("Missing or invalid amount");
				return false;
			}
			if(parsedAmount!=amount) {
				Log.Warn("Unable to verify payment, expected amount {0} but received '{1}'", amount, parsedAmount);
				return false;
			}
			if(!returnModel.status.Equals("PAID", StringComparison.OrdinalIgnoreCase)) {
				Log.Warn("Unable to verify payment, expected status {0} but received {1}", "PAID", returnModel.status);
				return false; 
			}
			return true;
		}

		public class ReturnModel {
			public string shopId { get; set; }
			public string paymentId { get; set; }
			public string amount { get; set; }
			public string status { get; set; }
			public string signature { get; set; }
		}
	}
}
