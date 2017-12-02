using System;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Personal_Genome_Explorer
{
    class dbSNPReader
	{
		StreamReader parentStream;

		/** A regex that matches text like this: rs8896 */
		Regex snpIdRegex = new Regex("^(rs\\d+)", RegexOptions.Compiled);

		/** A regex that matches either alleles or orientation declarations:
         *  alleles='C/T'
         * or:
         *  CTG...orient=-
         */
		Regex snpInfoRegex = new Regex("(alleles=\'([^\'.]*)\')|(CTG.*orient=([-+]))", RegexOptions.Compiled);
 
		/** Initialization constructor. */
		public dbSNPReader(StreamReader inParentStream)
		{
			parentStream = inParentStream;
		}

		/** Processes SNP orientation info from the parent stream and updates the local SNP database. */
		public void ProcessSNPOrientationInfo(CancellationToken cancellationToken)
		{
			while(!cancellationToken.IsCancellationRequested && !parentStream.EndOfStream)
			{
				// Read blocks separated by empty lines.
				while(!parentStream.EndOfStream)
				{
					var snpLine = parentStream.ReadLine();

				    // Parse the rs# for the SNP.
				    string snpId = Utilities.GetSingleRegexMatch(snpLine, snpIdRegex,null);
                    if (snpId != null)
                    {
                        // Skip SNPs that aren't in the local database.
                        if (!SNPDatabaseManager.localDatabase.snpToInfoMap.ContainsKey(snpId.ToLowerInvariant()))
                        {
                            while (!parentStream.EndOfStream)
                            {
                                var infoLine = parentStream.ReadLine();
                                if (infoLine == "") { break; }
                            }
                        }
                        else
                        {
                            var snpInfo = SNPDatabaseManager.localDatabase.snpToInfoMap[snpId];
                            var snpOrientationInfo = new SNPOrientationInfo();

                            // After reading a SNP heading, read info lines that are associated with the SNP until a blank line is encountered.
                            while (!parentStream.EndOfStream)
                            {
                                var infoLine = parentStream.ReadLine();
                                if (infoLine == "") { break; }

                                // Parse the orientation of the refSNP cluster on the reference human genome build.
                                var regexMatch = snpInfoRegex.Match(infoLine);
                                if (regexMatch.Success)
                                {
                                    if (regexMatch.Groups[2].Success)
                                    {
                                        // Parse the alleles for this SNP.
                                        string alleles = regexMatch.Groups[2].Value;

                                        // Parse the alleles of the SNP oriented to the refSNP.
                                        snpOrientationInfo.bHasAlleleA = alleles.Contains("A");
                                        snpOrientationInfo.bHasAlleleT = alleles.Contains("T");
                                        snpOrientationInfo.bHasAlleleC = alleles.Contains("C");
                                        snpOrientationInfo.bHasAlleleG = alleles.Contains("G");
                                    }
                                    else
                                    {
                                        Debug.Assert(regexMatch.Groups[4].Success);
                                        snpOrientationInfo.orientation = DNA.StringToOrientation(regexMatch.Groups[4].Value);
                                        Debug.Assert(snpOrientationInfo.orientation != Orientation.Unknown);
                                    }
                                }
                            }

                            // Set the orientation of this SNP in the database.
                            var snpOrientation = snpOrientationInfo.GetOrientation(snpInfo);
                            Debug.Assert(snpOrientation == snpInfo.orientation || snpInfo.orientation == Orientation.Unknown);
                            snpInfo.orientation = snpOrientation;
                            SNPDatabaseManager.localDatabase.snpToInfoMap[snpId] = snpInfo;
                        }
                    }
				}
			};
		}

		/** The orientation of a genotype relative to the reference human genome. */
		struct SNPOrientationInfo
		{
			public bool bHasAlleleA;
			public bool bHasAlleleT;
			public bool bHasAlleleC;
			public bool bHasAlleleG;
			public Orientation orientation;

			/** Tests whether a genotype matches the alleles in this orientation. */
			bool DoesGenotypeMatch(Genotype genotype)
			{
				switch(genotype)
				{
					case Genotype.A: return bHasAlleleA;
					case Genotype.T: return bHasAlleleT;
					case Genotype.C: return bHasAlleleC;
					case Genotype.G: return bHasAlleleG;
					default: return true;
				};
			}

			/** Returns the opposite of this orientation. */
			Orientation GetOppositeOrientation()
			{
				switch(orientation)
				{
					case Orientation.Plus: return Orientation.Minus;
					case Orientation.Minus: return Orientation.Plus;
					default: return Orientation.Unknown;
				}
			}

			/** Determines the orientation of the genotypes of a SNP. */
			public Orientation GetOrientation(SNPInfo snpInfo)
			{
				// Check whether any of the SNP's genotypes and their complements don't match the valid alleles for this orientation.
				var matches = new bool[2] { true, true };
				foreach(var genotypeInfo in snpInfo.genotypes)
				{
					// Determine whether this genotype or its complement matches the valid alleles for this orientation.
					var orientedGenotypes = new DiploidGenotype[]
					{
						genotypeInfo.genotype,
						genotypeInfo.genotype.GetComplement()
					};
					for(int tryIndex = 0;tryIndex < orientedGenotypes.Length;tryIndex++)
					{
						if(	!DoesGenotypeMatch(orientedGenotypes[tryIndex].a) ||
							!DoesGenotypeMatch(orientedGenotypes[tryIndex].b))
						{
							matches[tryIndex] =	false;
						}
					}
				}

				if (matches[0] && !matches[1])
				{
					// If the SNP's genotypes all match this orientation's valid alleles, they have the same orientation.
					return orientation;
				}
				else if (matches[1] && !matches[0])
				{
					// If the SNP's genotypes' complements all match this orientation's valid alleles, the SNP's genotypes have the opposite orientation.
					return GetOppositeOrientation();
				}
				else
				{
					// If none of the SNP's genotypes or their complements mismatch this orientation's alleles, we can't determine the orientation of the SNP's genotypes.
					return Orientation.Unknown;
				}
			}
		}
	}
}