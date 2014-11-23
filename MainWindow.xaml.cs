using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using WinForms = System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;

namespace Personal_Genome_Explorer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>

	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			InitAnalysis();
		}

		private void AddAnalysis(UIElement analysisElement)
		{
			if (analysisElement != null)
			{
				var frame = new Frame();
				frame.Content = analysisElement;
				analysisStack.Children.Add(frame);
			}
		}

		private void InitAnalysis()
		{
			analysisStack.Children.Clear();

			// Force the old analysis pages to be garbage collected.
			GC.Collect();

			// Use reflection to iterate over the static methods of the analyses type.
			var analysisMembers = typeof(Analyses).FindMembers(
				System.Reflection.MemberTypes.Method,
				BindingFlags.Static | BindingFlags.Public,
				delegate(MemberInfo member,object filterCriteria) { return true; },
				null
				);
			foreach (var analysisMember in analysisMembers)
			{
				var analysisMethod = (MethodInfo)analysisMember;

				// Only process members with the analysis attribute.
				object[] analysisAttributes = analysisMethod.GetCustomAttributes(typeof(AnalysisAttribute), false);
				if(analysisAttributes.Length > 0)
				{
					Debug.Assert(analysisAttributes[0].GetType() == typeof(AnalysisAttribute));

					// Invoke the analysis.
					object result = analysisMethod.Invoke(null,new object[] {});
					if(result != null)
					{
						// Add the analysis's UIElement to the analysis grid.
						Debug.Assert(result.GetType() == typeof(List<UIElement>));
						var analysisElements = (List<UIElement>)result;
						foreach(var analysisElement in analysisElements)
						{
							AddAnalysis(analysisElement);
						}
					}
				}
			}
		}

		private void menuClick_FileNew(object sender,RoutedEventArgs args)
		{
			App.documentSaveStream = null;
			App.document = new IndividualGenomeDatabase();

            // Reanalyze after resetting the data.
            InitAnalysis();
		}

		private void menuClick_FileOpen(object sender, RoutedEventArgs args)
		{
			bool bSuccessfullyLoaded = false;

			var dialog = new WinForms.OpenFileDialog();
			dialog.Filter = "Personal Genome Explorer files|*.PersonalGenomeData|All files (*.*)|*.*";
			if (dialog.ShowDialog() == WinForms.DialogResult.OK)
			{
				// If we had a file stream open for saving the current document, close it now to ensure that it doesn't prevent us from opening the user's new file.
				if (App.documentSaveStream != null)
				{
					App.documentSaveStream.Close();
					App.documentSaveStream = null;
				}

				// Open the chosen file.
				using(var fileStream = dialog.OpenFile())
				{
					int TryIndex = 0;
					while(true)
					{
						// Seek to the beginning of the file.
						fileStream.Seek(0,SeekOrigin.Begin);

						// Try a blank password before prompting the user.
						string password = "";
						if(TryIndex > 0)
						{
							// Prompt the user for the genome file's password.
							var passwordWindow = new PasswordWindow();
							passwordWindow.Owner = this;
							passwordWindow.ShowDialog();
							password = passwordWindow.password;
							if(!passwordWindow.bOkPressed)
							{
								// The user cancelled the password dialog, abort the open.
								break;
							}
						}

						// Try to load the genome from the chosen file.
						GenomeLoadResult result = IndividualGenomeDatabase.Load(fileStream, password, ref App.document);

						// If there was an error, display the appropriate dialog.
						if (result == GenomeLoadResult.IncorrectPassword)
						{
							if(TryIndex > 0)
							{
								// If the user entered the wrong password, give them another chance to enter it.
								WinForms.MessageBox.Show("The file cannot be decrypted with that password.", "Incorrect Password");
							}
						}
						else if (result == GenomeLoadResult.UnrecognizedFile)
						{
							WinForms.MessageBox.Show("The file doesn't appear to be a valid PersonalGenomeData file.", "Unrecognized file");
							break;
						}
						else
						{
							bSuccessfullyLoaded = true;
							break;
						}

						++TryIndex;
					};
				}
			}

			if(bSuccessfullyLoaded)
			{
				// Reanalyze after loading the data.
				InitAnalysis();
			}
		}

		private void InternalSave(bool bForcePrompt)
		{
			// Prompt the user for the file to save to if this is the first save, or the re-prompt is requested.
			if (bForcePrompt || App.documentSaveStream == null)
			{
				var dialog = new WinForms.SaveFileDialog();
				dialog.Filter = "Personal Genome Explorer files|*.PersonalGenomeData|All files (*.*)|*.*";
				if (dialog.ShowDialog() == WinForms.DialogResult.OK)
				{
					// Close the old save stream.
					if (App.documentSaveStream != null)
					{
						App.documentSaveStream.Close();
					}

					// Open the file chosen by the user for writing.
					App.documentSaveStream = dialog.OpenFile();
				}
			}

			if (App.documentSaveStream != null)
			{
				// Prompt the user for the password to protect the file with if this is the first save, or the re-prompt is requested.
				if (bForcePrompt || App.document.password == "")
				{
					var passwordWindow = new PasswordWindow();
					passwordWindow.Owner = this;
					passwordWindow.ShowDialog();
					if(passwordWindow.bOkPressed)
					{
						App.document.password = passwordWindow.password;
					}
					else
					{
						// Abort saving if the password prompt was cancelled.
						App.document.password = "";
						return;
					}
				}

				// Clear the file stream the document was last saved to.
				App.documentSaveStream.Seek(0, SeekOrigin.Begin);
				App.documentSaveStream.SetLength(0);

				// Write the genome to the file.
				App.document.Save(App.documentSaveStream);
			}
		}

		private void menuClick_FileSave(object sender,RoutedEventArgs args)
		{
			InternalSave(false);
		}

		private void menuClick_FileSaveAs(object sender,RoutedEventArgs args)
		{
			InternalSave(true);
		}

		private void menuClick_FileExit(object sender,RoutedEventArgs args)
		{
			Close();
		}

		private async void menuClick_ImportFrom_23AndMeAsync(object sender, RoutedEventArgs args)
		{
			var dialog = new WinForms.OpenFileDialog();
			dialog.Filter = TwentyThreeAndMeReader.fileFilterString;
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                // Open the chosen file.
                using (var fileStream = dialog.OpenFile())
                {
                    var streamReader = new StreamReader(fileStream);
					var database = await Task.Run(() => (new TwentyThreeAndMeReader(streamReader)).Read());

                    App.document = database;
                    App.documentSaveStream = null;

                    // Reanalyze after loading the data.
                    InitAnalysis();
                }
            }
		}

		private async void menuClick_ImportFrom_deCODEmeAsync(object sender, RoutedEventArgs args)
		{
			var dialog = new WinForms.OpenFileDialog();
			dialog.Filter = deCODEmeReader.fileFilterString;
			if (dialog.ShowDialog() == WinForms.DialogResult.OK)
			{
				// Open the chosen file.
				using (var fileStream = dialog.OpenFile())
				{
					var streamReader = new StreamReader(fileStream);
					var database = await Task.Run(() =>(new deCODEmeReader(streamReader)).Read());

					App.document = database;
					App.documentSaveStream = null;

					// Reanalyze after loading the data.
					InitAnalysis();
				}
			}
		}

		private void menuClick_ExportToCSV(object sender, RoutedEventArgs args)
		{
			var dialog = new WinForms.SaveFileDialog();
			dialog.Filter = "Comma-Separated-Value files|*.csv|All files (*.*)|*.*";
			if (dialog.ShowDialog() == WinForms.DialogResult.OK)
			{
				// Open the file chosen by the user, and write the genome to it in CSV format.
				using (var fileStream = dialog.OpenFile())
				{
					App.document.WriteToCSV(fileStream);
				}
			}
		}

		private void menuClick_RandomizeDataCEU(object sender,RoutedEventArgs args)
		{
			App.document.Randomize("CEU");

			// Reanalyze after randomizing the data.
			InitAnalysis();
		}

        private void menuClick_RandomizeDataHCB(object sender, RoutedEventArgs args)
        {
            App.document.Randomize("HCB");

            // Reanalyze after randomizing the data.
            InitAnalysis();
        }

        private void menuClick_RandomizeDataJPT(object sender, RoutedEventArgs args)
        {
            App.document.Randomize("JPT");

            // Reanalyze after randomizing the data.
            InitAnalysis();
        }

        private void menuClick_RandomizeDataYRI(object sender, RoutedEventArgs args)
        {
            App.document.Randomize("YRI");

            // Reanalyze after randomizing the data.
            InitAnalysis();
        }

		private async void menuClick_ImportFromSNPediaAsync(object sender, RoutedEventArgs args)
		{
			// Don't allow importing the SNPedia data unless this isn't an end user (i.e. the app is being debugged)
			if(!Debugger.IsAttached)
			{
				WinForms.MessageBox.Show("Importing data from SNPedia is disabled for non-developers to limit the load on SNPedia.  This option is re-enabled if you run the program within a debugger.", "SNPedia import disabled");
				return;
			}

			// Disable the main window.
			IsEnabled = false;

			// Create the progress window.
			var cancellationTokenSource = new CancellationTokenSource();
			var progressWindow = new ProgressWindow(() => cancellationTokenSource.Cancel());
			progressWindow.Owner = this;
			progressWindow.Show();

			// Try to read the data from the provided 23andme username.
			var newDatabase = await SNPediaReader.CreateSNPDatabaseAsync(
				(progressText,progress) => progressWindow.Update(progressText,progress),
				cancellationTokenSource.Token
				);

			// Close the progress window and reenable the main window.
			progressWindow.ForceClose();
			IsEnabled = true;

			// If the database was successfully imported, replace the local database with it.
			if (newDatabase != null)
			{
				SNPDatabaseManager.localDatabase = newDatabase;

				// Reanalyse after updating the SNP database.
				InitAnalysis();
			}
		}

		private async void menuClick_ImportFromdbSNPAsync(object sender, RoutedEventArgs args)
		{
			var dialog = new WinForms.OpenFileDialog();
			dialog.Filter = "dbSNP flat files|*.flat|All files (*.*)|*.*";
			dialog.Multiselect = true;
			if (dialog.ShowDialog() == WinForms.DialogResult.OK)
			{
				// Disable the main window.
				IsEnabled = false;

				// Create the progress window.
				var cancellationTokenSource = new CancellationTokenSource();
				var progressWindow = new ProgressWindow(() => cancellationTokenSource.Cancel());
				progressWindow.Owner = this;
				progressWindow.Show();

				var fileNameList = dialog.FileNames.ToList();
				for(var fileIndex = 0;fileIndex < fileNameList.Count;++fileIndex)
				{
					var filename = fileNameList[fileIndex];
					using(var fileStream = new FileStream(filename,FileMode.Open,FileAccess.Read))
					{
						progressWindow.Update(string.Format("Processing {0}", filename), (double)fileIndex / fileNameList.Count);
						await Task.Run(()=>(new dbSNPReader(new StreamReader(fileStream))).ProcessSNPOrientationInfo(cancellationTokenSource.Token));
					}
				}

				// Close the progress window and reenable the main window.
				progressWindow.ForceClose();
				IsEnabled = true;

				// Reanalyse after updating the SNP database.
				InitAnalysis();
			}
		}

		private void menuClick_RevertSNPDatabase(object sender,RoutedEventArgs args)
		{
			// Revert the SNP database to the builtin database.
			SNPDatabaseManager.RevertDatabase();

			// Reanalyse after updating the SNP database.
			InitAnalysis();
		}

		private void menuClick_LoadSNPDatabase(object sender, RoutedEventArgs args)
		{
			var dialog = new WinForms.OpenFileDialog();
			dialog.Filter = "SNPDatabase files|*.SNPDatabase|All files (*.*)|*.*";
			if (dialog.ShowDialog() == WinForms.DialogResult.OK)
			{
				// Open the file chosen by the user, and write the genome to it in CSV format.
				using (var fileStream = dialog.OpenFile())
				{
					if(!SNPDatabaseManager.ImportDatabase(fileStream))
					{
						WinForms.MessageBox.Show("The file doesn't appear to be a valid SNPDatabase file.", "Unrecognized file");
					}
					else
					{
						// Reanalyse after updating the SNP database.
						InitAnalysis();
					}
				}
			}
		}

		private void menuClick_SaveSNPDatabase(object sender,RoutedEventArgs args)
		{
			var dialog = new WinForms.SaveFileDialog();
			dialog.Filter = "SNPDatabase files|*.SNPDatabase|All files (*.*)|*.*";
			if (dialog.ShowDialog() == WinForms.DialogResult.OK)
			{
				// Open the file chosen by the user, and write the genome to it in CSV format.
				using (var fileStream = dialog.OpenFile())
				{
					SNPDatabaseManager.ExportDatabase(fileStream);
				}
			}
		}

		private void menuClick_LaunchWebsite(object sender,RoutedEventArgs args)
		{
			System.Diagnostics.Process.Start("http://www.scheidecker.net/personal-genome-explorer/");
		}
	}
}
