﻿using FzLib;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using ModernWpf.FzExtension.CommonDialog;
using SimpleFFmpegGUI.Manager;
using SimpleFFmpegGUI.Model;
using SimpleFFmpegGUI.WPF;
using SimpleFFmpegGUI.WPF.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace SimpleFFmpegGUI.WPF.Panels
{
    public class TaskListViewModel : INotifyPropertyChanged
    {
        public TaskListViewModel(QueueManager queue)
        {
            Queue = queue;
            Tasks.Refresh();
        }

        public TasksAndStatuses Tasks => App.ServiceProvider.GetService<TasksAndStatuses>();

        public QueueManager Queue { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class TaskList : UserControl
    {
        public TaskList()
        {
            DataContext = ViewModel;
            InitializeComponent();
        }

        public TaskListViewModel ViewModel { get; }= App.ServiceProvider.GetService<TaskListViewModel>();

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var task = (sender as FrameworkElement).DataContext as UITaskInfo;
            Debug.Assert(task != null);
            TaskManager.CancelTask(task.Id, ViewModel.Queue);
            task.UpdateSelf();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            bool delete = true;
            if (App.ServiceProvider.GetService<TasksAndStatuses>().SelectedTask.Status == SimpleFFmpegGUI.Model.TaskStatus.Processing)
            {
                delete = await CommonDialog.ShowYesNoDialogAsync("删除", "任务正在处理，是否删除？");
            }
            if (delete)
            {
                var task = (sender as FrameworkElement).DataContext as UITaskInfo;
                Debug.Assert(task != null);
                TaskManager.DeleteTask(task.Id, ViewModel.Queue);
                task.UpdateSelf();
            }
        }

        private void CloneButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var task = (sender as FrameworkElement).DataContext as UITaskInfo;
            Debug.Assert(task != null);
            TaskManager.ResetTask(task.Id, ViewModel.Queue);
            task.UpdateSelf();
        }


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var task = (sender as FrameworkElement).DataContext as UITaskInfo;
                Debug.Assert(task != null);
                ViewModel.Queue.StartStandalone(task.Id);
                task.UpdateSelf();
            }
            catch (Exception ex)
            {
                this.CreateMessage().QueueError("启动失败", ex);
            }
        }
    }
}