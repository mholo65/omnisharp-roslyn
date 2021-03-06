﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    [Export, Shared]
    public class DotNetCliService
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private readonly ConcurrentDictionary<string, object> _locks;
        private readonly SemaphoreSlim _semaphore;

        private string _dotnetPath = "dotnet";

        public string DotNetPath => _dotnetPath;

        [ImportingConstructor]
        public DotNetCliService(ILoggerFactory loggerFactory, IEventEmitter eventEmitter)
        {
            this._logger = loggerFactory.CreateLogger<DotNetCliService>();
            this._eventEmitter = eventEmitter;
            this._locks = new ConcurrentDictionary<string, object>();
            this._semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        private static void RemoveMSBuildEnvironmentVariables(IDictionary<string, string> environment)
        {
            // Remove various MSBuild environment variables set by OmniSharp to ensure that
            // the .NET CLI is not launched with the wrong values.
            environment.Remove("MSBUILD_EXE_PATH");
            environment.Remove("MSBuildExtensionsPath");
            environment.Remove("MSBuildSDKsPath");
        }

        public void SetDotNetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "dotnet";
            }

            if (string.Equals(_dotnetPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _dotnetPath = path;

            _logger.LogInformation($"DotNetPath set to {_dotnetPath}");

        }

        public Task RestoreAsync(string workingDirectory, string arguments = null, Action onFailure = null)
        {
            return Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Begin dotnet restore in '{workingDirectory}'");

                var restoreLock = _locks.GetOrAdd(workingDirectory, new object());
                lock (restoreLock)
                {
                    var exitStatus = new ProcessExitStatus(-1);
                    _eventEmitter.RestoreStarted(workingDirectory);
                    _semaphore.Wait();
                    try
                    {
                        // A successful restore will update the project lock file which is monitored
                        // by the dotnet project system which eventually update the Roslyn model
                        exitStatus = ProcessHelper.Run(_dotnetPath, $"restore {arguments}", workingDirectory, updateEnvironment: RemoveMSBuildEnvironmentVariables);
                    }
                    finally
                    {
                        _semaphore.Release();

                        _locks.TryRemove(workingDirectory, out _);

                        _eventEmitter.RestoreFinished(workingDirectory, exitStatus.Succeeded);

                        if (exitStatus.Failed && onFailure != null)
                        {
                            onFailure();
                        }

                        _logger.LogInformation($"Finish restoring project {workingDirectory}. Exit code {exitStatus}");
                    }
                }
            });
        }

        public Process Start(string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo(_dotnetPath, arguments)
            {
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            return Process.Start(startInfo);
        }
    }
}
