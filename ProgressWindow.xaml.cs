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
	public delegate void AddProgressMessageDelegate(string progressText);

	/// <summary>
	/// Interaction logic for Progress.xaml
	/// </summary>
	public partial class ProgressWindow : Window
	{
		// Indicates whether the user has requested that the operation be cancelled.
		public bool bCancelRequested = false;

		private bool bForcingClose = false;

		public ProgressWindow()
		{
			InitializeComponent();

			this.Closing += delegate(object sender,CancelEventArgs args)
			{
				bCancelRequested = true;
				if(bForcingClose)
				{
					args.Cancel = false;
				}
			};
		}

		public void ForceClose()
		{
			bForcingClose = true;
			Close();
		}

		public void AddProgressMessage(string progressText)
		{
			progressTextBox.Text += string.Format("{0}\n",progressText);

			// Create a new dispatcher frame.
			DispatcherFrame dispatcherFrame = new DispatcherFrame();

			// Add an event to the end of the thread's queue that stops the new dispatcher frame from processing events.
			Dispatcher.CurrentDispatcher.BeginInvoke(
				DispatcherPriority.Background,
				new DispatcherOperationCallback(delegate(object context)
				{
					dispatcherFrame.Continue = false;
					return null;
				}),
				null
				);

			// Process events in the new dispatcher frame.  This will return once it reaches the stop message enqueued above.
			Dispatcher.PushFrame(dispatcherFrame);
		}
	}
}
