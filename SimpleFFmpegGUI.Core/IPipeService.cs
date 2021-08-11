﻿using FFMpegCore;
using FFMpegCore.Enums;
using SimpleFFmpegGUI.Dto;
using SimpleFFmpegGUI.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SimpleFFmpegGUI
{
    public interface IPipeService
    {
        void CreateCodeTask(IEnumerable<string> path, string outputPath, CodeArguments arg, bool start);

        MediaInfoDto GetInfo(string path);

        string GetLastOutput();

        void Join(IEnumerable<string> path);

        void PauseQueue();

        void CancelQueue();

        void ResumeQueue();

        void StartQueue();

        List<TaskInfo> GetTasks(TaskStatus? status = null, int skip = 0, int take = 0);

        public StatusDto GetStatus();

        void ResetTask(int id);
    }
}