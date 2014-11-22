using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Personal_Genome_Explorer
{
	class CSVWriter : IDisposable
	{
		// The stream that the CSV is being written to.
		private StreamWriter parentWriter;

		// Whether the current row has any columns yet.
		private bool bRowHasColumns = false;

		public CSVWriter(StreamWriter inParentWriter)
		{
			Debug.Assert(inParentWriter != null);
			parentWriter = inParentWriter;
		}

		public void AddColumn(string ColumnValue)
		{
			// If this isn't the first column in the row, add a comma to separate it from the previous column.
			if(bRowHasColumns)
			{
				parentWriter.Write(',');
			}
			bRowHasColumns = true;

			// Encode embedded double quotes as double double quotes.
			ColumnValue = ColumnValue.Replace("\"","\"\"");

			// Write the column string, delimited by double quotes.
			parentWriter.Write("\"{0}\"",ColumnValue);
		}

		public void FlushRow()
		{
			// Write a newline to advance to the next row.
			parentWriter.Write('\n');

			// Indicate that the next column written is the first column in the row, and doesn't need to be preceded by a comma.
			bRowHasColumns = false;
		}

		// IDisposable interface.
		public void Dispose()
		{
            // Flush the last row to the file.
            FlushRow();

			if(parentWriter != null)
			{
				parentWriter.Dispose();
				parentWriter = null;
			}
		}
	}
}
