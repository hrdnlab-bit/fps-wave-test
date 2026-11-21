using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Copia i messaggi della Console dell'Editor in Logs/FpsWaveConsole.log (cartella ignorata da git),
    /// così errori e warning si possono leggere senza guardare la finestra Console.
    /// Il file viene azzerato a ogni ricaricamento del dominio (dopo ogni compilazione).
    /// Gli errori di compilazione vengono registrati tramite CompilationPipeline.
    /// </summary>
    [InitializeOnLoad]
    internal static class ConsoleLogMirror
    {
        private const string LogPath = "Logs/FpsWaveConsole.log";
        private static readonly object Lock = new();

        static ConsoleLogMirror()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(LogPath, $"# FpsWave console mirror - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        }

        private static void OnAssemblyCompiled(string assembly, UnityEditor.Compilation.CompilerMessage[] messages)
        {
            foreach (var message in messages)
            {
                if (message.type == UnityEditor.Compilation.CompilerMessageType.Error)
                    Append("CompileError", $"{message.file}({message.line}): {message.message}", null);
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Lo stack trace solo per errori ed eccezioni, per tenere il file leggibile.
            bool withStack = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            Append(type.ToString(), condition, withStack ? stackTrace : null);
        }

        private static void Append(string type, string message, string stackTrace)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [{type}] {message}{Environment.NewLine}";
            if (!string.IsNullOrEmpty(stackTrace))
                line += stackTrace + Environment.NewLine;
            lock (Lock)
            {
                try { File.AppendAllText(LogPath, line); }
                catch (IOException) { /* file momentaneamente bloccato: si perde una riga, non è critico */ }
            }
        }
    }
}
