﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleFFmpegGUI.Manager
{
    public class FFmpegProcess
    {
        private readonly Process process = new Process();

        public int Id
        {
            get
            {
                if (!started)
                {
                    throw new Exception("进程还未开始运行");
                }
                return process.Id;
            }
        }

        public FFmpegProcess(string argument)
        {
            process.StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = argument,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_OutputDataReceived;
        }

        private bool started = false;

        public Task StartAsync(string workingDir, CancellationToken? cancellationToken)
        {
            if (started)
            {
                throw new Exception("已经开始运行，不可再次运行");
            }
            started = true;

            if(!string.IsNullOrEmpty(workingDir))
            {           
                //2Pass时会生成文件名相同的临时文件，如果多个FFmpeg一起运行会冲突，因此需要设置单独的工作目录
                process.StartInfo.WorkingDirectory = workingDir;
            }
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            bool exit = false;
            cancellationToken?.Register(() =>
            {
                if (!exit)
                {
                    exit = true;
                    process.Kill();
                }
            });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.Exited += async (s, e) =>
             {
                 exit = true;
                 try
                 {
                     await Task.Delay(1000);
                     if (process.ExitCode == 0)
                     {
                         tcs.SetResult(true);
                     }
                     else if (exit)
                     {
                         tcs.SetException(new TaskCanceledException("进程被取消"));
                     }
                     else
                     {
                         tcs.SetException(new Exception($"进程退出返回错误退出码：" + process.ExitCode));
                     }
                     await Task.Delay(10000);
                     process.Dispose();
                 }
                 catch (Exception ex)
                 {
                     tcs.SetException(new Exception($"进程处理程序发生错误：" + ex.Message, ex));
                 }
             };
            return tcs.Task;
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            Output?.Invoke(this, new FFmpegOutputEventArgs(e.Data));
        }

        public event EventHandler<FFmpegOutputEventArgs> Output;
    }
}