using System;
using System.Collections.Specialized;
using System.Globalization;

using NLog;
using System.Web;

namespace Mios.Payment.Providers {
	[Obsolete("Due to the renaming of Sampo Pankki to Danske Bank, the SampoPaymentProvider has been deprecated in favor of the equivalent DanskeBankPaymentProvider")]
	public class SampoPaymentProvider : DanskeBankPaymentProvider {
		public SampoPaymentProvider() : base() { 
		}
		public SampoPaymentProvider(string parameterString) : base(parameterString) { 
		}
	}
}
