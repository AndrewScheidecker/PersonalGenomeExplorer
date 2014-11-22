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
    /** An expandable group of analysis pages. */
    public partial class AnalysisGroup : Page
    {
		Color backgroundColor;

		public Color BackgroundColor
		{
			get { return backgroundColor; }
		}

        public AnalysisGroup(string label,List<UIElement> children,bool bExpanded,Color inBackgroundColor)
		{
            InitializeComponent();

			backgroundColor = inBackgroundColor;

			groupLabel.Content = string.Format("{0} ({1})", label, children.Count);
            expander.IsExpanded = false;

            expander.Expanded += delegate(object sender, RoutedEventArgs args)
            {
                foreach (var child in children)
                {
                    var frame = new Frame();
                    frame.Content = child;
                    childStack.Children.Add(frame);
                }
            };

            expander.Collapsed += delegate(object sender, RoutedEventArgs args)
            {
                childStack.Children.Clear();
            };
            
            expander.IsExpanded = bExpanded;
        }
    }
}
