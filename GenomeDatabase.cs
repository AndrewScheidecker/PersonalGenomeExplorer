using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Personal_Genome_Explorer
{
	public enum GenomeLoadResult
	{
		Success,
		IncorrectPassword,
		UnrecognizedFile
	};

	public class IndividualGenomeDatabase
	{
		/** A set of associations between SNP ID and their values in this genome. */
		private Dictionary<string, SNPGenotype> snpToGenotypeMap = new Dictionary<string, SNPGenotype>();

		/** The password used to encrypt the database file. */
		public string password = "";

		public void AddSNPValue(string id,SNPGenotype value)
		{
			// Always store the SNP value with the lower-case SNP ID.
			id = id.ToLowerInvariant();

			if(snpToGenotypeMap.ContainsKey(id))
			{
				// If there SNP value is already in the database, ensure this data doesn't conflict with it.
                SNPGenotype oldValue = snpToGenotypeMap[id];
				Debug.Assert(oldValue.Equals(value));
			}
			else
			{
				// Add the SNP value to the database.
				snpToGenotypeMap.Add(id,value);
			}
		}

		public SNPGenotype? GetSNPValue(string id)
		{
			// Convert the SNP ID to the canonical lower-case format before lookup.
			id = id.ToLowerInvariant();

			if(snpToGenotypeMap.ContainsKey(id))
			{
				return snpToGenotypeMap[id];
			}
			else
			{
				return null;
			}
		}

        private static char[] referenceFileMagic = new char[] { 'P', 'G', 'F', '2' };
        private static char[] referenceFileMagicLegacy0 = new char[] { 'P', 'G', 'F', '0' };
        private static char[] referenceFileMagicLegacy1 = new char[] { 'P', 'G', 'F', '1' };

        private const int numSaltBytes = 24;
        private const int numIVBytes = 16;
        private const int numKeyBytes = 32;
        private const int numHashIterations = 1000;

        public void Save(Stream stream)
		{
			var writer = new BinaryWriter(stream);
            var rng = new RNGCryptoServiceProvider();

            // Write the file magic and version.
            writer.Write(referenceFileMagic);

            // Generate random salt and initialization vectors and write them to the file.
            var salt = new byte[numSaltBytes];
            rng.GetBytes(salt);
            writer.Write(salt);

            var initializationVector = new byte[numIVBytes];
            rng.GetBytes(initializationVector);
            writer.Write(initializationVector);

            // Use PBKDF2 to hash the password and salt an produce a 256-bit encryption key.
            byte[] encryptionKey = (new Rfc2898DeriveBytes(password, salt, numHashIterations)).GetBytes(numKeyBytes);

			// Create an AES-256 encrypting stream to write the un-encrypted data to the file.
			writer = new BinaryWriter(
				new CryptoStream(
					stream,
					Rijndael.Create().CreateEncryptor(
						encryptionKey,
						initializationVector),
						CryptoStreamMode.Write));

            // Write the file magic a second time to the encrypted stream as a way to verify a password when decrypting.
            writer.Write(referenceFileMagic);

			// Write the SNP IDs and values to the file.
			foreach(var pair in snpToGenotypeMap)
			{
				writer.Write(pair.Key);
				pair.Value.Write(writer);
			}
		}

		public static GenomeLoadResult Load(Stream stream,string password,ref IndividualGenomeDatabase outResult)
		{
			var reader = new BinaryReader(stream);

			// Read the file magic and version.
			char[] fileMagic = reader.ReadChars(referenceFileMagic.Length);

			// If the file doesn't have the expected magic header, abort and return an error.
			bool bFileHasMagic = Utilities.ArrayCompare(fileMagic,referenceFileMagic);
			bool bFileHasLegacyMagic0 = Utilities.ArrayCompare(fileMagic,referenceFileMagicLegacy0);
            bool bFileHasLegacyMagic1 = Utilities.ArrayCompare(fileMagic, referenceFileMagicLegacy1);
            if (!bFileHasMagic && !bFileHasLegacyMagic0 && !bFileHasLegacyMagic1)
			{
				return GenomeLoadResult.UnrecognizedFile;
			}

            if (bFileHasMagic)
            {
                // Read the salt and initialization vector.
                var salt = reader.ReadBytes(numSaltBytes);
                var initializationVector = reader.ReadBytes(numIVBytes);

                // Use PBKDF2 to hash the password and salt an produce a 256-bit encryption key.
                byte[] encryptionKey = (new Rfc2898DeriveBytes(ASCIIEncoding.ASCII.GetBytes(password), salt, numHashIterations)).GetBytes(numKeyBytes);

                // Create a decrypting stream to read the encrypted data from the file.
                reader = new BinaryReader(
                    new CryptoStream(
                        stream,
                        Rijndael.Create().CreateDecryptor(
                            encryptionKey,
                            initializationVector),
                            CryptoStreamMode.Read));

                // Read the encrypted file magic, and if it doesn't match then the password is wrong.
                char[] encryptedFileMagic = reader.ReadChars(fileMagic.Length);
                if(!Utilities.ArrayCompare(encryptedFileMagic,fileMagic))
                {
                    return GenomeLoadResult.IncorrectPassword;
                }
            }
            else
            {
                // Pad the password to 128 bits.
                var paddedPassword = password.PadRight(16, ' ');

                // Read the saved password hash from the file, and compare it to the hash of the password we're trying to load with.
                // This can't be done by saving some magic header inside the encrypted part of the file, as the CryptoStream will crash when decrypting with the wrong password.
                byte[] passwordHash = (new SHA512Managed()).ComputeHash(ASCIIEncoding.ASCII.GetBytes(paddedPassword));
                byte[] savedPasswordHash = reader.ReadBytes(passwordHash.Length);
                if (!Utilities.ArrayCompare(passwordHash, savedPasswordHash))
                {
                    return GenomeLoadResult.IncorrectPassword;
                }

                // Read the initialization vector.
                byte[] initializationVector = reader.ReadBytes(16);

                // Create a decrypting stream to read the encrypted data from the file.
                reader = new BinaryReader(
                    new CryptoStream(
                        stream,
                        Rijndael.Create().CreateDecryptor(
                            ASCIIEncoding.ASCII.GetBytes(paddedPassword),
                            initializationVector),
                            CryptoStreamMode.Read));
            }

			// Create the genome database that's about to be loaded.
			outResult = new IndividualGenomeDatabase();
			outResult.password = password;

			// Read SNPs until the end of the file.
			while(stream.Position < stream.Length)
			{
				// Read a SNP ID and value pair, and add them to the database.
				var Key = reader.ReadString();
				SNPGenotype Genotype;
				if(bFileHasLegacyMagic0)
				{
					Genotype = SNPGenotype.ReadLegacy0(reader);
				}
				else
				{
					Genotype = SNPGenotype.Read(reader);
				}
				outResult.AddSNPValue(Key,Genotype);
			}

			return GenomeLoadResult.Success;
		}

		public void WriteToCSV(Stream stream)
		{
            using (var writer = new CSVWriter(new StreamWriter(stream)))
            {
                // Write the column headers.
                writer.AddColumn("SNP ID");
                writer.AddColumn("Genotype");
                writer.AddColumn("Orientation");
                writer.FlushRow();

                // Write each SNP genotype to a row.
                foreach (var pair in snpToGenotypeMap)
                {
                    var SNPID = pair.Key;
                    var SNPValue = pair.Value;

                    writer.AddColumn(SNPID);
                    writer.AddColumn(string.Format("{0}{1}",
                        DNA.GenotypeToCharacter(SNPValue.genotype.a),
                        DNA.GenotypeToCharacter(SNPValue.genotype.b)
                        ));
                    writer.AddColumn(DNA.OrientationToString(SNPValue.orientation));

                    // Terminate the row.
                    writer.FlushRow();
                }
            }
		}

		public void Randomize(string population)
		{
			var random = new Random();
			var newSNPToGenotypeMap = new Dictionary<string, SNPGenotype>();
			foreach(var pair in SNPDatabaseManager.localDatabase.snpToInfoMap)
			{
				// Chose a random genotype, weighted according to the specified population frequency.
				var randomFraction = random.NextDouble();
				foreach(var genotypeInfo in pair.Value.genotypes)
				{
                    // Determine the frequency for this genotype in the specified population.
                    var populationFrequency = 1.0 / (double)pair.Value.genotypes.Length;
                    if (genotypeInfo.populationFrequencies.ContainsKey(population))
					{
                        populationFrequency = genotypeInfo.populationFrequencies[population];
                    }

                    // Randomly decide whether to use this genotype.
                    if (randomFraction > populationFrequency)
					{
                        randomFraction -= populationFrequency;
					}
                    else
                    {
                        var newValue = new SNPGenotype();
                        newValue.genotype = genotypeInfo.genotype;
                        newValue.orientation = pair.Value.orientation;
                        newSNPToGenotypeMap.Add(pair.Key, newValue);
                        break;
					}
				}
			}
			snpToGenotypeMap = newSNPToGenotypeMap;
		}
	};
}
