using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Personal_Genome_Explorer
{
	class SNPediaReader
	{
        /** The base URI for the SNPedia MediaWiki. */
		private static string baseURI = "http://www.snpedia.com/";

        /** The maximum number of pages to request listing of at a time.  The MediaWiki API limits this to 500. */
		private static int maxPagesRequest = 500;

        /** The result of a MediaWiki API call. */
		private class APIResult
		{
			public XmlNode resultXML = null;
			public bool bSuccess = false;
			public string errorCode = "";
			public string errorDescription = "";
		}

		private static APIResult CallMediaWikiAPI(string options)
		{
			// Append the options to the API URL.
			var uri = string.Format("{0}api.php", baseURI, options);

			// Create the web request object.
			var webRequest = (HttpWebRequest)WebRequest.Create(uri);
			webRequest.UserAgent = "Personal Genome Explorer";

			// Encode the post data from the string.
			var postData = (new ASCIIEncoding()).GetBytes(options);

			// Set up the request as a post request.
			webRequest.Method = "POST";
			webRequest.ContentType = "application/x-www-form-urlencoded";
			webRequest.ContentLength = postData.Length;

			// Write the post data into the request's stream.
			var postDataStream = webRequest.GetRequestStream();
			postDataStream.Write(postData, 0, postData.Length);
			postDataStream.Close();

			// Read the response.
			var webResponse = (HttpWebResponse)webRequest.GetResponse();
			if(webResponse.StatusCode == HttpStatusCode.OK)
			{
				var responseString = (new StreamReader(webResponse.GetResponseStream())).ReadToEnd();
				var responseDocument = new XmlDocument();
				responseDocument.Load(new StringReader(responseString.TrimStart(' ','\t','\r','\n')));

                // Check for an API error.
				var result = new APIResult();
				var errorElement = responseDocument.SelectSingleNode("/error");
				if(errorElement != null)
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
				result.errorCode = webResponse.StatusCode.ToString();
				result.errorDescription = webResponse.StatusDescription;
				result.bSuccess = false;

				return result;
			}
		}

		private static List<string> ReadPageList()
		{
			var pageList = new List<string>();

			// Read the SNPedia page list.
			string firstQueryPage = null;
			do
			{
				var result = CallMediaWikiAPI(string.Format(
					"action=query&list=allpages&aplimit={0}{1}&format=xml",
					maxPagesRequest,
					firstQueryPage != null ? ("&apfrom="+firstQueryPage) : ""
					));
				if(result.bSuccess)
				{
					firstQueryPage = null;

					// Read the query continue element.
					var continueElement = result.resultXML.SelectSingleNode("query-continue/allpages");
					if(continueElement != null)
					{
						firstQueryPage = continueElement.Attributes["apfrom"].Value;
					}

					// Read the page element list.
					var pageElements = result.resultXML.SelectNodes("query/allpages/p");
					foreach(XmlNode pageElement in pageElements)
					{
						pageList.Add(pageElement.Attributes["title"].Value);
					}
				}
				else
				{
					break;
				}
			}
			while(firstQueryPage != null);

			return pageList;
		}

		private static string ReadPageText(string title)
		{
			// Append the options to the API URL.
			var uri = string.Format("{0}index.php?action=raw&title={1}", baseURI, title );

			// Create the web request object.
			var webRequest = (HttpWebRequest)WebRequest.Create(uri);
			webRequest.UserAgent = "Personal Genome Explorer";
			webRequest.Method = "GET";

			// Read the response.
			try
			{
				var webResponse = (HttpWebResponse)webRequest.GetResponse();
				if(webResponse.StatusCode == HttpStatusCode.OK)
				{
					return (new StreamReader(webResponse.GetResponseStream())).ReadToEnd().Replace("\r","\n");
				}
			}
			catch(WebException)
			{
			}

			return null;
		}

		private static string ParsePageCategorySubstring(string pageText, string category)
		{
            // Parses a category block in the form:  {{ name ... }}
			return Utilities.GetSingleRegexMatch(pageText,new Regex(string.Format("{{{{\\s*{0}([^}}]*)}}}}",category), RegexOptions.IgnoreCase),null);
		}

		private static string ParseCategoryProperty(string categorySubstring, string propertyTag)
		{
            // Parses a category property in the form:  | property = value
			return Utilities.GetSingleRegexMatch(categorySubstring, new Regex(string.Format("\\|[ \t]*{0}[ \t]*=?[ \t]*([^}}\n]*)", propertyTag), RegexOptions.IgnoreCase), "");
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
						newGenotype.trait = ParseCategoryProperty(snpSubstring, string.Format("effect{0}", genotypeIndex + 1));
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

		public static void UpdateSNPDatabase(ref SNPDatabase database, AddProgressMessageDelegate addProgressMessageDelegate, CancelDelegate cancelDelegate)
		{
			// Read the page list.
			addProgressMessageDelegate("Reading SNPedia page list");
			var pageList = ReadPageList();

			// Read the raw contents of each page.
			foreach (var pageTitle in pageList)
			{
				if (cancelDelegate())
				{
					break;
				}
				else
				{
					addProgressMessageDelegate(string.Format("Reading SNPedia page {0}", pageTitle));

					var pageText = ReadPageText(pageTitle);
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
							if (ParsePageCategorySubstring(pageText, "is a \\| medical condition") != null ||
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
			}
		}

		public static SNPDatabase CreateSNPDatabase(AddProgressMessageDelegate addProgressMessageDelegate, CancelDelegate cancelDelegate)
		{
			var result = new SNPDatabase();
            UpdateSNPDatabase(ref result,addProgressMessageDelegate,cancelDelegate);
			return result;
		}
	}
}
