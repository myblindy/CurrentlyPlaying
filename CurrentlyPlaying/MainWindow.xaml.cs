using ReactiveUI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace CurrentlyPlaying
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        string? artist, songTitle, album;
        ImageSource? imageSource;

        public string? Artist { get => artist; set { artist = value; RaisePropertyChanged(); } }
        public string? SongTitle { get => songTitle; set { songTitle = value; RaisePropertyChanged(); } }
        public string? Album { get => album; set { album = value; RaisePropertyChanged(); } }
        public ImageSource? ImageSource { get => imageSource; set { imageSource = value; RaisePropertyChanged(); } }

        public MainWindow()
        {
            DataContext = this;
            _ = InitializeMediaListener();
            InitializeComponent();

            this.WhenAnyValue(x => x.SongTitle).ObserveOn(RxApp.MainThreadScheduler).Subscribe(async songTitle =>
            {
                if (songTitle is null)
                    Opacity = 0;
                else
                {
                    BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.5)));
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.5)));
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                    BeginAnimation(OpacityProperty, null);
                }
            });
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            var screen = Screen.PrimaryScreen!;
            Left = screen.Bounds.Right - sizeInfo.NewSize.Width - 20;
            Top = screen.Bounds.Bottom - sizeInfo.NewSize.Height - 20;
        }

        private async Task InitializeMediaListener()
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            GlobalSystemMediaTransportControlsSession? currentSession = default;
            void updateCurrentSession()
            {
                if (currentSession is not null)
                    currentSession.MediaPropertiesChanged -= currentMediaPropertiesChanged;

                currentSession = manager.GetCurrentSession();
                currentSession.MediaPropertiesChanged += currentMediaPropertiesChanged;
                currentMediaPropertiesChanged(currentSession, null!);
            }
            manager.CurrentSessionChanged += (manager, e) => updateCurrentSession();
            updateCurrentSession();

            async void currentMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
            {
                if (await session.TryGetMediaPropertiesAsync() is { } mediaProperties)
                {
                    BitmapImage? bitmap = null;
                    if (mediaProperties.Thumbnail is not null)
                        using (var thumbnailStream = await mediaProperties.Thumbnail.OpenReadAsync())
                        {
                            using var ms = new MemoryStream();
                            await thumbnailStream.AsStream().CopyToAsync(ms);
                            ms.Position = 0;

                            bitmap = new();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();

                            bitmap.Freeze();
                        }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        (Artist, SongTitle, Album, ImageSource) = (mediaProperties.Artist, mediaProperties.Title, mediaProperties.AlbumTitle, bitmap));
                }
            }
        }

        public void RaisePropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));
    }
}
