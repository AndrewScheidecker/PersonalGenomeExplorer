using System;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace Personal_Genome_Explorer
{
	class dbSNPReader
	{
		StreamReader parentStream;

		/** A regex that matches text like this: rs8896 */
		Regex snpIdRegex = new Regex("^(rs\\d+)");

		/** A regex that matches text like this: alleles='C/T' */
		Regex allelesRegex = new Regex("alleles=\'([^\'.]*)\'");

		/** A regex that matches text like this: CTG | assembly=reference | chr=MT | chr-pos=8270 | NC_001807.4 | ctg-start=8270 | ctg-end=8270 | loctype=2 | orient=- */
		Regex buildOrientationRegex = new Regex("CTG.*assembly=reference.*orient=([-+])");

		/** Initialization constructor. */
		public dbSNPReader(StreamReader inParentStream)
		{
			parentStream = inParentStream;
		}

		/** Processes SNP orientation info from the parent stream and updates the local SNP database. */
		public void ProcessSNPOrientationInfo()
		{
			while(!parentStream.EndOfStream)
			{
				// Read blocks separated by empty lines.
				string currentBlock = "";
				while(!parentStream.EndOfStream)
				{
					var line = parentStream.ReadLine();
					currentBlock += line;
					currentBlock += "\r\n";
					if(line == "")
					{
						break;
					}
				};

				// Parse the rs# for the SNP.
				string snpId = Utilities.GetSingleRegexMatch(currentBlock,snpIdRegex,null);
				if(snpId != null)
				{
					// Ignore SNPs that aren't in the local database.
					if(SNPDatabaseManager.localDatabase.snpToInfoMap.ContainsKey(snpId.ToLowerInvariant()))
					{
						// Ignore SNPs that we already know the orientation of.
						var snpInfo = SNPDatabaseManager.localDatabase.snpToInfoMap[snpId];

						// Parse the orientation of the refSNP cluster on the reference human genome build.
						string buildOrientationString = Utilities.GetSingleRegexMatch(currentBlock,buildOrientationRegex,null);
						if (buildOrientationString != null)
						{
							Orientation buildOrientation = DNA.StringToOrientation(buildOrientationString);
							Debug.Assert(buildOrientation != Orientation.Unknown);

							// Parse the alleles for this SNP.
							string alleles = Utilities.GetSingleRegexMatch(currentBlock,allelesRegex,"").ToUpperInvariant();

							// Parse the alleles of the SNP oriented to the refSNP.
							var orientationInfo = new SNPOrientationInfo();
							orientationInfo.bHasAlleleA = alleles.Contains("A");
							orientationInfo.bHasAlleleT = alleles.Contains("T");
							orientationInfo.bHasAlleleC = alleles.Contains("C");
							orientationInfo.bHasAlleleG = alleles.Contains("G");
							orientationInfo.orientation = buildOrientation;

							// Set the orientation of this SNP in the database.
							var snpOrientation = orientationInfo.GetOrientation(snpInfo);
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