namespace Mios.Payment {
  internal static class ReferenceCalculator {
    static readonly int[] ReferenceWeights = { 7, 3, 1 };
    public static string GenerateReferenceNumber(string source) {
      var chk = 0;
      for(var i=0; i<source.Length; i++) {
        chk += (source[source.Length-i-1]-'0')*ReferenceWeights[i%3];
      }
      return source+((10-(chk%10))%10);
    }
  }
}
