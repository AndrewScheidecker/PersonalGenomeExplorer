using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Personal_Genome_Explorer
{
	/** Encapsulates the global SNP database. */
	public class SNPDatabaseManager
	{
		public static SNPDatabase localDatabase = InternalLoadDatabase();

		private static SNPDatabase InternalLoadDatabase()
		{
			// Load the default database.
			using(var defaultDatabaseStream = new MemoryStream(Properties.Resources.DefaultSNPDatabase,false))
			{
				SNPDatabase result = null;
				if(SNPDatabase.Load(defaultDatabaseStream, ref result))
				{
					return result;
				}
				else
				{
					return new SNPDatabase();
				}
			}
		}

		public static void RevertDatabase()
		{
			localDatabase = InternalLoadDatabase();
		}

		public static bool ImportDatabase(Stream stream)
		{
			SNPDatabase newDatabase = null;
			if(SNPDatabase.Load(stream,ref newDatabase))
			{
				localDatabase = newDatabase;
				return true;
			}
			else
			{
				return false;
			}
		}

		public static void ExportDatabase(Stream stream)
		{
			localDatabase.Save(stream);
		}
	};

	public class SNPDatabase
	{
		public Dictionary<string, SNPInfo> snpToInfoMap = new Dictionary<string, SNPInfo>();
		public List<TraitInfo> traits = new List<TraitInfo>();

		private static char[] referenceFileMagic = new char[] { 'S', 'N', 'P', 'D', 'B', '1' };

		public void Save(Stream stream)
		{
			var writer = new BinaryWriter(stream);

			// Write the file magic and version.
			writer.Write(referenceFileMagic);

			// Write the SNP IDs and values to the file.
			writer.Write((Int32)snpToInfoMap.Count);
			foreach (var pair in snpToInfoMap)
			{
				writer.Write(pair.Key);
				pair.Value.Write(writer);
			}

			// Write the traits to the file.
			writer.Write((Int32)traits.Count);
			foreach(var trait in traits)
			{
				trait.Write(writer);
			}
		}

		public static bool Load(Stream stream, ref SNPDatabase outResult)
		{
			var reader = new BinaryReader(stream);

			// Read the file magic and version.
			char[] fileMagic = reader.ReadChars(referenceFileMagic.Length);

			// If the file doesn't have the expected magic header, abort and return an error.
			if (!Utilities.ArrayCompare(fileMagic, referenceFileMagic))
			{
				return false;
			}

			// Create the SNP info database that's about to be loaded.
			outResult = new SNPDatabase();

			// Read the SNPs in the database.
			int numSNPs = reader.ReadInt32();
			for(int snpIndex = 0;snpIndex < numSNPs;snpIndex++)
			{
				// Read a SNP ID and value pair, and add them to the database.
				var Key = reader.ReadString();
				var Value = SNPInfo.Read(reader);
				outResult.snpToInfoMap.Add(Key, Value);
			}

			// Read the traits in the database.
			int numTraits = reader.ReadInt32();
			for(int traitIndex = 0;traitIndex < numTraits;traitIndex++)
			{
				outResult.traits.Add(TraitInfo.Read(reader));
			}

			return true;
		}
	}
}
