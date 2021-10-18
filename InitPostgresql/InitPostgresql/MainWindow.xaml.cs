using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace InitPostgresql
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            PostgresqlPort = Convert.ToInt32(App.ArgValue("--postgres-port"));
            if (PostgresqlPort == 0)
                PostgresqlPort = 4012;
            PostgresUserPassword = App.ArgValue("--postgres-user-pass") ?? "123456";
            EnableChange = App.ArgValue("--disable-change") == "true" ? false : true;

            Topmost = true;

            if (App.ArgValue("--auto-init") == "true" || File.Exists(PostgresqlConf))
            {
                Show();
                Button_Click(this, null);
                Close();
            }
        }

        #region EnableChange
        public bool EnableChange
        {
            get { return (bool)GetValue(EnableChangeProperty); }
            set { SetValue(EnableChangeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for EnableChange.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EnableChangeProperty =
            DependencyProperty.Register("EnableChange", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));
        #endregion //EnableChange
        
        #region PostgresqlPort
        public int PostgresqlPort
        {
            get { return (int)GetValue(PostgresqlPortProperty); }
            set { SetValue(PostgresqlPortProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PostgresqlPort.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PostgresqlPortProperty =
            DependencyProperty.Register("PostgresqlPort", typeof(int), typeof(MainWindow), new PropertyMetadata(0));
        #endregion //PostgresqlPort

        #region PostgresUserPassword
        public string PostgresUserPassword
        {
            get { return (string)GetValue(PostgresUserPasswordProperty); }
            set { SetValue(PostgresUserPasswordProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PostgresUserPassword.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PostgresUserPasswordProperty =
            DependencyProperty.Register("PostgresUserPassword", typeof(string), typeof(MainWindow), new PropertyMetadata(""));
        #endregion //PostgresUserPassword

        public string BaseDirectory => Debugger.IsAttached ? "C:\\ABC Software\\ABC Postgresql 12\\PostgreSQL" : AppDomain.CurrentDomain.BaseDirectory;

        public string PgDataDirectory => Path.Combine(BaseDirectory, PostgresDirectory, @"data");

        public string PgPass => Path.Combine(BaseDirectory, @"temp.txt");

        public string InitDbExe => Path.Combine(BaseDirectory, PostgresDirectory, @"bin\initdb.exe");

        public string PSqlExe => Path.Combine(BaseDirectory, PostgresDirectory, @"bin\psql.exe");

        public string PostgresqlConf => Path.Combine(PgDataDirectory, @"postgresql.conf");

        public string PostgresDirectory => "";

        public void InitPostgresCluster()
        {
            if (!File.Exists(InitDbExe))
                throw new FileNotFoundException($"Couldn't find initdb.exe at {InitDbExe}");

            if (File.Exists(PostgresqlConf))
            {
                if (WritePostgresConf())
                    MessageBox.Show("Cluster is already initialized. Only the postgresql.conf file was updated.", "Only Updated postgresql.conf", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            if (PostgresqlPort < 1 || string.IsNullOrWhiteSpace(PostgresUserPassword))
            {
                MessageBox.Show("Port must be greater than 0 and password may not be blank.", "Invalid Values", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                File.WriteAllText(PgPass, PostgresUserPassword);

                var sb = new StringBuilder();

                var si = new ProcessStartInfo()
                {
                    FileName = InitDbExe,
                    Arguments = string.Format("--auth=password --pgdata=\"{0}\" --encoding=UTF8 --locale=C --username=postgres --pwfile=\"{1}\"",
                                        PgDataDirectory.Replace(@"\\", @"\"), PgPass.Replace(@"\\", @"\")),
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                var p = new Process
                {
                    StartInfo = si
                };
                p.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
                p.ErrorDataReceived += (sender, args) => sb.AppendLine(args.Data);
                p.Start();
                // start our event pumps
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                // until we are done
                p.WaitForExit();

                File.WriteAllText(Path.Combine(BaseDirectory, "intidb_output.txt"), sb.ToString());
                //Process.Start(Path.Combine(BaseDirectory, "intidb_output.txt"));

                File.Delete(PgPass);

                WritePostgresConf();
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Close();
        }

        public bool WritePostgresConf()
        {
            if (!File.Exists(PostgresqlConf))
                throw new FileNotFoundException($"Couldn't find postgresql.conf at {PostgresqlConf}");

            var lines = File.ReadAllText(PostgresqlConf);
            var original = lines;
            lines = lines.Replace("#listen_addresses = 'localhost'", "listen_addresses = 'localhost'");
            lines = lines.Replace("#port = 5432", "port = " + PostgresqlPort);
            lines = lines.Replace("max_connections = 100", "max_connections = 500");
            lines = lines.Replace("#log_destination = 'stderr'", "log_destination = 'stderr'");
            lines = lines.Replace("#logging_collector = off", "logging_collector = on");
            lines = lines.Replace("#log_line_prefix = ''", "log_line_prefix = '%t'");

            File.WriteAllText(PostgresqlConf, lines);

            return original != lines;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            InitPostgresCluster();
        }
    }

    public class Script
    {
        public string ConnectionString { get; set; }
        public string Commands { get; set; }
    }
}
