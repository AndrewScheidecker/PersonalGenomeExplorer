using System;
using System.Text;
using System.IO;

namespace Personal_Genome_Explorer
{
	/** Imports genome data exported from 23andme's website. */
	class deCODEmeReader
	{
		public static string fileFilterString = "deCODEme data files|*.csv|All files (*.*)|*.*";

		DelimitedTableFileReader tableReader;

		public deCODEmeReader(StreamReader inParentReader)
		{
			tableReader = new DelimitedTableFileReader(inParentReader, '#', ',');

			// Skip the first line of the file, which contains headings.
			inParentReader.ReadLine();
		}

		public IndividualGenomeDatabase Read()
        {
			// Initialize the database.
			var database = new IndividualGenomeDatabase();

			// Read the table of SNP genotypes.
			tableReader.Read(
				delegate(string[] columns)
				{
					if(columns.Length == 6)
					{
						// Parse a column of this format: rs4477212,A/G,1,72017,+,AA
						var snpId = columns[0];
						var snpValue = new SNPGenotype();
						snpValue.genotype = DNA.StringToDiploidGenotype(columns[5]);
						snpValue.orientation = DNA.StringToOrientation(columns[4]);

						// Add the SNP genotype to the database.
						database.AddSNPValue(snpId, snpValue);
					}
				});
			return database;
        }
	}
}
