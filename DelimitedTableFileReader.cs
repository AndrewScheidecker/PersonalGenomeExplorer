using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Personal_Genome_Explorer
{
	/** Reads a table from a text file with one row per line, and columns separated by a delimiting character. */
	class DelimitedTableFileReader
	{
		StreamReader parentReader;
		char commentPrefix;
		char columnDelimiter;

		/** A delegate that defines how each table row is processed. */
		public delegate void ProcessRowDelegate(string[] columns);
		
		/** Initialization constructor. */
		public DelimitedTableFileReader(StreamReader inParentReader,char inCommentPrefix,char inColumnDelimiter)
        {
            parentReader = inParentReader;
			commentPrefix = inCommentPrefix;
			columnDelimiter = inColumnDelimiter;
        }

		/** Reads the table's rows. */
		public void Read(ProcessRowDelegate processRowDelegate)
		{
			while (true)
			{
				string[] columns = ReadNextRow();
				if (columns != null)
				{
					processRowDelegate(columns);
				}
				else
				{
					break;
				}
			};
		}

		/** Reads the next row of the table. */
        string[] ReadNextRow()
        {
            while(!parentReader.EndOfStream)
            {
                string rowString = parentReader.ReadLine();

                // Skip blank or comment lines.
                if (rowString.Length > 0 && rowString[0] != commentPrefix)
                {
                    return rowString.Split(columnDelimiter);
                }
            };

            return null;
        }
	}
}
