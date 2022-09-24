﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenshinWoodmen.Core;
using GenshinWoodmen.Models;
using GenshinWoodmen.Views;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GenshinWoodmen.ViewModels
{
    public class MainViewModel : ObservableRecipient, IRecipient<CountMessage>, IRecipient<StatusMessage>, IRecipient<CancelShutdownMessage>
    {
        public MainWindow Source { get; set; } = null!;
        public double Left => SystemParameters.WorkArea.Right;
        public double Top => SystemParameters.WorkArea.Bottom;

        public int ForecastX3 => (int)(((Settings.DelayLogin / 1000d) + (Settings.DelayLaunch / 1000d) + 7d) * (2000d / 3d) / 60d);
        public int ForecastX6 => (int)Math.Floor(ForecastX3 / 2d);
        public int ForecastX9 => (int)Math.Floor(ForecastX3 / 3d);
        public int ForecastX12 => (int)Math.Floor(ForecastX3 / 4d);
        public int ForecastX15 => (int)Math.Floor(ForecastX3 / 5d);
        public int ForecastX18 => (int)Math.Floor(ForecastX3 / 6d);

        public int ForecastX3Count => (int)Math.Floor(2000d / 3d);
        public int ForecastX6Count => (int)Math.Floor(2000d / 6d);
        public int ForecastX9Count => (int)Math.Floor(2000d / 9d);
        public int ForecastX12Count => (int)Math.Floor(2000d / 12d);
        public int ForecastX15Count => (int)Math.Floor(2000d / 15d);
        public int ForecastX18Count => (int)Math.Floor(2000d / 18d);

        protected int currentCount = 0;
        public int CurrentCount
        {
            get => currentCount;
            internal set => SetProperty(ref currentCount, value);
        }

        protected int maxCount = 0;
        public int MaxCount
        {
            get => maxCount;
            set => SetProperty(ref maxCount, value);
        }

        protected string currentStatus = null!;
        public string CurrentStatus
        {
            get => currentStatus;
            internal set => SetProperty(ref currentStatus, value);
        }

        protected bool powerOffAuto = false;
        public bool PowerOffAuto
        {
            get => powerOffAuto;
            set => SetProperty(ref powerOffAuto, value);
        }

        protected int powerOffAutoMinuteByUser = 0;
        protected int powerOffAutoMinute = 0;
        public int PowerOffAutoMinute
        {
            get => powerOffAutoMinute;
            set => SetProperty(ref powerOffAutoMinute, value);
        }

        public DateTime? PowerOffDateTime { get; protected set; } = null!;
        public bool PowerOffNotified { get; protected set; } = false;

        public AutoMuteSelection AutoMute
        {
            get => (AutoMuteSelection)Settings.AutoMute.Get();
            set
            {
                if (value == AutoMuteSelection.AutoMuteNone) return;
                AutoMuteSelection prev = AutoMute;
                Broadcast(prev, value, nameof(AutoMute));
                Settings.AutoMute.Set((int)value);
                SettingsManager.Save();
            }
        }

        [Obsolete]
        public short MonitorBrightness
        {
            get => NativeMethods.GetMonitorBrightness();
            set => NativeMethods.SetMonitorBrightness(value);
        }
        [Obsolete] public short MonitorMinimumBrightness => NativeMethods.GetMonitorMinimumBrightness();
        [Obsolete] public short MonitorMaximumBrightness => NativeMethods.GetMonitorMaximumBrightness();

        protected byte brightness = NativeMethods.GetBrightness();
        public byte Brightness
        {
            get => brightness;
            set => SetProperty(ref brightness, value);
        }

        public ICommand StartCommand => new RelayCommand<Button>(async button =>
        {
            TextBlock startIcon = (Window.GetWindow(button).FindName("TextBlockStartIcon") as TextBlock)!;
            TextBlock start = (Window.GetWindow(button).FindName("TextBlockStart") as TextBlock)!;

            Brush brush = null!;
            button!.IsEnabled = false;
            if (startIcon.Text.Equals(FluentSymbol.Start))
            {
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEDBE8F6"));
                start.Text = Mui("ButtonStop");
                startIcon.Text = FluentSymbol.Stop;
                JiggingProcessor.Start();
            }
            else
            {
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEFFFFFF"));
                start.Text = Mui("ButtonStart");
                startIcon.Text = FluentSymbol.Start;
                JiggingProcessor.Stop();
            }

            Border border = (Window.GetWindow(button).FindName("Border") as Border)!;
            StoryboardUtils.BeginBrushStoryboard(border, new Dictionary<DependencyProperty, Brush>()
            {
                [Border.BackgroundProperty] = brush,
            });
            await Task.Delay(1000);
            button!.IsEnabled = true;
        });

        public ICommand ConfigOpenCommand => new RelayCommand(async () =>
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{SettingsManager.Path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch
            {
            }
        });

        public ICommand ConfigOpenWithCommand => new RelayCommand(async () =>
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo()
                {
                    FileName = "openwith.exe",
                    Arguments = $"\"{SettingsManager.Path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch
            {
            }
        });

        public ICommand ConfigReloadCommand => new RelayCommand(async () =>
        {
            SettingsManager.Reinit();
            Broadcast(ForecastX3, ForecastX3, nameof(ForecastX3));
            Broadcast(ForecastX6, ForecastX6, nameof(ForecastX6));
            Broadcast(ForecastX9, ForecastX9, nameof(ForecastX9));
            Broadcast(ForecastX12, ForecastX12, nameof(ForecastX12));
            Broadcast(ForecastX15, ForecastX15, nameof(ForecastX15));
            SetupLanguage();
        });

        public ICommand ClearCountCommand => new RelayCommand(() => MaxCount = CurrentCount = 0);
        public ICommand TopMostCommand => new RelayCommand<Window>(async app =>
        {
            app!.Topmost = !app.Topmost;
            if (app.FindName("TextBlockTopMost") is TextBlock topMostIcon)
            {
                topMostIcon.Text = app.Topmost ? FluentSymbol.Unpin : FluentSymbol.Pin;
            }
        });
        public ICommand RestorePosCommand => new RelayCommand<Window>(async app =>
        {
            app!.Left = Left - app.Width;
            app!.Top = Top - app.Height;
        });
        public ICommand RestartCommand => NotifyIconViewModel.RestartCommand;
        public ICommand ExitCommand => NotifyIconViewModel.ExitCommand;
        public ICommand UsageCommand => NotifyIconViewModel.UsageCommand;
        public ICommand UsageImageSingleCommand => NotifyIconViewModel.UsageImageSingleCommand;
        public ICommand UsageImageMultiCommand => NotifyIconViewModel.UsageImageMultiCommand;
        public ICommand GitHubCommand => NotifyIconViewModel.GitHubCommand;

        public ICommand MuteGameCommand  => new RelayCommand(async () => await MuteManager.MuteGameAsync(true));
        public ICommand UnmuteGameCommand => new RelayCommand(async () => await MuteManager.MuteGameAsync(false));
        public ICommand MuteSystemCommand => new RelayCommand(() => MuteManager.MuteSystem(true));
        public ICommand UnmuteSystemCommand => new RelayCommand(() => MuteManager.MuteSystem(false));

        public MainViewModel(MainWindow source)
        {
            Source = source;

            Source.Loaded += (_, _) =>
            {
                Source.BrightnessSlider.IsVisibleChanged += (_, _) =>
                {
                    Brightness = NativeMethods.GetBrightness();
                };
                Source.BrightnessSlider.ValueChanged += (_, _) =>
                {
                    if (Source.BrightnessSlider.IsVisible)
                        NativeMethods.SetBrightness(Brightness);
                };
            };

            WeakReferenceMessenger.Default.RegisterAll(this);
            GC.KeepAlive(new PeriodProcessor(() =>
            {
                if (CurrentCount > 0 && MaxCount > 0 && CurrentCount >= MaxCount)
                {
                    if (!JiggingProcessor.IsCanceled)
                    {
                        Source.Dispatcher.Invoke(() =>
                        {
                            StartCommand?.Execute(Source.FindName("ButtonStart"));
                        });
                        NoticeService.AddNotice(Mui("Tips"), string.Format(Mui("CountReachStop"), MaxCount), string.Empty, ToastDuration.Short);
                    }
                }
                if (PowerOffAuto && PowerOffDateTime != null && powerOffAutoMinuteByUser > 0)
                {
                    TimeSpan timeOffset = (PowerOffDateTime - DateTime.Now).Value;

                    if (!PowerOffNotified && (PowerOffAutoMinute = (int)timeOffset.TotalMinutes) <= 3)
                    {
                        NoticeService.AddNoticeWithButton(Mui("Tips"), string.Format(Mui("PowerOffTips1"), Math.Round(timeOffset.TotalMinutes)), Mui("Cancel"), ("timetoshutdown", "cancel"), ToastDuration.Long);
                        PowerOffNotified = true;
                    }
                    if (timeOffset.TotalSeconds <= 0)
                    {
                        NoticeService.ClearNotice();
                        NativeMethods.ShutdownPowerOff();
                    }
                }
            }));

            PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(PowerOffAuto):
                        if (PowerOffAuto)
                        {
                            if (PowerOffAutoMinute == 0)
                            {
                                PowerOffAuto = false;
                                return;
                            }
                            PowerOffDateTime = DateTime.Now.AddMinutes(PowerOffAutoMinute);
                            powerOffAutoMinuteByUser = PowerOffAutoMinute;
                        }
                        else
                        {
                            PowerOffDateTime = null;
                        }
                        PowerOffNotified = false;
                        break;
                }
            };

            SettingsManager.Reloaded += RegisterHotKey;
            RegisterHotKey();
        }

        private void RegisterHotKey()
        {
            try
            {
                HotkeyHolder.RegisterHotKey(Settings.ShortcutKey, (s, e) =>
                {
                    StartCommand?.Execute(Source.FindName("ButtonStart"));
                });
            }
            catch (Exception e)
            {
                Logger.Exception(e);
            }
        }

        void IRecipient<CountMessage>.Receive(CountMessage message) => CurrentCount++;
        void IRecipient<StatusMessage>.Receive(StatusMessage message) => CurrentStatus = message.ToString();
        void IRecipient<CancelShutdownMessage>.Receive(CancelShutdownMessage message)
        {
            PowerOffAuto = false;
            NoticeService.ClearNotice();
        }
    }
}
