using System;
using System.Text;
using System.IO;

namespace Personal_Genome_Explorer
{
	/** Imports genome data exported from 23andme's website. */
	class TwentyThreeAndMeReader
	{
		public static string fileFilterString = "23andme data files|*.txt|All files (*.*)|*.*";

        DelimitedTableFileReader tableReader;

		public TwentyThreeAndMeReader(StreamReader inParentReader)
		{
			tableReader = new DelimitedTableFileReader(inParentReader,'#','\t');
		}

        public IndividualGenomeDatabase Read()
        {
			// Initialize the database.
			var database = new IndividualGenomeDatabase();

			// Read the table of SNP genotypes.
			tableReader.Read(
				delegate(string[] columns)
				{
					if(columns.Length == 4)
					{
						// Parse a column of this format: rs#	chromosome	position	genotype
						var snpId = columns[0];
						var snpValue = new SNPGenotype();
						snpValue.genotype = DNA.StringToDiploidGenotype(columns[3]);
						snpValue.orientation = Orientation.Plus;

						// Add the SNP genotype to the database.
						database.AddSNPValue(snpId, snpValue);
					}
				});
			return database;
        }
	}
}
