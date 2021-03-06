﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Personal_Genome_Explorer
{
	class Analyses
	{
		static Color TraitColor = Color.FromRgb(0x34, 0x5E, 0x78);
		static Color GroupColor = Color.FromRgb(0x34 / 2, 0x5E / 2, 0x78 / 2);

		private static void CreateTraitPage(string snpId,SNPInfo snpInfo,ref List<UIElement> genotypedPageList)
		{
			// Check if the current genome database has a genotype for this SNP.
			var value = App.document.GetSNPValue(snpId);
			var genotype = value != null ?
				value.Value.GetOrientedGenotype(snpInfo.orientation) :
				new DiploidGenotype(Genotype.Unknown, Genotype.Unknown);

			// Create the trait page for this SNP.
			var page = new SimpleDiploidTraitPage(
				snpInfo,
				genotype
				);

			// Add the trait page to the appropriate list depending on whether there's a genotype for it.
			if(page.bHasMatchingGenotype)
			{
				genotypedPageList.Add(page);
			}
		}

		public static List<UIElement> SNPedia()
		{
			var snpDatabase = SNPDatabaseManager.localDatabase;

			// Iterate over the traits in the database.
			var traitPages = new List<UIElement>();
			foreach(var trait in snpDatabase.traits)
			{
				// Create pages for the trait's associated SNPs.
				var genotypedTraitSNPPages = new List<UIElement>();
				var ungenotypedTraitSNPPages = new List<UIElement>();
				foreach(var associatedSNP in trait.associatedSNPs)
				{
					if(snpDatabase.snpToInfoMap.ContainsKey(associatedSNP))
					{
						var snpInfo = snpDatabase.snpToInfoMap[associatedSNP];
						CreateTraitPage(associatedSNP, snpInfo, ref genotypedTraitSNPPages);
					}
				}
				// If the trait has any genotyped SNP pages, create a group page for it.
				if (genotypedTraitSNPPages.Count > 0)
				{
					traitPages.Add(new AnalysisGroup(trait.title, genotypedTraitSNPPages, false, TraitColor));
				}
			}
            
			// Find SNPs that we have a genotype for that doesn't match the alleles in the database.
			foreach(var pair in snpDatabase.snpToInfoMap)
			{
				if(pair.Value.orientation != Orientation.Unknown)
				{
					var snpValue = App.document.GetSNPValue(pair.Key);
					if(snpValue != null)
					{
						var genotype = snpValue.Value.genotype;
						if(genotype.a != Genotype.Unknown && genotype.b != Genotype.Unknown)
						{
							var sensibleSNPPages = new List<UIElement>();
							CreateTraitPage(pair.Key, pair.Value, ref sensibleSNPPages);
						}
					}
				}
			}

            return traitPages;
		}
	};
}