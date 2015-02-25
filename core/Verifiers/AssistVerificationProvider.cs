using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Mios.Payment.Verifiers {
	public class AssistVerificationProvider : IVerificationProvider {
		public string Account { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
		public Uri EndpointUrl { get; set; }

		public AssistVerificationProvider() {
			EndpointUrl = new Uri("https://payments.paysecure.ru/results/results.cfm");
		}
		public AssistVerificationProvider(string data) : this() {
			var parameters = HttpUtility.ParseQueryString(data);
			Account = parameters["account"];
			User = parameters["user"];
			Password = parameters["password"];
			Uri endpointUrl;
			if(Uri.TryCreate(parameters["endpointUrl"], UriKind.Absolute, out endpointUrl)) {
				EndpointUrl = endpointUrl;
			}
		}
		public async Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount, System.Threading.CancellationToken cancellationToken = default(CancellationToken)) {
			if(String.IsNullOrEmpty(Account)) {
				throw new InvalidOperationException("The Account property must be set before calling VerifyPaymentAsync");
			}
			if(String.IsNullOrEmpty(User)) {
				throw new InvalidOperationException("The User property must be set before calling VerifyPaymentAsync");
			}
			if(String.IsNullOrEmpty(Password)) {
				throw new InvalidOperationException("The Password property must be set before calling VerifyPaymentAsync");
			}

			var rangeEnd = DateTime.Now.AddDays(1);
			var rangeStart = rangeEnd.AddDays(-7);
			
			var data = new Dictionary<string, string> {
				{"ShopOrderNumber", identifier },// ReferenceCalculator.GenerateReferenceNumber(identifier)},
				{"Shop_Id", Account},
				{"Login", User},
				{"Password", Password},
				{"Format", "1"},
				{"English", "1"},
				{"StartYear", rangeStart.Year.ToString() },
				{"StartMonth", rangeStart.Month.ToString() },
				{"StartDay", rangeStart.Day.ToString() },
				{"StartHour", "0"},
				{"StartMin", "0"},
				{"EndYear", rangeEnd.Year.ToString() },
				{"EndMonth", rangeEnd.Month.ToString() },
				{"EndDay", rangeEnd.Day.ToString() },
				{"EndHour", "0"},
				{"EndMin", "0"}
			};

			// Make request
			var client = new HttpClient();
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data), cancellationToken);
			response.EnsureSuccessStatusCode();
			cancellationToken.ThrowIfCancellationRequested();
			var responseContent = await response.Content.ReadAsStringAsync();

			// Match successful payment
			if(responseContent.Contains(identifier+";AS000;SUCCESSFUL")) {
				return true;
			}
			// Match payment outside date interval
			if(responseContent.Contains("ERROR:")) {
				return false;
			}
			// Match various error codes for unsuccessful payments
			if(Regex.IsMatch(responseContent, Regex.Escape(identifier)+@";AS[1234]\d\d")) {
				return false;
			}
			throw new VerificationProviderException("Assist verification service returned unknown response.") {
				ResponseContent = responseContent
			};
		}
	}
}
