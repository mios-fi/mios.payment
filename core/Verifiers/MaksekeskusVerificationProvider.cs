using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
#if NET45||NET46||NET47 
using System.Net.Cache;
using System.Runtime.Caching;
#endif
#if NETCOREAPP2_0
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
#endif
namespace Mios.Payment.Verifiers
{
    public class MaksekeskusVerificationProvider : IVerificationProvider {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly string cacheKey;
        readonly SemaphoreSlim cacheLock = new SemaphoreSlim(1);
        public Uri EndpointUri { get; set; }
        public string Currency { get; set; }
        public string Account { get; set; }
        public string Secret { get; set; }
        public TimeSpan CacheDuration { get; set; }
        public TimeSpan Horizon { get; set; }

#if NETCOREAPP2_0
        readonly IMemoryCache cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
#endif

        public MaksekeskusVerificationProvider() {
            cacheKey = typeof(MaksekeskusVerificationProvider).Name + "_" + GetHashCode().ToString() + ".Transactions";
            EndpointUri = new Uri("https://api.maksekeskus.ee/v1/");
            Currency = "EUR";
            CacheDuration = TimeSpan.FromMinutes(10);
            Horizon = TimeSpan.FromDays(-5);
        }
        public async Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount, CancellationToken cancellationToken = default(CancellationToken)) {
            if (Account == null)
                throw new InvalidOperationException("ShopId property must be set before verifying payments.");
            if (Secret == null)
                throw new InvalidOperationException("SecretKey property must be set before verifying payments.");

            var allTransactions = await GetCachedTransactions(cancellationToken);
            IList<Transaction> transactions = null;
            if (!allTransactions.TryGetValue(identifier, out transactions)) {
                Log.Debug("No payments for reference {0} found in transaction list.", identifier);
                return false;
            }
            foreach (var transaction in transactions) {
                if ("COMPLETED".Equals(transaction.Status, StringComparison.OrdinalIgnoreCase) == false) {
                    Log.Debug("Expected status COMPLETED but found {0} in transaction {1} on reference {2}.", transaction.Status, transaction.Id, identifier);
                    continue;
                }
                if (!Currency.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase)) {
                    Log.Debug("Expected currency {0} but found {1} in transaction {2} on reference {3}.", Currency, transaction.Currency, transaction.Id, identifier);
                    continue;
                }
                if (expectedAmount.HasValue && transaction.Amount != expectedAmount.Value) {
                    Log.Debug("Expected amount {0} but found {1} paid in transaction {2} on reference {3}.", expectedAmount, transaction.Amount, transaction.Id, identifier);
                    continue;
                }
                Log.Info("Found matching transaction {0} for reference {1}.", transaction.Id, identifier);
                return true;
            }
            Log.Debug("Unable to find matching transaction for reference {0}.", identifier);
            return false;
        }

        private async Task<IDictionary<string, IList<Transaction>>> GetCachedTransactions(CancellationToken cancellationToken) {
            var transactions = GetTransactionsFromCache();
            if (transactions != null) {
                Log.Trace("Retrieved {0} transactions from cache for shop {1}.", transactions.Count, Account);
                return transactions;
            }

            await cacheLock.WaitAsync();
            try {
                // Try the cache again, maybe someone filled it while we were waiting for the lock
                transactions = GetTransactionsFromCache();
                if (transactions != null)
                    return transactions;

                // Download transaction list
                transactions = await GetTransactions(cancellationToken);
#if NET46
                MemoryCache.Default.Add(cacheKey, transactions, new CacheItemPolicy {
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(20)
                });
#endif
#if NETCOREAPP2_0
                cache.Set(cacheKey, transactions, absoluteExpiration: DateTimeOffset.Now.AddMinutes(20));
#endif
            } finally {
                cacheLock.Release();
            }

            return transactions;
        }

        private IDictionary<string, IList<Transaction>> GetTransactionsFromCache() {
#if NET45||NET46||NET47 
            return MemoryCache.Default.Get(cacheKey) as IDictionary<string,IList<Transaction>>;
#endif
#if NETCOREAPP2_0
            return cache.Get(cacheKey) as IDictionary<string, IList<Transaction>>;
#endif
        }

		private async Task<IDictionary<string,IList<Transaction>>> GetTransactions(CancellationToken cancellationToken) {
			var transactions = new Dictionary<string, IList<Transaction>>();
			var today = DateTimeOffset.Now.Date.Add(Horizon);
#if NET45||NET46||NET47 
			var client = new HttpClient(new WebRequestHandler {
				CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.CacheIfAvailable)
			});
#endif
#if NETCOREAPP2_0
            var client = new HttpClient();
#endif
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
				"Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(Account+":"+Secret))
			);
			var since = DateTimeOffset.Now.Add(Horizon);
			var until = DateTimeOffset.Now.AddDays(1);
			var requestUri = new Uri(EndpointUri, "transactions?since="+Uri.EscapeDataString(since.ToString("yyyy-MM-dd"))+"&until="+Uri.EscapeDataString(until.ToString("yyyy-MM-dd")));
			try {
				Log.Info("Requesting fresh transactions starting from {0} until {1} from Maksekeskus for shop {2}.", since, until, Account);
				while(true) {
					var response = await client.GetAsync(requestUri);
					response.EnsureSuccessStatusCode();
					cancellationToken.ThrowIfCancellationRequested();

					// Parse response
					var responseString = await response.Content.ReadAsStringAsync();
					var transactionList = JsonConvert.DeserializeObject<IList<Transaction>>(responseString);
					Log.Info("Retrieved {0} transactions from Makeskeskus for shop {1}, first on {2}, last on {3}.",
						transactionList.Count,
						Account,
						transactionList.Min(t => t.Created_At),
						transactionList.Max(t => t.Created_At)
					);
					IList<Transaction> transactionsForReference;
					foreach(var transaction in transactionList) {
						// If the existing transaction is 
						if(transactions.TryGetValue(transaction.Reference, out transactionsForReference)) {
							transactionsForReference.Add(transaction);
						} else {
							transactions[transaction.Reference] = new List<Transaction> { transaction };
						}
					}

					requestUri = NextPageUriFromHeaders(response.Headers);
					if(requestUri==null) break;
				}
			} catch(HttpRequestException e) {
				Log.Error(e, "Exception while requesting transactions from Maksekeskus for shop "+Account+".");
			}
			return transactions;
		}

		private Uri NextPageUriFromHeaders(HttpResponseHeaders headers) {
			var linkHeader = headers
				.Where(t => "Link".Equals(t.Key, StringComparison.OrdinalIgnoreCase))
				.SelectMany(t => t.Value)
				.FirstOrDefault();
			if(linkHeader==null) return null;
			var match = NextLinkPattern.Match(linkHeader);
			if(!match.Success) return null;
			return new Uri(match.Groups[1].Value);
		}

		private Regex NextLinkPattern = new Regex(@"<([^>]+)>;\s+rel=""next""");

		class Transaction {
			public string Id { get; set; }
			public DateTime Created_At { get; set; }
			public DateTime Completed_At { get; set; }
			public string Status { get; set; }
			public decimal Amount { get; set; }
			public string Currency { get; set; }
			public string Reference { get; set; }
		}
	}
}
