﻿using Enterwell.Clients.Wpf.Notifications;
using Enterwell.Clients.Wpf.Notifications.Controls;
using FzLib;
using Mapster;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAPICodePack.FzExtension;
using ModernWpf.FzExtension.CommonDialog;
using Notifications.Wpf.Core;
using SimpleFFmpegGUI.Manager;
using SimpleFFmpegGUI.Model;
using SimpleFFmpegGUI.WPF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
using System.Windows.Shapes;

namespace SimpleFFmpegGUI.WPF
{
    public class PresetsPanelViewModel : INotifyPropertyChanged
    {
        public PresetsPanelViewModel()
        {
        }

        private ObservableCollection<CodePreset> presets;

        public ObservableCollection<CodePreset> Presets
        {
            get => presets;
            set => this.SetValueAndNotify(ref presets, value, nameof(Presets));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public INotificationMessageManager Manager { get; } = new NotificationMessageManager();
    }

    public partial class PresetsPanel : UserControl
    {
        public PresetsPanel()
        {
            DataContext = ViewModel;
            InitializeComponent();
        }

        public PresetsPanelViewModel ViewModel { get; } = App.ServiceProvider.GetService<PresetsPanelViewModel>();
        private TaskType type;

        public void Update(TaskType type)
        {
            this.type = type;
            ViewModel.Presets = new ObservableCollection<CodePreset>(PresetManager.GetPresets().Where(p => p.Type == type));
        }

        public CodeArgumentsPanelViewModel CodeArgumentsViewModel { get; set; }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(CodeArgumentsViewModel != null);
            var name = await CommonDialog.ShowInputDialogAsync("请输入新预设的名称");
            if (name == null)
            {
                return;
            }
            try
            {
                var preset = PresetManager.AddPreset(name, type, CodeArgumentsViewModel.GetArguments());
                ViewModel.Presets.Add(preset);
            }
            catch (Exception ex)
            {
                this.CreateMessage().QueueError("新增预设失败", ex);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(CodeArgumentsViewModel != null);
            var preset = (sender as FrameworkElement).Tag as CodePreset;
            CodeArgumentsViewModel.Update(type, preset.Arguments);
            this.CreateMessage().QueueSuccess($"已加载预设“{preset.Name}”");
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(CodeArgumentsViewModel != null);
            var preset = (sender as FrameworkElement).Tag as CodePreset;
            try
            {
                preset.Arguments = CodeArgumentsViewModel.GetArguments();
                this.CreateMessage().QueueSuccess($"预设“{preset.Name}”更新成功");
            }
            catch (Exception ex)
            {
                this.CreateMessage().QueueError("更新预设失败", ex);
            }
        }
    }

    public static class NotificationMessageExtension
    {
        public static void QueueSuccess(this NotificationMessageBuilder builder, string message)
        {
            builder.Background(Brushes.Green)
                       .HasMessage(message)
                       .Animates(true)
                       .Dismiss().WithDelay(2000)
                       .Queue();
        }

        public static void QueueError(this NotificationMessageBuilder builder, string message, Exception ex)
        {
            builder.Background("#B71C1C")
                       .HasMessage(message)
                       .Animates(true)
                       .Dismiss().WithDelay(5000)
                       .Dismiss().WithButton("详情", b => CommonDialog.ShowErrorDialogAsync(ex))
                       .Queue();
        }

        public static NotificationMessageBuilder CreateMessage(this DependencyObject element)
        {
            var window = Window.GetWindow(element);
            if (window == null)
            {
                throw new Exception("找不到元素的窗口");
            }
            if (!(window.Content is Grid))
            {
                throw new Exception("窗口的内容不是Grid");
            }
            Grid grid = window.Content as Grid;

            NotificationMessageContainer container;
            if (grid.Children.OfType<NotificationMessageContainer>().Any())
            {
                container = grid.Children.OfType<NotificationMessageContainer>().First();
            }
            else
            {
                container = new NotificationMessageContainer
                {
                    Margin = new Thickness(-grid.Margin.Left, -grid.Margin.Top, -grid.Margin.Right, -grid.Margin.Bottom),
                    Width = 360,
                    Manager = new NotificationMessageManager()
                };
                Grid.SetRowSpan(container, int.MaxValue);
                Grid.SetColumnSpan(container, int.MaxValue);
                grid.Children.Add(container);
            }
            return container.Manager.CreateMessage();
        }
    }
}