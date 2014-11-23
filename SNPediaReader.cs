using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Web;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Personal_Genome_Explorer
{
	class SNPediaReader
	{
		/** The base URI for the SNPedia MediaWiki. */
		private static string baseURI = "http://www.snpedia.com/";

		/** The maximum number of pages to request listing of at a time.  The MediaWiki API limits this to 500. */
		private static int maxPagesRequest = 500;

		/** The HTTP client used to access SNpedia. */
		private static HttpClient cachedHttpClient = null;

		private static HttpClient httpClient
		{
			get
			{
				if(cachedHttpClient == null)
				{
					cachedHttpClient = new HttpClient();
					cachedHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Personal Genome Explorer");
				}
				return cachedHttpClient;
			}
		}

		/** The result of a MediaWiki API call. */
		private class APIResult
		{
			public XmlNode resultXML = null;
			public bool bSuccess = false;
			public string errorCode = "";
			public string errorDescription = "";
		}

		private delegate HttpRequestMessage HttpRequestFactoryDelegate();

		private static async Task<HttpResponseMessage> SendAsyncWithRetry(HttpRequestFactoryDelegate requestFactory, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					// Call SendAsync with a new request object each time, since it's invalid to send the same request object twice.
					var response = await httpClient.SendAsync(requestFactory(), cancellationToken);
					if(response.IsSuccessStatusCode)
					{
						return response;
					}
					else if (response.StatusCode != HttpStatusCode.BadGateway
						&& response.StatusCode != HttpStatusCode.RequestTimeout
						&& response.StatusCode != HttpStatusCode.ServiceUnavailable)
					{
						return response;
					}
				}
				catch (WebException webException)
				{
					if (webException.Status != WebExceptionStatus.ReceiveFailure
					&& webException.Status != WebExceptionStatus.ConnectFailure
					&& webException.Status != WebExceptionStatus.KeepAliveFailure)
					{
						throw webException;
					}
				}
			};
			return null;
		}

		private static async Task<APIResult> CallMediaWikiAPI(string options, CancellationToken cancellationToken)
		{
			// Post the request to the MediaWiki API endpoint.
			var uri = string.Format("{0}api.php", baseURI, options);
			HttpRequestFactoryDelegate requestFactory = () =>
				{
					var request = new HttpRequestMessage(HttpMethod.Post, uri);
					request.Content = new StringContent(options, Encoding.UTF8, "application/x-www-form-urlencoded");
					return request;
				};
			using (var postResponse = await SendAsyncWithRetry(requestFactory, cancellationToken))
			{
				// Read the response.
				if (!cancellationToken.IsCancellationRequested && postResponse.IsSuccessStatusCode)
				{
					var responseString = await postResponse.Content.ReadAsStringAsync();
					var responseDocument = new XmlDocument();
					responseDocument.Load(new StringReader(responseString.TrimStart(' ', '\t', '\r', '\n')));

					// Check for an API error.
					var result = new APIResult();
					var errorElement = responseDocument.SelectSingleNode("/error");
					if (errorElement != null)
					{
						// Extract the API error code and description.
						result.errorCode = errorElement.Attributes["code"].Value;
						result.errorDescription = errorElement.Attributes["info"].Value;
						result.bSuccess = false;
					}
					else
					{
						// If the API call succeeded, extract the resulting XML tree.
						result.resultXML = responseDocument["api"];
						result.bSuccess = true;
					}

					return result;
				}
				else
				{
					// If the HTTP request failed, setup an unsuccesful result struct.
					var result = new APIResult();
					result.resultXML = null;
					result.errorCode = postResponse.StatusCode.ToString();
					result.errorDescription = postResponse.ReasonPhrase;
					result.bSuccess = false;
					return result;
				}
			}
		}

		private static async Task<List<string>> ReadPageListAsync(UpdateProgressDelegate updateProgressDelegate,CancellationToken cancellationToken)
		{
			var pageList = new List<string>();

			// Read the SNPedia page list.
			string firstQueryPage = null;
			do
			{
				var result = await CallMediaWikiAPI(string.Format(
					"action=query&list=allpages&aplimit={0}{1}&format=xml",
					maxPagesRequest,
					firstQueryPage != null ? ("&apcontinue=" + firstQueryPage) : ""
					), cancellationToken);
				if (!cancellationToken.IsCancellationRequested && result.bSuccess)
				{
					firstQueryPage = null;

					// Read the query continue element.
					var continueElement = result.resultXML.SelectSingleNode("query-continue/allpages");
					if (continueElement != null)
					{
						firstQueryPage = continueElement.Attributes["apcontinue"].Value;
					}

					// Read the page element list.
					var pageElements = result.resultXML.SelectNodes("query/allpages/p");
					foreach (XmlNode pageElement in pageElements)
					{
						pageList.Add(pageElement.Attributes["title"].Value);
					}

					// Post a message to the UI thread with a progress update.
					updateProgressDelegate(string.Format("Read {0} entries from SNPedia page list", pageList.Count),0);
				}
				else
				{
					break;
				}
			}
			while (firstQueryPage != null);

			return pageList;
		}

		private static string ParsePageCategorySubstring(string pageText, string category)
		{
			// Parses a category block in the form:  {{ name ... }}
			return Utilities.GetSingleRegexMatch(pageText,new Regex(string.Format("{{{{\\s*{0}([^}}]*)}}}}",category), RegexOptions.IgnoreCase),null);
		}

		private static string ParseCategoryProperty(string categorySubstring, string propertyTag)
		{
			// Parses a category property in the form:  | property = value
			return Utilities.GetSingleRegexMatch(categorySubstring, new Regex(string.Format("\\|[ \t]*{0}[ \t]*=?[ \t]*([^}}\\|\n]*)", propertyTag), RegexOptions.IgnoreCase), "");
		}

		private static List<string> ParseList(string listText)
		{
			// Parse a list in the form:  | item1 | item2 | ...
			var regex = new Regex("\\|[ \t]*([^\\s]+)");
			var regexMatches = regex.Matches(listText);
			var result = new List<string>();
			foreach(Match match in regexMatches)
			{
				result.Add(match.Groups[1].Value);
			}
			return result;
		}

		private static SNPInfo? ParseSNPPage(string pageText)
		{
			// Check if this page is a SNP.
			string snpSubstring = ParsePageCategorySubstring(pageText,"rsnum");
			if(snpSubstring != null)
			{
				// Parse this SNP's properties.
				var result = new SNPInfo();
				result.id = string.Format("rs{0}",ParseCategoryProperty(snpSubstring,"rsid"));
				result.gene = ParseCategoryProperty(snpSubstring,"gene");
				result.chromosome = ParseCategoryProperty(snpSubstring, "chromosome");
				if(!int.TryParse(ParseCategoryProperty(snpSubstring, "position"),out result.position))
				{
					result.position = -1;
				}
				result.orientation = Orientation.Unknown;
				result.updateTime = DateTime.Today;

				// If the page is empty, don't add it to the database.
				result.descriptionWikiText = pageText;
				if(Utilities.ConvertWikiTextToPlainText(pageText).Trim().Length == 0)
				{
					return null;
				}

				// Parse the SNP's genotypes.
				var tempGenotypes = new List<SNPGenotypeInfo>();
				for(int genotypeIndex = 0;;genotypeIndex++)
				{
					string genotypeString = ParseCategoryProperty(snpSubstring, string.Format("geno{0}", genotypeIndex + 1));
					if(genotypeString == "")
					{
						break;
					}
					else
					{
						// SNPedia represents deletion genotypes as a '-', but we use D.
						genotypeString = genotypeString.Replace('-','D');

						// Parse the genotype info.
						var newGenotype = new SNPGenotypeInfo();
						newGenotype.genotype = DNA.StringToDiploidGenotype(genotypeString);
						newGenotype.trait = "";
						newGenotype.populationFrequencies = new Dictionary<string, float>();
						tempGenotypes.Add(newGenotype);

						// If any of the genotypes are deletions or unknown, don't add this snp to the database as we can't correctly parse the inserted genotype yet.
						if(	newGenotype.genotype.a == Genotype.Unknown || newGenotype.genotype.a == Genotype.Deletion ||
							newGenotype.genotype.b == Genotype.Unknown || newGenotype.genotype.b == Genotype.Deletion)
						{
							return null;
						}
					}
				}
				result.genotypes = tempGenotypes.ToArray();

				// Check if the page has SNP population frequency data.
				var populationFrequencySubstring = ParsePageCategorySubstring(pageText,"population diversity");
				if(populationFrequencySubstring != null)
				{
					// Remap the genotype indices to match the genotypes declared above.
					var genotypeIndexRemap = new int?[result.genotypes.Length];
					for(int genotypeIndex = 0;genotypeIndex < result.genotypes.Length;genotypeIndex++)
					{
						genotypeIndexRemap[genotypeIndex] = null;

						var genotype = DNA.StringToDiploidGenotype(ParseCategoryProperty(populationFrequencySubstring, string.Format("geno{0}", genotypeIndex + 1)));
						for(int genotypeInfoIndex = 0;genotypeInfoIndex < result.genotypes.Length;genotypeInfoIndex++)
						{
							if(result.genotypes[genotypeInfoIndex].genotype.Equals(genotype))
							{
								genotypeIndexRemap[genotypeIndex] = genotypeInfoIndex;
								break;
							}
						}
					}

					// Ignore population frequencies if they are redundantly defined or not defined for any genotypes.
					bool bValidPopulationData = true;
					for (int genotypeIndex = 0; genotypeIndex < result.genotypes.Length; genotypeIndex++)
					{
						int numRemaps = 0;
						for(int remapIndex = 0;remapIndex < genotypeIndexRemap.Length;remapIndex++)
						{
							if(genotypeIndexRemap[remapIndex] == genotypeIndex)
							{
								numRemaps++;
							}
						}
						if(numRemaps != 1)
						{
							bValidPopulationData = false;
						}
					}

					if(bValidPopulationData)
					{
						// Parse each population's frequencies for this SNP.
						string[] populations = new string[] { "CEU", "HCB", "JPT", "YRI" };
						foreach(var population in populations)
						{
							var frequencies = ParseList(ParseCategoryProperty(populationFrequencySubstring,population));
							if(frequencies.Count == result.genotypes.Length)
							{
								for (int genotypeIndex = 0; genotypeIndex < genotypeIndexRemap.Length; genotypeIndex++)
								{
									if(genotypeIndexRemap[genotypeIndex] != null)
									{
										float frequency;
										if(float.TryParse(frequencies[genotypeIndex],out frequency))
										{
											result.genotypes[genotypeIndexRemap[genotypeIndex].Value].populationFrequencies.Add(population,frequency / 100.0f);
										}
									}
								}
							}
						}
					}
				}

				return result;
			}
			else
			{
				return null;
			}
		}

		private static void ProcessPage(SNPDatabase database, string pageTitle, string pageText)
		{
			if (pageText != null)
			{
				// Try to parse the page's SNP category properties.
				var snpInfo = ParseSNPPage(pageText);
				if (snpInfo != null)
				{
					// Skip pages with mismatched SNP ID and title.
					if (snpInfo.Value.id == pageTitle.ToLowerInvariant())
					{
						database.snpToInfoMap.Add(snpInfo.Value.id, snpInfo.Value);
					}
				}
				else
				{
					var genotypeCategoryText = ParsePageCategorySubstring(pageText, "Genotype");
					if(genotypeCategoryText != null)
					{
						// Parse the trait summary for a SNP genotype and add it to the previously created SNPInfo.
						var rsid = string.Format("rs{0}",ParseCategoryProperty(genotypeCategoryText,"rsid"));
						var genotype = DNA.StringToDiploidGenotype(string.Format("{0};{1}",ParseCategoryProperty(genotypeCategoryText,"allele1"),ParseCategoryProperty(genotypeCategoryText,"allele2")));
						var genotypeTraitSummary = ParseCategoryProperty(genotypeCategoryText, "summary");
						if (database.snpToInfoMap.ContainsKey(rsid))
						{
							var snpGenotypes = database.snpToInfoMap[rsid].genotypes;
							for (var genotypeIndex = 0; genotypeIndex < snpGenotypes.Length; ++genotypeIndex)
							{
								if (snpGenotypes[genotypeIndex].genotype.Equals(genotype))
								{
									snpGenotypes[genotypeIndex].trait = genotypeTraitSummary;
									break;
								}
							}
						}
					}
					else if (ParsePageCategorySubstring(pageText, "is a \\| medical condition") != null ||
						ParsePageCategorySubstring(pageText, "is a \\| medicine") != null ||
						ParsePageCategorySubstring(pageText, "is a \\| gene") != null)
					{
						// If the page isn't a SNP, add it to the database as a trait with associated SNPs.
						var traitInfo = new TraitInfo();
						traitInfo.title = pageTitle;

						// Parse links to associated SNPs.
						var associatedSNPs = new List<string>();
						Utilities.ProcessDelimitedItems(pageText, "[[", "]]", delegate(string itemText)
						{
							var snpId = itemText.ToLowerInvariant();
							if (snpId.StartsWith("rs"))
							{
								if (!associatedSNPs.Contains(snpId))
								{
									associatedSNPs.Add(snpId);
								}
							}
							return itemText;
						});
						traitInfo.associatedSNPs = associatedSNPs.ToArray();

						if (traitInfo.associatedSNPs.Length > 0)
						{
							// Add the trait to the database.
							database.traits.Add(traitInfo);
						}
					}
				}
			}
		}

		private static async Task UpdatePages(SNPDatabase database, IEnumerable<string> pageTitles, CancellationToken cancellationToken)
		{
			var queryResponse = await CallMediaWikiAPI(string.Format(
				"action=query&prop=revisions&rvprop=content&titles={0}&format=xml",
				HttpUtility.UrlEncode(string.Join("|",pageTitles))
				), cancellationToken);
			if (!cancellationToken.IsCancellationRequested && queryResponse.bSuccess)
			{
				// Process each page received in the response.
				var pageElements = queryResponse.resultXML.SelectNodes("query/pages/page");
				foreach (XmlNode pageElement in pageElements)
				{
					var pageTitle = pageElement.Attributes["title"].Value;
					var pageRevision = pageElement.SelectSingleNode("revisions/rev");
					ProcessPage(database,pageTitle,pageRevision.InnerText);
				}
			}
		}

		public static async Task UpdateSNPDatabaseAsync(SNPDatabase database, UpdateProgressDelegate updateProgressDelegate, CancellationToken cancellationToken)
		{
			// Read the page list.
			updateProgressDelegate("Reading SNPedia page list",0);
			var pageList = await ReadPageListAsync(updateProgressDelegate,cancellationToken);

			// Sort the page list to ensure that genotype pages (e.g. rs1234(a;a)) are processed after the corresponding snp page (i.e. rs1234)
			pageList.Sort(StringComparer.OrdinalIgnoreCase);

			var pagesPerBatch = 50;
			for (int pageIndex = 0; pageIndex < pageList.Count; pageIndex += pagesPerBatch)
			{
				if (cancellationToken.IsCancellationRequested) { break; }
				
				await UpdatePages(database, new ArraySegment<string>(pageList.ToArray(), pageIndex, Math.Min(pageList.Count - pageIndex, pagesPerBatch)), cancellationToken);

				updateProgressDelegate(string.Format("Processed {0}/{1} pages", pageIndex, pageList.Count), (double)pageIndex / (double)pageList.Count);
			}
		}

		public static async Task<SNPDatabase> CreateSNPDatabaseAsync(UpdateProgressDelegate updateProgressDelegate, CancellationToken cancellationToken)
		{
			var result = new SNPDatabase();
			try
			{
				await UpdateSNPDatabaseAsync(result, updateProgressDelegate, cancellationToken);
				return result;
			}
			catch(TaskCanceledException)
			{
				return null;
			}
		}
	}
}
