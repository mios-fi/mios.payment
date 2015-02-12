using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Mios.Payment.Verifiers {
	public class DanskeBankVerificationProvider : IVerificationProvider {
		public string Account { get; set; }
		public string Secret { get; set; }
		public string Contract { get; set; }
		public string Currency { get; set; }
		public Uri EndpointUrl { get; set; }

		public DanskeBankVerificationProvider() {
			EndpointUrl = new Uri("https://netbank.danskebank.dk/HB");
			Currency = "EUR";
		}
		public DanskeBankVerificationProvider(string data) : this() {
			var parameters = HttpUtility.ParseQueryString(data);
			Account = parameters["account"];
			Secret = parameters["secret"];
			Contract = parameters["contract"];
			Currency = Currency ?? parameters["currency"];
			Uri endpointUrl;
			if(Uri.TryCreate(parameters["endpointUrl"], UriKind.Absolute, out endpointUrl)) {
				EndpointUrl = endpointUrl;
			}
		}

		public async Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount, CancellationToken cancellationToken = default(CancellationToken)) {
			if(String.IsNullOrEmpty(Account)) {
				throw new InvalidOperationException("The Account property must be set before calling VerifyPaymentAsync.");
			}
			if(String.IsNullOrEmpty(Secret)) {
				throw new InvalidOperationException("The Secret property must be set before calling VerifyPaymentAsync.");
			}
			if(String.IsNullOrEmpty(Contract)) {
				throw new InvalidOperationException("The Contract property must be set before calling VerifyPaymentAsync.");
			}

			var data = new Dictionary<string, string> {
				{"Refno",      ReferenceCalculator.GenerateReferenceNumber(identifier)},
				{"MerchantID", Account},
				{"gsAftlnr",   Contract},
				{"gsSprog",    "EN"},
				{"gsProdukt",  "IBV"},
				{"gsNextObj",  "InetPayV"},
				{"gsNextAkt",  "InetPaySt"},
				{"gsResp",     "S"},
				{"Version",    "0001"},
				{"algorithm",  "03"}
			};

			var hashStr = String.Format("{0}&{1}&{2}&", Secret, Account, data["Refno"]);
			data["VerifyCode"] = hashStr
				.Hash("SHA256")
				.ToLowerInvariant();

			// Make request
			var client = new HttpClient();
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data), cancellationToken);
			response.EnsureSuccessStatusCode();
			cancellationToken.ThrowIfCancellationRequested();
			var responseContent = await response.Content.ReadAsStringAsync();

			// Parse response as querystring
			var responseFields = HttpUtility.ParseQueryString(responseContent);

			// Throw for unexpected return codes
			if(responseFields["ReturnCode"]!="001" && responseFields["ReturnCode"]!="000") {
				throw new VerificationProviderException("Unexpected ReturnCode "+responseFields["ReturnCode"]+" in response.") {
					ResponseContent = responseContent
				};
			}

			// Return false for unverified payments
			if(responseFields["ReturnCode"]=="001") { 
				return false;
			}

			// Throw for unexpected currency in response
			if(responseFields["Currency"]!=Currency) {
				throw new VerificationProviderException("Expected currency "+Currency+" but found "+responseFields["Currency"]+" in response.") {
					ResponseContent = responseContent
				};
			}

			// Throw for unexpected amount in response
			if(expectedAmount.HasValue && decimal.Parse(responseFields["Amount"], CultureInfo.InvariantCulture) != expectedAmount.Value) { 
				throw new VerificationProviderException("Expected amount "+expectedAmount.Value.ToString("f2", CultureInfo.GetCultureInfo("fi-FI"))+" but found "+responseFields["Amount"]) {
					ResponseContent = responseContent
				};
			}

			return true;
		}
	}
}
