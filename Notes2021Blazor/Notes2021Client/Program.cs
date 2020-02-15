using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notes2021Client
{
    static class Program
    {
        public static readonly HttpClient MyClient = new HttpClient();
        public static string AuthToken;
        public static string baseUri;
        public static string DefaultLogin = string.Empty;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            DefaultLogin = config["DefaultLogin"];
            baseUri = config["Server"];
            MyClient.BaseAddress = new Uri(baseUri);

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            Login login = new Login();
            tryagain:
            if (login.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(login.token))
                {
                    login.RestPassword();
                    goto tryagain;
                }

                AuthToken = login.token;
                MyClient.DefaultRequestHeaders.Add("authentication", login.token);
                Application.Run(new ListFiles());
            }


        }
    }
}
