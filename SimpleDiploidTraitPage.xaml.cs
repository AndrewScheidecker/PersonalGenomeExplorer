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

namespace Personal_Genome_Explorer
{
    /**
     * The item type that's added to the genotype listbox.
     * It exposes data about the genotype as properties so the listbox's item template data binding can access it.
     */
	class SNPGenotypeUIAdapter
	{
        /** The information about this genotype of the SNP. */
		private SNPGenotypeInfo genotypeInfo;

        /** Whether this is the genotype that matches the currently loaded genome. */
        private bool bPersonalGenotype;

        /** Initialization constructor. */
		public SNPGenotypeUIAdapter(SNPGenotypeInfo inGenotypeInfo,bool bInPersonalGenotype)
		{
			genotypeInfo = inGenotypeInfo;
            bPersonalGenotype = bInPersonalGenotype;
		}

        // Data binding properties.
		public string Genotype
		{
			get { return genotypeInfo.genotype.ToString(); }
		}
		public string You
		{
			get
			{
                return bPersonalGenotype ? genotypeInfo.genotype.ToString() : "";
			}
		}
		public string Trait
		{
			get { return genotypeInfo.trait; }
		}
		public string CEU
		{
			get
			{
				return genotypeInfo.populationFrequencies.ContainsKey("CEU") ?
					string.Format("{0}%",genotypeInfo.populationFrequencies["CEU"] * 100.0f) :
					"?";
			}
		}
		public string HCB
		{
			get
			{
				return genotypeInfo.populationFrequencies.ContainsKey("HCB") ?
					string.Format("{0}%", genotypeInfo.populationFrequencies["HCB"] * 100.0f) :
					"?";
			}
		}
		public string JPT
		{
			get
			{
				return genotypeInfo.populationFrequencies.ContainsKey("JPT") ?
					string.Format("{0}%", genotypeInfo.populationFrequencies["JPT"] * 100.0f) :
					"?";
			}
		}
		public string YRI
		{
			get
			{
				return genotypeInfo.populationFrequencies.ContainsKey("YRI") ?
					string.Format("{0}%", genotypeInfo.populationFrequencies["YRI"] * 100.0f) :
					"?";
			}
		}
		public Visibility ShowHighlight
		{
			get
			{
                return bPersonalGenotype ? Visibility.Visible : Visibility.Hidden;
			}
		}
	};

	/// <summary>
	/// Interaction logic for SimpleAnalysisPage.xaml
	/// </summary>
	public partial class SimpleDiploidTraitPage : Page
	{
		public readonly bool bHasMatchingGenotype;

		public SimpleDiploidTraitPage(
			SNPInfo snpInfo,
			DiploidGenotype personalGenotype
			)
		{
			InitializeComponent();

            // Setup the SNP information controls.
			nameLabel.Content = snpInfo.id;
			descriptionLabel.Text = Utilities.ConvertWikiTextToPlainText(snpInfo.descriptionWikiText);
            snpediaLink.NavigateUri = new Uri(string.Format("http://www.snpedia.com/index.php?title={0}", snpInfo.id));

            // Setup the list of genotypes for this SNP.
			bHasMatchingGenotype = false;
            foreach (var genotypeInfo in snpInfo.genotypes)
            {
				bool bGenotypeMatchesPersonalGenome = personalGenotype.Equals(genotypeInfo.genotype);
				if (bGenotypeMatchesPersonalGenome)
				{
					bHasMatchingGenotype = true;
				}
                genotypeList.Items.Add(new SNPGenotypeUIAdapter(
                    genotypeInfo,
					bGenotypeMatchesPersonalGenome
                    ));
            }

			// If the genome doesn't match any of the genotypes, create a placeholder genotype for it.
			if (!bHasMatchingGenotype)
			{
				var genotypeInfo = new SNPGenotypeInfo();
				genotypeInfo.genotype = personalGenotype;
				genotypeInfo.trait = "";
				genotypeInfo.populationFrequencies = new Dictionary<string, float>();
				genotypeList.Items.Add(new SNPGenotypeUIAdapter(
					genotypeInfo,
					true
					));
			}
		}

        private void SNPediaLink_RequestNavigation(object sender, RequestNavigateEventArgs e)
		{
            // Launch a browser process for the SNPedia URL.
            System.Diagnostics.Process.Start(snpediaLink.NavigateUri.ToString());
			e.Handled = true;
		}
	}
}
