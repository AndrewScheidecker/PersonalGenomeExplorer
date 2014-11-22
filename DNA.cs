using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Personal_Genome_Explorer
{
	public enum Genotype
	{
		Unknown,
		None,
		A,
		G,
		T,
		C,
		Deletion,
		Insertion
	};

    /** Both genotypes of a SNP that a diploid chromosome has. */
	public struct DiploidGenotype
	{
		public Genotype a;
		public Genotype b;

		public DiploidGenotype(Genotype inA,Genotype inB)
		{
			a = inA;
			b = inB;
		}

		public bool Equals(DiploidGenotype otherPair)
		{
			return (a == otherPair.a && b == otherPair.b) ||
					(a == otherPair.b && b == otherPair.a);
		}
		public override string ToString()
		{
			return string.Format("{0}{1}",DNA.GenotypeToCharacter(a),DNA.GenotypeToCharacter(b));
		}
        public DiploidGenotype GetComplement()
        {
            return new DiploidGenotype(
                DNA.GetComplementaryNucleotide(a),
                DNA.GetComplementaryNucleotide(b)
                );
        }

        // Serializers
        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)a);
            writer.Write((byte)b);
        }
        public static DiploidGenotype Read(BinaryReader reader)
        {
            var result = new DiploidGenotype();
            result.a = (Genotype)reader.ReadByte();
            result.b = (Genotype)reader.ReadByte();
            return result;
        }
	};

	public enum Orientation
	{
		Unknown,
		Plus,
		Minus
	};

	public struct SNPGenotypeInfo
	{
		public DiploidGenotype genotype;
		public string trait;
		public Dictionary<string,float> populationFrequencies;
	};

	public struct SNPInfo
	{
		public string id;
		public string descriptionWikiText;
		public string gene;
		public string chromosome;
		public int position;
		public Orientation orientation;
		public DateTime updateTime;
		public SNPGenotypeInfo[] genotypes;

		// Serializers.
		public void Write(BinaryWriter writer)
		{
			writer.Write(id);
			writer.Write(descriptionWikiText);
			writer.Write(gene);
			writer.Write(chromosome);
			writer.Write(position);
			writer.Write((byte)orientation);
			writer.Write(updateTime.ToBinary());
			writer.Write((Int32)genotypes.Length);
			foreach(var genotypeInfo in genotypes)
			{
				genotypeInfo.genotype.Write(writer);
				writer.Write(genotypeInfo.trait);
				writer.Write((Int32)genotypeInfo.populationFrequencies.Count);
				foreach(var populationFrequency in genotypeInfo.populationFrequencies)
				{
					writer.Write(populationFrequency.Key);
					writer.Write(populationFrequency.Value);
				}
			}
		}
		public static SNPInfo Read(BinaryReader reader)
		{
			var result = new SNPInfo();
			result.id = reader.ReadString();
			result.descriptionWikiText = reader.ReadString();
			result.gene = reader.ReadString();
			result.chromosome = reader.ReadString();
			result.position = reader.ReadInt32();
			result.orientation = (Orientation)reader.ReadByte();
			result.updateTime = DateTime.FromBinary(reader.ReadInt64());
			result.genotypes = new SNPGenotypeInfo[reader.ReadInt32()];
			for(int genotypeIndex = 0;genotypeIndex < result.genotypes.Length;genotypeIndex++)
			{
				var genotypeInfo = new SNPGenotypeInfo();
				genotypeInfo.genotype = DiploidGenotype.Read(reader);
				genotypeInfo.trait = reader.ReadString();
				genotypeInfo.populationFrequencies = new Dictionary<string,float>();
				int numPopulations = reader.ReadInt32();
				for(int populationIndex = 0;populationIndex < numPopulations;populationIndex++)
				{
					var populationTag = reader.ReadString();
					var populationFrequency = reader.ReadSingle();
					genotypeInfo.populationFrequencies.Add(populationTag,populationFrequency);
						
				}
				result.genotypes[genotypeIndex] = genotypeInfo;
			}
			return result;
		}
	};

	public struct TraitInfo
	{
		public string title;
		public string[] associatedSNPs;
		
		// Serializers.
		public void Write(BinaryWriter writer)
		{
			writer.Write(title);
			writer.Write(associatedSNPs.Length);
			foreach(var snp in associatedSNPs)
			{
				writer.Write(snp);
			}
		}
		public static TraitInfo Read(BinaryReader reader)
		{
			var result = new TraitInfo();
			result.title = reader.ReadString();
			result.associatedSNPs = new string[reader.ReadInt32()];
			for(int snpIndex = 0;snpIndex < result.associatedSNPs.Length;snpIndex++)
			{
				result.associatedSNPs[snpIndex] = reader.ReadString();
			}
			return result;
		}
	};

	public struct SNPGenotype
	{
		public DiploidGenotype genotype;
		public Orientation orientation;

		/** Accesses the genotype oriented for a given strand. */
		public DiploidGenotype GetOrientedGenotype(Orientation targetOrientation)
		{
			if(targetOrientation == Orientation.Unknown || orientation == Orientation.Unknown)
			{
				return new DiploidGenotype(Genotype.Unknown,Genotype.Unknown);
			}
			else if(orientation == targetOrientation)
			{
				return genotype;
			}
			else
			{
				return genotype.GetComplement();
			}
		}

		// Serializers.
		public void Write(BinaryWriter writer)
		{
			writer.Write((byte)orientation);
			genotype.Write(writer);
		}
		public static SNPGenotype Read(BinaryReader reader)
		{
			var result = new SNPGenotype();
			result.orientation = (Orientation)reader.ReadByte();
			result.genotype = DiploidGenotype.Read(reader);
			return result;
		}
		public static SNPGenotype ReadLegacy(BinaryReader reader)
		{
			var result = new SNPGenotype();
			var legacyGenotype = DiploidGenotype.Read(reader);
			var legacyOrientation = reader.ReadByte();
			result.orientation = Orientation.Plus;
			result.genotype = DiploidGenotype.Read(reader);
			return result;
		}
	};

	public class DNA
	{
		public static char GenotypeToCharacter(Genotype genotype)
		{
			switch (genotype)
			{
				case Genotype.A: return 'A';
				case Genotype.G: return 'G';
				case Genotype.T: return 'T';
				case Genotype.C: return 'C';
				case Genotype.Deletion: return 'D';
				case Genotype.Insertion: return 'I';
				case Genotype.None: return ' ';
				default:
				case Genotype.Unknown: return '?';
			};
		}

        public static Genotype GetComplementaryNucleotide(Genotype nucleotide)
        {
            switch (nucleotide)
            {
                case Genotype.A: return Genotype.T;
                case Genotype.T: return Genotype.A;
                case Genotype.C: return Genotype.G;
                case Genotype.G: return Genotype.C;
				case Genotype.Deletion: return Genotype.Deletion;
				case Genotype.Insertion: return Genotype.Insertion;
                case Genotype.None: return Genotype.None;
                default:
                case Genotype.Unknown: return Genotype.Unknown;
            };
        }

		public static Genotype CharacterToGenotype(char genotypeChar)
		{
			if (genotypeChar == 'A')
			{
				return Genotype.A;
			}
			else if (genotypeChar == 'G')
			{
				return Genotype.G;
			}
			else if (genotypeChar == 'T')
			{
				return Genotype.T;
			}
			else if (genotypeChar == 'C')
			{
				return Genotype.C;
			}
			else if (genotypeChar == 'D')
			{
				return Genotype.Deletion;
			}
			else if(genotypeChar == 'I')
			{
				return Genotype.Insertion;
			}
			else
			{
				return Genotype.Unknown;
			}
		}

		public static string OrientationToString(Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Minus: return "Minus";
				case Orientation.Plus: return "Plus";
				default:
				case Orientation.Unknown: return "N/A";
			};
		}

		public static Orientation StringToOrientation(string orientationString)
		{
			if (orientationString.ToLowerInvariant() == "minus" || orientationString == "-")
			{
				return Orientation.Minus;
			}
			else if (orientationString.ToLowerInvariant() == "plus" || orientationString == "+")
			{
				return Orientation.Plus;
			}
			else
			{
				return Orientation.Unknown;
			}
		}

        static Regex diploidGenotypeRegex = new Regex("\\(?([AGTCDI-]);?([AGTCDI-])?\\)?");

		public static DiploidGenotype StringToDiploidGenotype(string genotypeString)
		{
			var result = new DiploidGenotype();
			result.a = Genotype.Unknown;
			result.b = Genotype.Unknown;

            var regexMatch = diploidGenotypeRegex.Match(genotypeString);
			if(regexMatch.Success)
			{
				if (regexMatch.Groups.Count >= 2 && regexMatch.Groups[1].Value.Length > 0)
				{
					result.a = CharacterToGenotype(regexMatch.Groups[1].Value[0]);
				}
				if (regexMatch.Groups.Count >= 3 && regexMatch.Groups[2].Value.Length > 0)
				{
					result.b = CharacterToGenotype(regexMatch.Groups[2].Value[0]);
				}
			}

			return result;
		}
	}
}