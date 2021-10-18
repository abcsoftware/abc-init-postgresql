using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace InitPostgresql
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            if (App.ArgValue("--execute-script") == "true" && File.Exists(PgScript))
            {
				try
				{
					var script = JsonConvert.DeserializeObject<Script>(File.ReadAllText(PgScript));

					using (var cn = new NpgsqlConnection(script.ConnectionString))
					{
						using (var cmd = new NpgsqlCommand(script.Commands, cn))
						{
							cn.Open();
							cmd.ExecuteNonQuery();
							cn.Close();
						}
					}
					Close();
				}
				catch (Exception e)
				{
					if (e.Message.Contains("already exists"))
						Close();
					else
					{
						string errorFile = Path.Combine(BaseDirectory, @"errors.txt");
						File.AppendAllText(errorFile, e.Message);
						Process.Start(errorFile);
						throw;
					}
				}
			}

			InitializeComponent();
            DataContext = this;
            PostgresqlPort = Convert.ToInt32(App.ArgValue("--postgres-port"));
            if (PostgresqlPort == 0)
                PostgresqlPort = 4011;
            PostgresUserPassword = App.ArgValue("--postgres-user-pass") ?? "123456";
            NewUser = App.ArgValue("--new-user") ?? "";
            NewUserPassword = App.ArgValue("--new-user-pass") ?? "";
            EnableChange = App.ArgValue("--disable-change") == "true" ? false : true;

            if (App.ArgValue("--auto-init") == "true")
			    Button_Click(this, null);

            Topmost = true;
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

        #region NewUser
        public string NewUser
        {
            get { return (string)GetValue(NewUserProperty); }
            set { SetValue(NewUserProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NewUser.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NewUserProperty =
            DependencyProperty.Register("NewUser", typeof(string), typeof(MainWindow), new PropertyMetadata(""));
        #endregion //NewUser

        #region NewUserPassword
        public string NewUserPassword
        {
            get { return (string)GetValue(NewUserPasswordProperty); }
            set { SetValue(NewUserPasswordProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NewPassword.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NewUserPasswordProperty =
            DependencyProperty.Register("NewUserPassword", typeof(string), typeof(MainWindow), new PropertyMetadata(""));
        #endregion //NewUserPassword

        public string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        public string PgDataDirectory => Path.Combine(BaseDirectory, PostgresDirectory, @"data");

        public string PgPass => Path.Combine(BaseDirectory, @"temp.txt");

        public string InitDbExe => Path.Combine(BaseDirectory, PostgresDirectory, @"bin\initdb.exe");

        public string PSqlExe => Path.Combine(BaseDirectory, PostgresDirectory, @"bin\psql.exe");

        public string PgScript => Path.Combine(BaseDirectory, @"script.psql");

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
                this.Close();
            }

            if (PostgresqlPort < 1 || string.IsNullOrWhiteSpace(PostgresUserPassword))
            {
                MessageBox.Show("Port must be greater than 0 and password may not be blank.", "Invalid Values", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(NewUser) && string.IsNullOrWhiteSpace(NewUserPassword))
            {
                MessageBox.Show("When setting a new user, the password may not be blank", "Invalid Values", MessageBoxButton.OK, MessageBoxImage.Information);
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

                if (!string.IsNullOrWhiteSpace(NewUser))
                {
					var script = new Script() {
						ConnectionString = $"Server=127.0.0.1;Port={PostgresqlPort};Database=postgres;User Id=postgres;Password={PostgresUserPassword};",
						Commands = $@"DO $do$
									BEGIN
										IF NOT EXISTS (
											SELECT                       -- SELECT list can stay empty for this
											FROM   pg_catalog.pg_roles
											WHERE  rolname = 'candleapi') THEN

											CREATE ROLE {NewUser} WITH CREATEDB LOGIN PASSWORD '{NewUserPassword}';
										END IF;
									END
									$do$;"
					};
                    File.WriteAllText(PgScript, JsonConvert.SerializeObject(script));
                }

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
