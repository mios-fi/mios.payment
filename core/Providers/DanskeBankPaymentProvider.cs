﻿using System;
using System.Collections.Specialized;
using System.Globalization;

using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	public class DanskeBankPaymentProvider : IPaymentProvider {
		static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string Account { get; set; }
		public string Secret { get; set; }
		public string Url { get; set; }
		public string Currency { get; set; }
		public DanskeBankPaymentProvider() {
			Url = "https://verkkopankki.danskebank.fi/SP/vemaha/VemahaApp";
		}
		public DanskeBankPaymentProvider(string parameterString)
			: this() {
			var parameters = HttpUtility.ParseQueryString(parameterString);
			if(parameters["account"] == null) {
				throw new ArgumentException("Missing required 'account' parameter in initialization string.");
			}
			if(parameters["secret"] == null) {
				throw new ArgumentException("Missing required 'secret' parameter in initialization string.");
			}
			if(parameters["currency"] == null) {
				throw new ArgumentException("Missing required 'currency' parameter in initialization string.");
			}
			Account = parameters["account"];
			Secret = parameters["secret"];
			Currency = parameters["currency"];
			Url = parameters["url"] ?? Url;
		}
		public PaymentDetails GenerateDetails(string identifier, decimal amount, string returnUrl, string errorUrl, string message) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			var formattedAmount = amount.ToString("N2", CultureInfo.CreateSpecificCulture("fi-fi"));
			var details = new PaymentDetails {
				Url = Url,
				Fields = new NameValueCollection(StringComparer.Ordinal) {
					{ "KNRO",     Account },
					{ "SUMMA",    formattedAmount },
					{ "VIITE",    referenceNumber },
					{ "VALUUTTA", Currency },
					{ "VERSIO",   "4" },
					{ "ALG",      "03" },
					{ "ERAPAIVA", DateTime.Now.ToString("dd.MM.yyyy") },
					{ "OKURL",    returnUrl },
					{ "VIRHEURL", errorUrl }
				}
			};
			details.Fields["TARKISTE"] =
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&{8}&",
					Secret,
					details.Fields["SUMMA"],
					details.Fields["VIITE"],
					details.Fields["KNRO"],
					details.Fields["VERSIO"],
					details.Fields["VALUUTTA"],
					details.Fields["OKURL"],
					details.Fields["VIRHEURL"],
					details.Fields["ERAPAIVA"]).Hash("SHA256").ToUpperInvariant();
			return details;
		}

		public bool VerifyResponse(string identifier, decimal amount, NameValueCollection fields) {
			var referenceNumber = ReferenceCalculator.GenerateReferenceNumber(identifier);
			if(!referenceNumber.Equals(fields["VIITE"])) {
				log.Error("Reference number comparison failed when verifying response from Danske Bank, expected {0} found {1}",
					referenceNumber, fields["VIITE"]);
				return false;
			}
			var expected =
				String.Format("{0}&{1}&{2}&{3}&{4}&{5}&{6}&{7}&",
					Secret,
					fields["VIITE"],
					fields["SUMMA"],
					fields["STATUS"],
					fields["KNRO"],
					fields["VERSIO"],
					fields["VALUUTTA"],
					fields["ERAPAIVA"]).Hash("SHA256").ToUpperInvariant();
			if(expected.Equals(fields["TARKISTE"])) {
				return true;
			}
			log.Error(
				"Hash check failed when verifying response from Danske Bank, expected {0} found {1}, value computed from {2}{3}{4}{5}{6}{7}{8}{9}",
				expected,
				fields["TARKISTE"],
				"SECRET",
				fields["VIITE"],
				fields["SUMMA"],
				fields["STATUS"],
				fields["KNRO"],
				fields["VERSIO"],
				fields["VALUUTTA"],
				fields["ERAPAIVA"]
				);
			return false;
		}
	}
}
