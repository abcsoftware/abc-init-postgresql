using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InitPostgresql
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string[] Args { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            Args = e.Args.Length > 0 ? e.Args : new string[0];
        }

        public static string ArgValue(string arg)
        {
            var a = Args.Where(s => s.StartsWith(arg)).FirstOrDefault();
            if (a == null)
                return null;

            try
            {
                return a.Split('=')[1];
            }
            catch (Exception)
            {
                throw new ArgumentException($"{a} is an invalid argument");
            }
        }
    }
}
