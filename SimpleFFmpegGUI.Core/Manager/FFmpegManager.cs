﻿using System;
using System.Collections.Generic;
using Instances;
using SimpleFFmpegGUI.Dto;
using SimpleFFmpegGUI.Model;
using System.IO;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using Tasks = System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using FzLib.IO;
using SimpleFFmpegGUI.FFMpegArgumentExtension;
using System.Threading;

namespace SimpleFFmpegGUI.Manager
{
    public class FFmpegManager
    {
        public ProgressDto Progress { get; private set; }

        private ProgressDto GetProgress(TaskInfo task)
        {
            var p = new ProgressDto();
            if (task.Inputs.Count == 1)
            {
                try
                {
                    if (task.Arguments != null && task.Arguments.Input != null && task.Arguments.Input.From.HasValue && task.Arguments.Input.To.HasValue)
                    {
                        p.VideoLength = TimeSpan.FromSeconds(task.Arguments.Input.To.Value - task.Arguments.Input.From.Value);
                    }
                    else
                    {
                        p.VideoLength = FFProbe.Analyse(task.Inputs[0]).Duration;
                    }
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                try
                {
                    if (task.Arguments != null && task.Arguments.Input != null && task.Arguments.Input.From.HasValue && task.Arguments.Input.To.HasValue)
                    {
                        p.VideoLength = TimeSpan.FromSeconds(task.Arguments.Input.To.Value - task.Arguments.Input.From.Value);
                    }
                    else
                    {
                        p.VideoLength = TimeSpan.FromTicks(task.Inputs.Select(p => FFProbe.Analyse(p).Duration.Ticks).Sum());
                    }
                }
                catch
                {
                    return null;
                }
            }
            p.StartTime = DateTime.Now;
            return p;
        }

        private ProgressDto GetConvertToTsProgress(string file)
        {
            var p = new ProgressDto();
            try
            {
                p.VideoLength = FFProbe.Analyse(file).Duration;
                p.StartTime = DateTime.Now;
                return p;
            }
            catch
            {
                return null;
            }
        }

        private async Tasks.Task<List<string>> ConvertToTsAsync(IEnumerable<string> files, string tempFolder, CancellationToken cancellationToken)
        {
            List<string> tsFiles = new List<string>();
            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }
            int i = 0;
            foreach (var file in files)
            {
                Progress = GetConvertToTsProgress(file);
                string path = FileSystem.GetNoDuplicateFile(Path.Combine(tempFolder, "join.ts"));
                tsFiles.Add(path);
                var p = FFMpegArguments.FromFileInput(file)
                          .OutputToFile(path, true, p =>
                              p.CopyChannel()
                              .WithBitStreamFilter(Channel.Video, Filter.H264_Mp4ToAnnexB)
                              .ForceFormat(VideoType.Ts));
                await RunAsync(p, $"打包第{++i}个临时文件", cancellationToken);
            }
            return tsFiles;
        }

        private async Task CodeProcessAsync(TaskInfo task, string tempDir, CancellationToken cancellationToken)
        {
            FFMpegArguments f = null;
            string message = null;
            if (task.Inputs.Count() == 1)
            {
                f = FFMpegArguments.FromFileInput(task.Inputs.First(), true, a => ApplyInputArguments(a, task.Arguments));

                Progress = GetProgress(task);
                message = $"正在转码：{Path.GetFileName(task.Inputs[0])}";
            }
            else
            {
                var tsFiles = await ConvertToTsAsync(task.Inputs, tempDir, cancellationToken);
                f = FFMpegArguments.FromConcatInput(tsFiles, a => ApplyInputArguments(a, task.Arguments));
                Progress = GetProgress(task);
                message = $"正在合并：{Path.GetFileName(task.Inputs[0])}等";
            }
            task.Output = GetFileName(task.Arguments, task.Output);
            var p = f.OutputToFile(task.Output, true, a => ApplyOutputArguments(a, task.Arguments));
            await RunAsync(p, message, cancellationToken);
        }

        private async Task CombineProcessAsync(TaskInfo task, CancellationToken cancellationToken)
        {
            if (task.Inputs.Count() != 2)
            {
                throw new ArgumentException("合并音视频操作，输入文件必须为2个");
            }

            FFMpegArguments f = FFMpegArguments
                .FromFileInput(task.Inputs[0])
                .AddFileInput(task.Inputs[1]);

            Progress = GetProgress(task);

            task.Output = GetFileName(task.Arguments, task.Output);
            var p = f.OutputToFile(task.Output, true, a =>
            {
                a.CopyChannel(Channel.Both);
                if (task.Arguments?.Combine?.Shortest ?? false)
                {
                    a.UsingShortest(true);
                }
            });
            await RunAsync(p, "正在合并音视频", cancellationToken);
        }

        public async Task StartNewAsync(TaskInfo task, CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info(task, "开始任务");
                string tempDir = Path.Combine(Path.GetDirectoryName(task.Output), "temp");
                if (!task.Inputs.Any())
                {
                    throw new ArgumentException("没有输入文件");
                }

                await (task.Type switch
                {
                    TaskType.Code => CodeProcessAsync(task, tempDir, cancellationToken),
                    TaskType.Combine => CombineProcessAsync(task, cancellationToken),
                    _ => throw new NotSupportedException("不支持的任务类型：" + task.Type),
                });

                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Logger.Info(task, "完成任务");
            }
            finally
            {
                Progress = null;
            }
        }

