﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using NLog;

namespace Mios.Payment.Verifiers {
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

		public MaksekeskusVerificationProvider() {
			cacheKey = typeof(MaksekeskusVerificationProvider).Name+"_"+GetHashCode().ToString()+".Transactions";
			EndpointUri = new Uri("https://api.maksekeskus.ee/v1/");
			Currency = "EUR";
			CacheDuration = TimeSpan.FromMinutes(10);
			Horizon = TimeSpan.FromDays(-5);
		}
		public async Task<bool> VerifyPaymentAsync(string identifier, decimal? expectedAmount, CancellationToken cancellationToken = default(CancellationToken)) {
			if(Account==null)
				throw new InvalidOperationException("ShopId property must be set before verifying payments.");
			if(Secret==null)
				throw new InvalidOperationException("SecretKey property must be set before verifying payments.");

			var transactions = await GetCachedTransactions(cancellationToken);
			Transaction transaction = null;
			if(!transactions.TryGetValue(identifier, out transaction)) {
				Log.Debug("Transaction {0} not found in transaction list.", identifier);
				return false;
			}
			if("COMPLETED".Equals(transaction.Status, StringComparison.OrdinalIgnoreCase)==false) {
				Log.Debug("Expected status COMPLETED but found {0} on transaction {1}.", transaction.Status, identifier);
				return false;
			}
			if(!Currency.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase)) {
				Log.Debug("Expected currency {0} but found {1} on transaction {2}.", Currency, transaction.Currency, identifier);
				return false;
			}
			if(expectedAmount.HasValue && transaction.Amount!=expectedAmount.Value) {
				Log.Debug("Expected amount {0} but found {1} paid in transaction {2}.", expectedAmount, transaction.Amount, identifier);
				return false;
			}
			return true;
		}

		private async Task<IDictionary<string, Transaction>> GetCachedTransactions(CancellationToken cancellationToken) {
			var transactions = MemoryCache.Default.Get(cacheKey) as IDictionary<string,Transaction>;
			if(transactions!=null) {
				Log.Trace("Retrieved {0} transactions from cache for shop {1}.", transactions.Count, Account);
				return transactions;
			}

			await cacheLock.WaitAsync();
			try {
				// Try the cache again, maybe someone filled it while we were waiting for the lock
				transactions = MemoryCache.Default.Get(cacheKey) as IDictionary<string, Transaction>;
				if(transactions!=null)
					return transactions;

				// Download transaction list
				transactions = await GetTransactions(cancellationToken);
				MemoryCache.Default.Add(cacheKey, transactions, new CacheItemPolicy {
					AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(20)
				});
			} finally {
				cacheLock.Release();
			}

			return transactions;
		}

		private async Task<IDictionary<string,Transaction>> GetTransactions(CancellationToken cancellationToken) {
			var transactions = new Dictionary<string, Transaction>();
			var today = DateTimeOffset.Now.Date.Add(Horizon);
			var client = new HttpClient(new WebRequestHandler {
				CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.CacheIfAvailable)
			});
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
					foreach(var transaction in transactionList) {
						transactions[transaction.Reference] = transaction;
					}

					requestUri = NextPageUriFromHeaders(response.Headers);
					if(requestUri==null) break;
				}
			} catch(HttpRequestException e) {
				Log.ErrorException("Exception while requesting transactions from Maksekeskus for shop "+Account+".", e);
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
			public DateTime Created_At { get; set; }
			public DateTime Completed_At { get; set; }
			public string Status { get; set; }
			public decimal Amount { get; set; }
			public string Currency { get; set; }
			public string Reference { get; set; }
		}

	}
}
