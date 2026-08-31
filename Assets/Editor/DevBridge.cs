#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace DungeonCrawler.EditorTools
{
    // Writes compile results and Play-mode console output to plain files under Logs/,
    // instead of relying on grepping Unity's own global Editor.log (which mixes editor UI
    // noise with actual output and doesn't cleanly separate one Play session from the
    // next). Two attempts at third-party Unity MCP servers both turned out to hard-require
    // a newer Unity version than this project is on despite their docs claiming otherwise
    // (see project history) -- this gets most of the actual value (fast compile-status
    // checks, and crucially, visibility into exceptions thrown during Play-mode testing)
    // with zero external dependencies and zero version-compatibility risk, since it's built
    // entirely from CompilationPipeline and Application.logMessageReceived, both present
    // since long before Unity 2020.3.
    [InitializeOnLoad]
    public static class DevBridge
    {
        private static readonly string LogDir = Path.Combine(Application.dataPath, "..", "Logs");
        private static readonly string CompileLogPath = Path.Combine(LogDir, "compile-status.log");
        private static readonly string ConsoleLogPath = Path.Combine(LogDir, "play-console.log");

        private static readonly StringBuilder pendingErrors = new StringBuilder();

        static DevBridge()
        {
            Directory.CreateDirectory(LogDir);
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            Application.logMessageReceived += OnLogMessage;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnCompilationStarted(object obj)
        {
            pendingErrors.Clear();
            SafeWrite(CompileLogPath, $"[{DateTime.Now:HH:mm:ss}] Compiling...\n");
        }

        private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                if (m.type == CompilerMessageType.Error)
                    pendingErrors.AppendLine($"{Path.GetFileName(assemblyPath)}({m.line},{m.column}): {m.message}");
            }
        }

        private static void OnCompilationFinished(object obj)
        {
            string result = pendingErrors.Length == 0
                ? $"[{DateTime.Now:HH:mm:ss}] Compilation finished: 0 errors\n"
                : $"[{DateTime.Now:HH:mm:ss}] Compilation finished -- {CountLines(pendingErrors)} error(s):\n{pendingErrors}";
            SafeWrite(CompileLogPath, result);
        }

        private static int CountLines(StringBuilder sb)
        {
            int count = 0;
            foreach (char c in sb.ToString()) if (c == '\n') count++;
            return count;
        }

        // Editor-time console noise (asset import warnings etc.) isn't useful here --
        // only what happens once you're actually testing the game.
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                SafeAppend(ConsoleLogPath, $"\n===== Play session started {DateTime.Now:HH:mm:ss} =====\n");
            else if (state == PlayModeStateChange.ExitingPlayMode)
                SafeAppend(ConsoleLogPath, $"===== Play session ended {DateTime.Now:HH:mm:ss} =====\n");
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!EditorApplication.isPlaying) return;
            string line = $"[{DateTime.Now:HH:mm:ss}] [{type}] {condition}";
            if (type == LogType.Exception || type == LogType.Error)
                line += $"\n{stackTrace}";
            SafeAppend(ConsoleLogPath, line + "\n");
        }

        // File I/O here is best-effort diagnostics, not gameplay-critical -- swallow
        // failures (e.g. the file briefly locked by another process reading it) rather
        // than let a logging hiccup surface as an editor error dialog.
        private static void SafeWrite(string path, string content)
        {
            try { File.WriteAllText(path, content); } catch { }
        }

        private static void SafeAppend(string path, string content)
        {
            try { File.AppendAllText(path, content); } catch { }
        }
    }
}
#endif
