using System.Collections.Specialized;

namespace Mios.Payment {
  public struct PaymentDetails {
    public string Url { get; set; }
    public NameValueCollection Fields { get; set; }
  }
}