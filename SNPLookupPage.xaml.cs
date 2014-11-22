	using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.Diagnostics;

namespace Personal_Genome_Explorer
{
	public struct SNPListItem
	{
		public string SNPID;
		public string dbSNPGenotype;
		public string dbSNPOrientation;

		public string SNPIDProperty
		{
			get { return SNPID; }
		}

		public string dbSNPGenotypeProperty
		{
			get { return dbSNPGenotype; }
		}

		public string dbSNPOrientationProperty
		{
			get { return dbSNPOrientation; }
		}
	};

	/// <summary>
	/// Interaction logic for SNPLookupPage.xaml
	/// </summary>
	public partial class SNPLookupPage : Page
	{
		public SNPLookupPage()
		{
			InitializeComponent();

			addButton.Click += delegate(object sender,RoutedEventArgs args)
			{
				OnUserAddSNP();
			};

			snpTextBox.KeyDown += delegate(object sender, KeyEventArgs args)
			{
				// When the user presses enter in the SNP ID text box, add its current value to the list.
				if(args.Key == Key.Enter || args.Key == Key.Return)
				{
					OnUserAddSNP();
				}
			};

			removeButton.Click += delegate(object sender,RoutedEventArgs args)
			{
				// Make a copy of the list of selected items, so we can iterate over it while removing items from the list box.
				object[] selectedItems = new object[snpListBox.SelectedItems.Count];
				snpListBox.SelectedItems.CopyTo(selectedItems,0);

				// Remove the selected items from the list box.
				foreach (var selectedItem in selectedItems)
				{
					snpListBox.Items.Remove(selectedItem);
				}
			};
		}

		private void OnUserAddSNP()
		{
			AddSNP(snpTextBox.Text.ToLowerInvariant());
			snpTextBox.Text = "";
		}

		private void AddSNP(string SNPID)
		{
			// Don't add the SNP if it's already in the list.
			foreach(var item in snpListBox.Items)
			{
				if(((SNPListItem)item).SNPID == SNPID)
				{
					return;
				}
			}

			// Only add the SNP if it's in the current genome database.
			var maybeSNPValue = App.document.GetSNPValue(SNPID);
			if(maybeSNPValue.HasValue)
			{
				var snpValue = maybeSNPValue.Value;
				var snpListItem = new SNPListItem();
				snpListItem.SNPID = SNPID;
				snpListItem.dbSNPGenotype = string.Format(
					"{0}{1}",
					DNA.GenotypeToCharacter(snpValue.genotype.a),
					DNA.GenotypeToCharacter(snpValue.genotype.b)
					);
				snpListItem.dbSNPOrientation = DNA.OrientationToString(snpValue.orientation);
				snpListBox.Items.Add(snpListItem);
			}
		}
	}
}
