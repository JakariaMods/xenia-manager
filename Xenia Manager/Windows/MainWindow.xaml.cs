﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Navigation;

// Imported
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Xenia_Manager.Classes;
using Xenia_Manager.Pages;

namespace Xenia_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Information on the latest version of Xenia Manager
        /// </summary>
        private UpdateInfo latestXeniaManagerRelease;

        /// <summary>
        /// Holds all of the WPF Pages that were opened at some point during the time Xenia Manager was open
        /// </summary>
        private Dictionary<string, Page> pageCache = new Dictionary<string, Page>();

        public MainWindow()
        {
            InitializeComponent();
            PageViewer.Navigated += PageViewer_Navigated;
        }

        /// <summary>
        /// Fade In animation
        /// </summary>
        private void PageViewer_Navigated(object sender, NavigationEventArgs e)
        {
            Page newPage = e.Content as Page;
            if (newPage != null)
            {
                DoubleAnimation fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.15));
                newPage.BeginAnimation(Page.OpacityProperty, fadeInAnimation);
            }
        }

        /// <summary>
        /// Check if the Page is already opened and cached and load that, otherwise load new page
        /// </summary>
        /// <param name="pageName">Name of the page user wants to navigate to</param>
        private async Task CheckForCachedPage(string pageName)
        {
            try
            {
                Log.Information($"Trying to navigate to {pageName}");
                switch (pageName)
                {
                    case "XeniaSettings":
                        // Xenia Settings Page
                        Log.Information("Checking if the Xenia Settings Page is already cached");
                        if (!pageCache.ContainsKey(pageName))
                        {
                            Log.Information("Xenia Settings Page is not cached. Loading it and caching it for future use.");
                            XeniaSettings xeniaSettings = new XeniaSettings();
                            pageCache[pageName] = xeniaSettings;
                            PageViewer.Navigate(pageCache[pageName]);
                        }
                        else
                        {
                            Log.Information("Xenia Settings Page is already cached. Loading it");
                            PageViewer.Navigate(pageCache[pageName]);
                        }
                        break;
                    case "Settings":
                        // Xenia Manager Settings Page
                        Log.Information("Checking if the Settings Page is already cached");
                        if (!pageCache.ContainsKey(pageName))
                        {
                            Log.Information("Settings Page is not cached. Loading it and caching it for future use.");
                            Settings settings = new Settings();
                            pageCache[pageName] = settings;
                            PageViewer.Navigate(pageCache[pageName]);
                        }
                        else
                        {
                            Log.Information("Settings Page is already cached. Loading it");
                            PageViewer.Navigate(pageCache[pageName]);
                        }
                        break;
                    default:
                        // Home/Library Page
                        Log.Information("Checking if the Library Page is already cached");
                        if (!pageCache.ContainsKey(pageName))
                        {
                            Log.Information("Library Page is not cached. Loading it and caching it for future use.");
                            Library library = new Library();
                            pageCache[pageName] = library;
                            PageViewer.Navigate(pageCache[pageName]);
                        }
                        else
                        {
                            Log.Information("Library Page is already cached. Loading it");
                            PageViewer.Navigate(pageCache[pageName]);
                        }
                        break;
                }
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + "\nFull Error:\n" + ex);
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Crossfade navigation to different WPF Pages
        /// </summary>
        /// <param name="pageName">Name of the page user wants to navigate to</param>
        public async Task NavigateToPage(string pageName)
        {
            if (PageViewer.Content != null)
            {
                Page currentPage = PageViewer.Content as Page;
                if (currentPage != null)
                {
                    DoubleAnimation fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.15));
                    fadeOutAnimation.Completed += async (s, a) =>
                    {
                        await CheckForCachedPage(pageName);
                    };
                    currentPage.BeginAnimation(Page.OpacityProperty, fadeOutAnimation);
                }
            }
            else
            {
                await CheckForCachedPage(pageName);
            }
        }

        /// <summary>
        /// Used for dragging the window around
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// When window loads, check for updates
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Storyboard fadeInStoryboard = ((Storyboard)Application.Current.FindResource("FadeInAnimation")).Clone();
            fadeInStoryboard.Begin(this);
            try
            {
                if (App.appConfiguration != null)
                {
                    if (App.appConfiguration.Manager.LastUpdateCheckDate == null || (DateTime.Now - App.appConfiguration.Manager.LastUpdateCheckDate.Value).TotalDays >= 1)
                    {
                        Log.Information("Checking for Xenia Manager updates");

                        using (HttpClient client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "C# HttpClient");
                            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                            HttpResponseMessage response = await client.GetAsync("https://api.github.com/repos/xenia-manager/xenia-manager/releases/latest");

                            if (response.IsSuccessStatusCode)
                            {
                                string json = await response.Content.ReadAsStringAsync();
                                JObject latestRelease = JObject.Parse(json);
                                string version = (string)latestRelease["tag_name"];
                                DateTime releaseDate;
                                DateTime.TryParseExact(latestRelease["published_at"].Value<string>(), "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out releaseDate);
                                if (version != App.appConfiguration.Manager.Version)
                                {
                                    Log.Information("Found newer version of Xenia Manager");
                                    Update.Visibility = Visibility.Visible;
                                    latestXeniaManagerRelease = new UpdateInfo();
                                    latestXeniaManagerRelease.Version = version;
                                    latestXeniaManagerRelease.ReleaseDate = releaseDate;
                                    latestXeniaManagerRelease.LastUpdateCheckDate = DateTime.Now;
                                    MessageBox.Show("Found newer version of Xenia Manager.\nClick on the Update button to update the Xenia Manager.");
                                }
                                else
                                {
                                    Log.Information("Latest version is already installed");
                                }
                            }
                            else
                            {
                                Log.Error($"Failed to retrieve releases (Status code: {response.StatusCode})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Does fade out animation before closing the window
        /// </summary>
        private async Task ClosingAnimation()
        {
            Storyboard FadeOutClosingAnimation = ((Storyboard)Application.Current.FindResource("FadeOutAnimation")).Clone();

            FadeOutClosingAnimation.Completed += (sender, e) =>
            {
                Log.Information("Closing the application");
                Environment.Exit(0);
            };

            FadeOutClosingAnimation.Begin(this);
            await Task.Delay(1);
        }

        /// <summary>
        /// Loads FadeOut animation and exits the application completely
        /// </summary>
        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            await ClosingAnimation();
        }

        /// <summary>
        /// Opens the Library page
        /// </summary>
        private async void Home_Click(object sender, RoutedEventArgs e)
        {
            //NavigateToPage(new Library());
            await NavigateToPage("Library");
        }

        /// <summary>
        /// Opens the Xenia Settings page
        /// </summary>
        private async void XeniaSettings_Click(object sender, RoutedEventArgs e)
        {
            //NavigateToPage(new XeniaSettings());
            await NavigateToPage("XeniaSettings");
        }

        /// <summary>
        /// Opens Xenia Manager Settings page
        /// </summary>
        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            //NavigateToPage(new Settings());
            await NavigateToPage("Settings");
        }

        /// <summary>
        /// Opens Xenia Manager Updater
        /// </summary>
        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            // Updating Xenia Manager info
            Log.Information("Updating info on Xenia Manager");
            App.appConfiguration.Manager.Version = latestXeniaManagerRelease.Version;
            App.appConfiguration.Manager.ReleaseDate = latestXeniaManagerRelease.ReleaseDate;
            App.appConfiguration.Manager.LastUpdateCheckDate = latestXeniaManagerRelease.LastUpdateCheckDate;

            // Updating configuration
            await App.appConfiguration.SaveAsync(AppDomain.CurrentDomain.BaseDirectory + "config.json");

            // Launching Xenia Manager Updater
            Log.Information("Launching Xenia Manager Updater");
            using (Process updater = new Process())
            {
                updater.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                updater.StartInfo.FileName = "Xenia Manager Updater.exe";
                updater.StartInfo.UseShellExecute = true;
                updater.Start();
            };

            Log.Information("Closing Xenia Manager for update");
            Environment.Exit(0);
        }
    }
}