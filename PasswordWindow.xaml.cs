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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Personal_Genome_Explorer
{
	/// <summary>
	/// Interaction logic for PasswordWindow.xaml
	/// </summary>

	public partial class PasswordWindow : Window
	{
		public string password = "";
		public bool bOkPressed = false;

		public PasswordWindow()
		{
			InitializeComponent();

			// Start with the focus on the password text box.
			passwordTextBox.Focus();

			// When the user presses the OK button, close the dialog and indicate success.
			okButton.Click += delegate(object sender,RoutedEventArgs args)
			{
				password = passwordTextBox.Password;
				bOkPressed = true;
				Close();
			};

			// When the user presses the OK button, close the dialog.
			cancelButton.Click += delegate(object sender,RoutedEventArgs args)
			{
				Close();
			};
		}
	}
}
