using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mios.Payment.Verifiers {
	public class CrosskeyVerificationProvider : IVerificationProvider {

		public string Account { get; set; }
		public string Secret { get; set; }
		public Uri EndpointUrl { get; set; }
		public string Currency { get; set; }

		public async Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount, CancellationToken cancellationToken = default(CancellationToken)) {
			if(EndpointUrl==null)
				throw new InvalidOperationException("EndpointUrl property must be set before verifying payments.");
			if(Account==null)
				throw new InvalidOperationException("Account property must be set before verifying payments.");
			if(Secret==null)
				throw new InvalidOperationException("Secret propery must be set before verifying payments.");

			var data = new Dictionary<string, string> {
				{"CBS_VERSION", "0001"},
				{"CBS_TIMESTMP", DateTime.Now.ToString("yyyyMMddhhmmss0001", CultureInfo.InvariantCulture)},
				{"CBS_RCV_ID", Account},
				{"CBS_LANGUAGE", "1"},
				{"CBS_RESPTYPE", "xml"},
				{"CBS_RESPDATA", "text/xml"},
				{"CBS_STAMP", identifier },
				{"CBS_REF", ReferenceCalculator.GenerateReferenceNumber(identifier) },
				{"CBS_AMOUNT", expectedAmount.GetValueOrDefault().ToString("f2", CultureInfo.GetCultureInfo("fi-FI")) },
				{"CBS_CUR", Currency},
				{"CBS_KEYVERS", "0001"},
				{"CBS_ALG", "01"},
			};
			data["CBS_MAC"] = 
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&{8}&{9}&",
					data["CBS_VERSION"],
					data["CBS_TIMESTMP"],
					data["CBS_RCV_ID"],
					data["CBS_LANGUAGE"],
					data["CBS_RESPTYPE"],
					data["CBS_RESPDATA"],
					data["CBS_STAMP"],
					data["CBS_REF"],
					data["CBS_ALG"],
					Secret
				)
				.Hash("MD5");

			// Make request
			var client = new HttpClient();
			var response = await client.PostAsync(EndpointUrl, new FormUrlEncodedContent(data), cancellationToken);
			response.EnsureSuccessStatusCode();
			cancellationToken.ThrowIfCancellationRequested();
			var responseContent = await response.Content.ReadAsStringAsync();
			if(responseContent.Contains("<CBS_RESPCODE>OK</CBS_RESPCODE>")) {
				return true;
			}
			if(responseContent.Contains("<CBS_RESPCODE>Notfound</CBS_RESPCODE>")) {
				return false;
			}
			throw new VerificationProviderException("Crosskey verification service returned unknown response.") {
				Data = {
					{"Response", responseContent} 
				}
			};
		}
	}
}