        private Task RunAsync(FFMpegArgumentProcessor processor, string desc, CancellationToken cancellationToken)
        {
            Logger.Info("FFmpeg参数为：" + processor.Arguments);
            Progress.Name = desc;
            return processor
                .CancellableThrough(cancellationToken)
                .NotifyOnOutput(Output)
                .ProcessAsynchronously();
        }

        private void Output(string data, DataType type)
        {
            Logger.Output(data);
            FFmpegOutput?.Invoke(this, new FFmpegOutputEventArgs(type == Instances.DataType.Error, data));
        }

        private void ApplyInputArguments(FFMpegArgumentOptions fa, CodeArguments a)
        {
            if (a.Input == null)
            {
                return;
            }
            if (a.Input.From.HasValue && a.Input.To.HasValue)
            {
                fa.Seek(TimeSpan.FromSeconds(a.Input.From.Value))
                    .WithDuration(TimeSpan.FromSeconds(a.Input.To.Value - a.Input.From.Value));
            }
        }

        private void ApplyOutputArguments(FFMpegArgumentOptions fa, CodeArguments a)
        {
            if (a.DisableVideo && a.DisableAudio)
            {
                throw new Exception("不能同时禁用视频和音频");
            }
            if (a.Video == null && a.Audio == null && !a.DisableAudio && !a.DisableVideo)
            {
                fa.CopyChannel(Channel.Both);
                return;
            }
            if (a.DisableVideo)
            {
                a.Video = null;
            }
            if (a.DisableAudio)
            {
                a.Audio = null;
            }

            if (a.Video == null)
            {
                if (a.DisableVideo)
                {
                    fa.DisableChannel(Channel.Video);
                }
                else
                {
                    fa.CopyChannel(Channel.Video);
                }
            }
            if (a.Audio == null)
            {
                if (a.DisableAudio)
                {
                    fa.DisableChannel(Channel.Audio);
                }
                else
                {
                    fa.CopyChannel(Channel.Audio);
                }
            }

            if (a.Video != null)
            {
                Codec code = a.Video.Code.ToLower().Replace(".", "") switch
                {
                    "h265" => FFMpeg.GetCodec("libx265"),
                    "h264" => FFMpeg.GetCodec("libx264"),
                    "vp9" => FFMpeg.GetCodec("libvpx-vp9"),
                    _ => throw new NotSupportedException("不支持的格式：" + a.Video.Code)
                };
                fa.WithVideoCodec(code);
                fa.WithSpeedPreset((Speed)a.Video.Preset);
                if (a.Video.Crf.HasValue)
                {
                    fa.WithConstantRateFactor(a.Video.Crf.Value);
                }
                if (a.Video.Fps.HasValue)
                {
                    fa.WithFramerate(a.Video.Fps.Value);
                }
                if (a.Video.AverageBitrate.HasValue)
                {
                    fa.WithVideoMBitrate(a.Video.AverageBitrate.Value);
                }
                if (a.Video.MaxBitrate.HasValue && a.Video.MaxBitrateBuffer.HasValue)
                {
                    fa.WithVideoMMaxBitrate(a.Video.MaxBitrate.Value, a.Video.MaxBitrateBuffer.Value);
                }
                if (a.Video.Width.HasValue && a.Video.Height.HasValue)
                {
                    fa.WithVideoFilters(o =>
                    {
                        if (a.Video.Width.HasValue && a.Video.Height.HasValue)
                        {
                            o.Scale(a.Video.Width.Value, a.Video.Height.Value);
                        }
                    });
                }
            }
            if (a.Audio != null)
            {
                Codec code = a.Audio.Code.ToLower().Replace(".", "") switch
                {
                    "aac" => FFMpeg.GetCodec("aac"),
                    "ac3" => FFMpeg.GetCodec("ac3"),
                    _ => throw new NotSupportedException("不支持的格式：" + a.Video.Code)
                };
                fa.WithAudioCodec(code);
                if (a.Audio.Bitrate.HasValue)
                {
                    fa.WithAudioBitrate(a.Audio.Bitrate.Value);
                }
                if (a.Audio.SamplingRate.HasValue)
                {
                    fa.WithAudioSamplingRate(a.Audio.SamplingRate.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(a.Extra))
            {
                fa.WithArguments(a.Extra);
            }
            if (!string.IsNullOrWhiteSpace(a.Format))
            {
                fa.ForceFormat(a.Format);
            }
        }

        private string GetFileName(CodeArguments a, string output)
        {
            string result = output;
            if (!string.IsNullOrEmpty(a?.Format))
            {
                VideoFormat format = VideoFormat.Formats.Where(p => p.Name == a.Format || p.Extension == a.Format).FirstOrDefault();
                if (format != null)
                {
                    string dir = Path.GetDirectoryName(output);
                    string name = Path.GetFileNameWithoutExtension(output);
                    string extension = format.Extension;
                    result = Path.Combine(dir, name + "." + extension);
                }
            }
            return FileSystem.GetNoDuplicateFile(result);
        }

        public event EventHandler<FFmpegOutputEventArgs> FFmpegOutput;
    }
}