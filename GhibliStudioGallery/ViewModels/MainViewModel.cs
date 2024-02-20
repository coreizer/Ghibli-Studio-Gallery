#region License Information (GPL v3)

/**
 * Copyright (C) 2024 coreizer
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

#endregion

namespace GhibliStudioGallery.ViewModels
{
    using System.Diagnostics;
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using AngleSharp;
    using Avalonia.Controls;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using System.IO;
    using System.Linq;
    using AngleSharp.Dom;

    public partial class MainViewModel : ViewModelBase
    {
        private readonly HttpClient _http = new ();
        private readonly int _startIndex = 1;

        [ObservableProperty]
        public string message = "作品静止画をダウンロードする";

        [ObservableProperty]
        public int progressValue = 0;

        [ObservableProperty]
        public string filmTitle = "";

        [ObservableProperty]
        public int sizes = 0;

        public MainViewModel()
        {
            this._http.DefaultRequestHeaders.Add("User-Agent", $"GhibliStudioGallery Studio Gallery/1.0.5");
        }

        [RelayCommand]
        [Obsolete]
        public async Task GetGallery()
        {
            var OFD = new OpenFolderDialog(); // TODO: 廃止予定
            var selectedDirectory = await OFD.ShowAsync(Views.MainWindow.Instance);
            if (selectedDirectory?.Length > 0) {
                var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
                var document = await context.OpenAsync("https://www.GhibliStudioGallery.jp/works");
                var headingElement = document.QuerySelector("div.bs-docs-section"); // <h2 class="post-header">スタジオジブリ作品一覧</h2>
                var moviesElement = headingElement?.QuerySelector("div.buttons"); // <div class="buttons mt20">

                this.Sizes = moviesElement.GetElementCount() - 1;

                var movieIndex = 0;
                foreach (var movie in moviesElement?.QuerySelectorAll("a")) {
                    var sb = new StringBuilder(movie.GetAttribute("href"));
                    sb.Replace("#", "");
                    sb.Replace("-", "");

                    try {
                        this.FilmTitle = $"作品名 : {movie.TextContent}";
                        this.ProgressValue = movieIndex;
                        await this.GalleryDownload(selectedDirectory, sb.ToString(), movie.TextContent);
                    }
                    catch (Exception ex) {
                        Trace.WriteLine(ex.Message);
                    }

                    movieIndex++;
                }
            }
        }

        private async Task GalleryDownload(string selectedDirectory, string titleId, string titleName)
        {
            if (string.IsNullOrWhiteSpace(selectedDirectory)) throw new ArgumentNullException(nameof(selectedDirectory));

            var titleDir = Path.Combine(selectedDirectory, titleName);
            if (!Directory.Exists(titleDir)) {
                Directory.CreateDirectory(titleDir);
            }

            var galleryEndIndex = 50;
            var maxAttempts = 5;
            var imageFormat = ".jpg";

            switch (titleId) {
                case "kimitachi":
                    //imageFormat = ".png";
                    galleryEndIndex = 19;
                    break;

                // On Your Mark（1995）
                case "onyourmark":
                    galleryEndIndex = 28;
                    break;

                // 火垂るの墓（1988）
                // 未提供
                case "hotarunohaka":
                    galleryEndIndex = 0;
                    break;

                // アーヤと魔女（2020）
                // 未提供
                case "post_570":
                    galleryEndIndex = 0;
                    break;
            }

            foreach (var galleryIndex in Enumerable.Range(this._startIndex, galleryEndIndex)) {
                try {
                    foreach (var attempts in Enumerable.Range(0, maxAttempts)) {
                        this.FilmTitle = $"作品名 : {titleName} ({galleryIndex}/{galleryEndIndex})";
                        if (!await this.DownloadImage(titleDir, titleId, galleryIndex, imageFormat)) {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                            continue;
                        }
                        break;
                    }
                }
                catch (Exception ex) {
                    Trace.WriteLine(ex.Message);
                }
            }
        }

        private async Task<bool> DownloadImage(string titleDirectory, string titleId, int galleryIndex, string imageFormat)
        {
            var fileName = string.Concat(titleId, string.Format("{0:D3}", galleryIndex), imageFormat);
            var filePath = Path.Combine(titleDirectory, fileName);
            if (File.Exists(filePath)) return true;

            var response = await this._http.GetAsync($"https://www.GhibliStudioGallery.jp/gallery/{fileName}", HttpCompletionOption.ResponseContentRead);
            if (!response.IsSuccessStatusCode) return false;

            try {
                using var fileStream = File.Create(filePath);
                using var imageStream = await response.Content.ReadAsStreamAsync();
                imageStream.CopyTo(fileStream);
            }
            catch {
                return false;
            }

            return true;
        }
    }
}