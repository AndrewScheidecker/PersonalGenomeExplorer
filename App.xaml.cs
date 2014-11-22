using System;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Xml;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Personal_Genome_Explorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		public static IndividualGenomeDatabase document = new IndividualGenomeDatabase();
		public static Stream documentSaveStream = null;
    }
}
