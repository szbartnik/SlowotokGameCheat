﻿using SłowotokCheat.Models;
using SłowotokCheat.Utilities;
using SłowotokCheat.WebConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Security.Cryptography;
using System.Threading;

namespace SłowotokCheat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Properties and Constants Area

        public const string DICTIONARY_FILENAME = "slowa.txt";

        private MainPageViewModel vm = new MainPageViewModel();
        private char[,] arrayToProcess;
        public GameManagement GameOps { get; set; }
        public Dictionary<string, object> Dictionary { get; set; }

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = vm;
            Dictionary = new Dictionary<string, object>();
        }

        #region Generator Region
        private async Task BeginProcessing()
        {
            if (vm.InProgress) return;

            vm.ShowingResults = true;
            vm.InformationBox = "Generating the words...";
            vm.InProgress = true;
            vm.FoundWords.Clear();

            await Task.Factory.StartNew(() =>
            {
                Parallel.For(0, 4, x =>
                {
                    for (int y = 0; y < 4; y++)
                    {
                        generateWords(arrayToProcess, arrayToProcess[x, y].ToString(), x, y);
                    }
                });
            });

            vm.InProgress = false;
        }

        private void generateWords(char[,] array, string word, int x, int y, int recursLvl = 16)
        {
            char[,] _array = ((char[,])array.Clone()); // cloning the array
            _array[x, y] = '_';

            var possibilities = isMovePossible(_array, x, y);

            if (word.Length >= 3 && Dictionary.ContainsKey(word))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (vm.FoundWords.FirstOrDefault(z => z.Word.Equals(word)) == null
                        && (GameOps != null ? GameOps.CurrentBoard.Hashs.Contains(word.CalculateMD5()) : true))
                    {
                        vm.FoundWords.AddSorted(
                            new WordRecord() { Word = word, Length = word.Length },
                            (a, b) => b.Length.CompareTo(a.Length)
                        );
                    }
                }));
            }


            if (possibilities.Count != 0 && recursLvl != 1)
            {
                foreach (var nextMove in possibilities)
                {
                    // the recursion
                    generateWords(_array, word + _array[nextMove.X, nextMove.Y], nextMove.X, nextMove.Y, recursLvl-1);
                }
            }
        }

        private List<IntPoint> isMovePossible(char[,] array, int x, int y)
        {
            var possibilities = new List<IntPoint>();

            for (int a = (x - 1); a <= (x + 1); a++)
            {
                for (int b = (y - 1); b <= (y + 1); b++)
                {
                    if (a >= 0 && a < 4 && b >= 0 && b < 4 && array[a,b] != '_')
                        possibilities.Add(new IntPoint(a, b));
                }
            }
            
            return possibilities;
        }

        #endregion

        #region Login/Logout Click Events

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!vm.IsBaseLoaded)
            {
                MessageBox.Show("Load the base first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            vm.InProgress = true;
            vm.InformationBox = "Logging in...";

            GameOps = new GameManagement();
            GameOps.WebActions = new SlowotokWebActions(vm.UserEmail, passwordBox.Password);
            if (await GameOps.WebActions.LogOn())
            {
                Button login = (sender as Button);
                login.Click -= LoginButton_Click;
                login.Content = "Logout";

                vm.IsLoggedIn = true;
                GameOps.BoardChanged += GameOps_BoardChanged;
                GameOps.SendAnswerGotPossible += GameOps_SendAnswerGotPossible;
                vm.InProgress = false;
                GameOps.StartAutomation();
                LoginExpander.IsExpanded = false;

                login.Click += LogoutButton_Click;
            }
            else
            {
                MessageBox.Show("Incorrect email or password!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            vm.InProgress = false;
        }

        private async void GameOps_SendAnswerGotPossible(object sender, EventArgs e)
        {
            if (vm.InProgress) return;

            var response = await GameOps.SendAnswers(vm.FoundWords.ToList().Where(x => x.IsSelected).ToList());
            MessageBox.Show("You got "
                + response.Answers.Where(x => x.Found).Sum(y => (y.Word.Length-2).Pow(2)).ToString()
                + "/" + GameOps.CurrentBoard.Points + " points!", "Congratulation", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GameOps_BoardChanged(object sender, BoardEventArgs e)
        {
            vm.ArrayOfChars = e.NewBoard.Letters.ConvertToJaggedArray(4, 4);
            validateRequirementsToProcessing(sender, new RoutedEventArgs());
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Button logout = (sender as Button);
            logout.Content = "Login";
            logout.Click -= LogoutButton_Click;

            GameOps.StopAutomation();
            GameOps.BoardChanged -= GameOps_BoardChanged;

            GameOps.Dispose();
            GameOps = null;

            vm.IsLoggedIn = false;
            logout.Click += LoginButton_Click;
        }

        #endregion
        private async void LoadTheBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (vm.InProgress) return;
            vm.InProgress = true;
            vm.InformationBox = "Loading the base (it may take a while)...";

            try
            {
                using (StreamReader file = new StreamReader(DICTIONARY_FILENAME))
                {
                    string line;

                    while ((line = await file.ReadLineAsync()) != null)
                    {
                        Dictionary.Add(line, null);
                    }
                }

                vm.IsBaseLoaded = true;
                grid.Focus();
                this.Width += 1;
                passwordBox.Focus();
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(String.Format("Not found dictionary file: \"{0}\" in program directory! Place the file and restart program!", ex.FileName) ,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Error: {0}", ex), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                vm.InProgress = false;
            }
        }

        private async void validateRequirementsToProcessing(object sender, RoutedEventArgs e)
        {
            if (!vm.IsBaseLoaded)
            {
                MessageBox.Show("Load the base first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!vm.ValidateArray())
            {
                MessageBox.Show("Fill all of the fields first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.arrayToProcess = vm.ArrayOfChars.ConvertTo2DArray(4, 4);

            await BeginProcessing();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.LastUsedEmail = vm.UserEmail;
            Properties.Settings.Default.Save();
        }
    }
}
