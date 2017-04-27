﻿using System;
using OmniSharp.Models.Events;

namespace OmniSharp.Eventing
{
    public static class IEventEmitterExtensions
    {
        public static void Error(this IEventEmitter emitter, Exception ex, string fileName = null)
        {
            emitter.Emit(
                EventTypes.Error,
                new ErrorMessage { FileName = fileName, Text = ex.ToString() });
        }

        public static void RestoreStarted(this IEventEmitter emitter, string projectPath)
        {
            emitter.Emit(
                EventTypes.PackageRestoreStarted,
                new PackageRestoreMessage { FileName = projectPath });
        }

        public static void RestoreFinished(this IEventEmitter emitter, string projectPath, bool succeeded)
        {
            emitter.Emit(
                EventTypes.PackageRestoreFinished,
                new PackageRestoreMessage
                {
                    FileName = projectPath,
                    Succeeded = succeeded
                });
        }
    }
}
