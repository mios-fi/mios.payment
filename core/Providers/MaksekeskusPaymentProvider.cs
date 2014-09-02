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

		public MaksekeskusPaymentProvider() {
			Url = "https://payment.maksekeskus.ee/pay/1/signed.html";
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
			var details = new Dictionary<string, string> {
				{"shopId", Account},
				{"paymentId", identifier},
				{"amount", amount.ToString("F2", CultureInfo.InvariantCulture)},
			};
			details["signature"] = (details["shopId"]+details["paymentId"]+details["amount"]+Secret).Hash("SHA512").ToUpperInvariant();
			return new PaymentDetails {
				Url = Url, Fields = new NameValueCollection {
					{"json", JsonConvert.SerializeObject(details)},
					{"locale", CultureInfo.CurrentCulture.TwoLetterISOLanguageName }
				}
			};
		}

		public bool VerifyResponse(string identifier, decimal amount, System.Collections.Specialized.NameValueCollection fields) {
			var json = fields["json"];
			if(String.IsNullOrWhiteSpace(json)) 
				return false;
			var returnModel = JsonConvert.DeserializeObject<ReturnModel>(json);

			var expectedHash = (returnModel.paymentId+returnModel.amount.ToString("f2", CultureInfo.InvariantCulture)+returnModel.status+Secret).Hash("SHA512").ToUpperInvariant();
			if(!expectedHash.Equals(returnModel.signature)) {
				Log.Warn("Unable to verify payment, expected signature {0} but found {1}, computed from {2}+{3}+{4}+SECRET", expectedHash, returnModel.signature, returnModel.paymentId, returnModel.amount, returnModel.status);
				return false;
			}
			if(returnModel.paymentId!=identifier) {
				Log.Warn("Unable to verify payment, expected identifier {0} but received {1}", identifier, returnModel.paymentId);
				return false;
			}
			if(returnModel.amount!=amount) {
				Log.Warn("Unable to verify payment, expected amount {0} but received {1}", amount, returnModel.amount);
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
			public decimal amount { get; set; }
			public string status { get; set; }
			public string signature { get; set; }
		}
	}
}
