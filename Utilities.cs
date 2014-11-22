using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Personal_Genome_Explorer
{
	delegate bool CancelDelegate();

	class Utilities
	{
		public static bool ArrayCompare<T>(T[] A, T[] B)
		{
			if (A.Length != B.Length)
			{
				return false;
			}
			for (int index = 0; index < A.Length; index++)
			{
				if (!A[index].Equals(B[index]))
				{
					return false;
				}
			}
			return true;
		}

		public static string GetSingleRegexMatch(string searchString,Regex regex, string unmatchedResult)
		{
			var regexMatch = regex.Match(searchString);
			if (regexMatch.Success)
			{
				return regexMatch.Groups[1].Value;
			}
			else
			{
				return unmatchedResult;
			}
		}

		public delegate string ProcessDelimitedItemDelegate(string itemText);

		public static string ProcessDelimitedItems(string text, string beginningDelimiter, string endDelimiter, ProcessDelimitedItemDelegate processDelimitedItemDelegate)
		{
			while (true)
			{
				// Find the next delimited item in the text.
				int itemBeginIndex = text.IndexOf(beginningDelimiter);
				if (itemBeginIndex == -1)
				{
					break;
				}
				int itemEndIndex = text.IndexOf(endDelimiter, itemBeginIndex + beginningDelimiter.Length);
				if (itemEndIndex == -1)
				{
					break;
				}

				// Process the item.
				int itemTextBeginIndex = itemBeginIndex + beginningDelimiter.Length;
				string itemText = text.Substring(itemTextBeginIndex, itemEndIndex - itemTextBeginIndex);
				string replacedItemText = processDelimitedItemDelegate(itemText);

				// Replace the item with the result of processDelimitedItemDelegate.
				text = text.Substring(0, itemBeginIndex) + replacedItemText + text.Substring(itemEndIndex + endDelimiter.Length);
			};

			return text;
		}

		public static string ConvertWikiTextToPlainText(string wikiText)
		{
			var result = wikiText;

			// Strip out the category text.
			result = Utilities.ProcessDelimitedItems(result, "{{", "}}", delegate(string itemText)
			{
				return "";
			});

			// Replace internal links and images.
			result = Utilities.ProcessDelimitedItems(result, "[[", "]]", delegate(string itemText)
			{
				if (itemText.ToLowerInvariant().StartsWith("image"))
				{
					// Strip out images.
					return "";
				}
				else
				{
					// Replace links with their friendly text.
					string friendlyText = itemText;
					int friendlySplitIndex = itemText.IndexOf('|');
					if (friendlySplitIndex != -1)
					{
						friendlyText = itemText.Substring(friendlySplitIndex + 1);
					}
					return friendlyText;
				}
			});

			// Replace external links.
			result = Utilities.ProcessDelimitedItems(result, "[", "]", delegate(string itemText)
			{
				return itemText;
			});

			// Coalesce multi-line breaks into single-line breaks.
			while (result.Contains("\n\n"))
			{
				result = result.Replace("\n\n", "\n");
			};

			// Replace single-line breaks with double-line breaks.
			result = result.Replace("\n", "\n\n");

			return result;
		}
	};
}
