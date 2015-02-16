using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Mios.Payment.Verifiers {
	public class OsuuspankkiVerificationProvider : IVerificationProvider {

		public string Account { get; set; }
		public string Secret { get; set; }
		public Uri EndpointUrl { get; set; }

		public OsuuspankkiVerificationProvider() {
			EndpointUrl = new Uri("https://kultaraha.op.fi/cgi-bin/krcgi");
		}
		public OsuuspankkiVerificationProvider(string data) : this() {
			var parameters = HttpUtility.ParseQueryString(data);
			Account = parameters["account"];
			Secret = parameters["secret"];
			Uri endpointUrl;
			if(Uri.TryCreate(parameters["endpointUrl"], UriKind.Absolute, out endpointUrl)) {
				EndpointUrl = endpointUrl;
			}
		}

		public async Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount, CancellationToken cancellationToken = default(CancellationToken)) {
			if(String.IsNullOrEmpty(Account)) {
				throw new InvalidOperationException("The Account property must be set before calling VerifyPaymentAsync");
			}
			if(String.IsNullOrEmpty(Secret)) {
				throw new InvalidOperationException("The Secret property must be set before calling VerifyPaymentAsync");
			}

			var data = new Dictionary<string,string>() {
				{"action_id", "708"},
				{"VERSIO", "0006"},
				{"MYYJA", Account},
				{"KYSELYTUNNUS", "0"},
				{"MAKSUTUNNUS", identifier},
				{"VIITE", ReferenceCalculator.GenerateReferenceNumber(identifier)},
				{"TARKISTE-VERSIO", "6"},
				{"PALUU-LINKKI", "http://www.dermoshop.com"}
			};
			data["TARKISTE"] = 
				String.Join("", 
					data["VERSIO"], 
					data["MYYJA"], 
					data["KYSELYTUNNUS"], 
					data["MAKSUTUNNUS"], 
					data["VIITE"],
					data["TARKISTE-VERSIO"],
					Secret
				)
				.Hash("MD5");

			// Make request
			var client = new HttpClient();
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data), cancellationToken);
			response.EnsureSuccessStatusCode();
			cancellationToken.ThrowIfCancellationRequested();
			var responseContent = await response.Content.ReadAsStringAsync();

			if(responseContent.Contains("Maksua ei ole maksettu")) {
				return false;
			} 
			if(responseContent.Contains("Maksu on maksettu")) {
				return true;
			}
			throw new VerificationProviderException("OP verification service returned unknown response.") {
				ResponseContent = responseContent
			};
		}
	}
}

