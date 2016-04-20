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


using VkNet;
using VkNet.Enums.Filters;
using VkNet.Model.RequestParams;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Enums.SafetyEnums;

using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using VkNet.Enums;

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VK_Musical_Search
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int ApplicationId = your_app_ID;
        static Settings scope = Settings.All;
        static VkApi api = new VkApi();

        public MainWindow()
        {
            InitializeComponent();

            Sex_comboBox.ItemsSource = Enum.GetValues(typeof(Sex));
            Sex_comboBox.SelectedItem = Sex.Female;

            Status_comboBox.ItemsSource = Enum.GetValues(typeof(MaritalStatus));
            Status_comboBox.SelectedItem = MaritalStatus.Single;

            LoadCredentials();
        }


       

        private void Authorize_button_Click(object sender, RoutedEventArgs e)
        {
            Func<string> code = () =>
            {
                var codeWindow = new CodeWindow();

                codeWindow.ShowDialog();                

                return Globals.Code;
            };
            
            try
            {
                if((bool)IsCode_checkBox.IsChecked)
                    api.Authorize(new ApiAuthParams
                    {
                        ApplicationId = ApplicationId,
                        Login = MailPhone_textBox.Text,
                        Password = Password_passwordBox.Password,
                        Settings = scope,
                        TwoFactorAuthorization = code
                    });
                else
                    api.Authorize(new ApiAuthParams
                    {
                        ApplicationId = ApplicationId,
                        Login = MailPhone_textBox.Text,
                        Password = Password_passwordBox.Password,
                        Settings = scope
                    });
            }
            catch
            {
                MessageBox.Show("Не удалось авторизоваться.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            Authorized();
        }


        void LoadCredentials()
        {
            if (!File.Exists("credentials")) return;

            var credentials = File.ReadAllLines("credentials");

            MailPhone_textBox.Text = credentials[0];

            try
            {
                api.Authorize(credentials[1]);
				
                var test = api.Account.GetCounters(CountersFilter.Gifts);
            }
            catch
            {
                return;
            }

            Authorized();
        }

        void SaveCredentials()
        {
            if (api.AccessToken == null) return;

            var credentials = MailPhone_textBox.Text + Environment.NewLine + api.AccessToken;

            File.WriteAllText("credentials", credentials);
        }


        void Authorized()
        {
            MailPhone_textBox.IsEnabled = false;

            Password_passwordBox.IsEnabled = false;

            Authorize_button.Content = "Авторизованы";

            Search_button.IsEnabled = true;

            Authorize_button.IsEnabled = false;

            IsCode_checkBox.IsEnabled = false;
        }



        static User[] SearchUsersByCretereas([Optional] string query, [Optional] string cityQuery, [Optional] ushort ageFrom, [Optional] ushort ageTo, [Optional] MaritalStatus status, [Optional] Sex sex)
        {
            var users = new User[0];

            var cityId = (int)api.Database.GetCities(1, query: cityQuery).First().Id;


            int count;
            users = api.Users.Search(out count, new UserSearchParams
            {
                Query = query,
                Sex = sex,
                City = cityId,
                AgeFrom = ageFrom,
                AgeTo = ageTo,
                Status = status,
                HasPhoto = true,
                Count = 1000
            }).ToArray();

            return users;
        }


        static string[] GetCommonAudios(Audio[] arr1, Audio[] arr2)
        {
            var common = new List<string>();

            foreach (var audio1 in arr1)
            {
                foreach (var audio2 in arr2)
                {
                    if (audio1.Artist.ToLower() == audio2.Artist.ToLower() && audio1.Title.ToLower() == audio2.Title.ToLower())
                    {
                        common.Add($"{audio1.Artist} - {audio1.Title}");

                        break;
                    }
                }
            }


            return common.Distinct().OrderBy(x => x).ToArray();
        }

        static Audio[] GetUsersAudio(long userID)
        {
            User u;

            Audio[] audios;

            try
            {
                audios = api.Audio.Get(out u, new AudioGetParams { Count = 6000, NeedUser = false, OwnerId = userID }).ToArray();
            }
            catch
            {
                return new Audio[0];
            }

            return audios;
        }

        static Thread DoWork;

        static Stopwatch stopwatch;
        const int apiRequestsPerSecond = 3;
        const int delayBetweenApiRequestsInMs = 1000 / apiRequestsPerSecond;

        void WaitRequestInterval()
        {
            var needToSleep = (int)(delayBetweenApiRequestsInMs - stopwatch.ElapsedMilliseconds);

            if (needToSleep <= 0)
            {
                //stopwatch.Restart();

                return;
            }
            

            Thread.Sleep(needToSleep);

            stopwatch.Restart();
        }


        void Work()
        {
            SearchInProgress(true);

            stopwatch = new Stopwatch();

            stopwatch.Start();

            ushort ageFrom = 0;
            ushort ageTo = 0;
            Sex sex = Sex.Unknown;
            MaritalStatus status = 0;

            string currentId = "";
            string query = "";
            string city = "";

            int usersToParse = 1000;

            bool isAnyStatus = false;

            Dispatcher.Invoke(new Action(delegate
            {
                var input = ReferenceId_TextBox.Text.Trim();

                if (Regex.IsMatch(input, @"\d+")) input = "id" + input;                

                currentId = ReferenceId_TextBox.Text.Trim();
                query = Query_TextBox.Text.Trim();
                city = City_TextBox.Text.Trim();

                ushort.TryParse(AgeFrom_TextBox.Text.Trim(), out ageFrom);
                ushort.TryParse(AgeTo_TextBox.Text.Trim(), out ageTo);

                if(!int.TryParse(UsersToParseCount_TextBox.Text.Trim(), out usersToParse)) usersToParse = 1000;

                sex = (Sex)Sex_comboBox.SelectedItem;
                status = (MaritalStatus)Status_comboBox.SelectedItem;

                isAnyStatus = (bool)IsIgnoreStatus_checkBox.IsChecked;
            }));

            var userInfo = api.Utils.ResolveScreenName(currentId);
            WaitRequestInterval();

            var userId = userInfo.Id;

            User u;
            Audio[] thisProgramUserAudios_Reference = api.Audio.Get(out u, new AudioGetParams { Count = 6000, NeedUser = false, OwnerId = userId }).ToArray();
            WaitRequestInterval();

            var foundUsers = SearchUsersByCretereas(
                    query: query,
                    ageFrom: ageFrom,
                    ageTo: ageTo,
                    cityQuery: city,
                    sex: sex,
                    status: isAnyStatus? 0 : status
                    );

            var results = new List<SearchResult>();

            foreach(var user in foundUsers)
            {
                usersToParse--;

                if (usersToParse < 0) break;

                WaitRequestInterval();

                Audio[] userAudios;
                try
                {
                    userAudios = api.Audio.Get(out u, new AudioGetParams { Count = 6000, NeedUser = false, OwnerId = user.Id }).ToArray();
                }
                catch
                {
                    // доступ был запрещён
                    continue;
                }

                if (userAudios.Length == 0) continue;

                var commonAudios = GetCommonAudios(thisProgramUserAudios_Reference, userAudios);

                if (commonAudios.Length != 0)
                    results.Add(new SearchResult { User = user, CommonAudios = commonAudios });
            }

            results = results.OrderByDescending(x => x.CommonAudios.Length).ToList();


            Dispatcher.BeginInvoke(new Action(delegate
            {
                var text = "";

                foreach(var result in results)
                {
                    text += $"{result.User.FirstName} {result.User.LastName} https://vk.com/id{result.User.Id}" + Environment.NewLine;
                    text += $"Общие записи ({result.CommonAudios.Length}):" + Environment.NewLine;
                    text += string.Join(Environment.NewLine, result.CommonAudios.Select(x => "\t" + x).ToArray());

                    text += Environment.NewLine + Environment.NewLine;
                }

                if (results.Count() == 0)
                    Results_TextBox.Text = "Не найдено совпадений.";
                else
                    Results_TextBox.Text = text;
            }));

            SearchInProgress(false);
        }


        void NoCommonAudios()
        {
            Dispatcher.Invoke(new Action(delegate
            {
                //Audios_textBox.Text = "Нет общих записей";
                //Refresh_button.IsEnabled = true;
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveCredentials();
        }


        private void Search_button_Click(object sender, RoutedEventArgs e)
        {
            if(ReferenceId_TextBox.Text == "")
            {
                MessageBox.Show("Сначала введите ID того пользователя, относительно которого будет идти поиск. Если id цифровой, например id123, то вводите id123, а не 123.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Asterisk);

                return;
            }

            Results_TextBox.Text = "";

            DoWork = new Thread(new ThreadStart(Work));
            DoWork.IsBackground = true;
            DoWork.Start();
        }


        void SearchInProgress(bool isInProgress)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                Search_button.IsEnabled = !isInProgress;
                Search_button.Content = isInProgress ? "Идёт поиск..." : "Поиск";
            }));
        }

        private void IsIgnoreStatus_checkBox_Click(object sender, RoutedEventArgs e)
        {
            Status_comboBox.IsEnabled = !Status_comboBox.IsEnabled;
        }
    }

    class SearchResult
    {
        public User User { get; set; }
        public string[] CommonAudios { get; set; }
    }
}
