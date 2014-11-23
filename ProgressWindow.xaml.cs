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
using System.Windows.Shapes;
using System.Windows.Threading;
using System.ComponentModel;

namespace Personal_Genome_Explorer
{
	// A delegate that receives progress updates.
	public delegate void UpdateProgressDelegate(string progressText,double progress);

	/// <summary>
	/// Interaction logic for Progress.xaml
	/// </summary>
	public partial class ProgressWindow : Window
	{
		private bool bForcingClose = false;

		private Action OnClose;

		public ProgressWindow(Action InOnClose)
		{
			OnClose = InOnClose;
			InitializeComponent();
		}

		public void ForceClose()
		{
			bForcingClose = true;
			Close();
		}

		public void OnCancel(object sender, RoutedEventArgs args)
		{
			OnClose();
		}

		public void OnClosing(object sender,CancelEventArgs args)
		{
			OnClose();
			if (bForcingClose)
			{
				args.Cancel = false;
			}
		}

		public void Update(string progressText, double progress)
		{
			progressLabel.Content = string.Format("{0}\n", progressText);
			progressBar.Value = progress * 100.0;
		}
	}
}
