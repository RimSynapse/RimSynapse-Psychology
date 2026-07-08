using System;
using System.IO;
using Verse;

namespace RimSynapse.Psychology.Utils
{
    public static class PsychologyLogger
    {
        private static string LogDirectory => Path.Combine(GenFilePaths.SaveDataFolderPath, "RimSynapse_Logs", "Psychology");

        public static void LogEvent(Pawn pawn, string eventType, string details)
        {
            if (!RimSynapsePsychologyMod.Settings.enableDebugLogging) return;

            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string safePawnName = pawn.Name?.ToStringShort ?? "UnknownPawn";
                // Sanitize filename just in case
                safePawnName = string.Join("_", safePawnName.Split(Path.GetInvalidFileNameChars()));
                
                string filePath = Path.Combine(LogDirectory, $"{safePawnName}_PsychologyLog.txt");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                string logLine = $"[{timestamp}] [{safePawnName}] [{eventType}] - {details}\n";
                
                File.AppendAllText(filePath, logLine);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimSynapse-Psychology] Failed to write debug log: {ex.Message}");
            }
        }

        public static void LogMetric(Pawn pawn, string processName, long elapsedMilliseconds)
        {
            if (!RimSynapsePsychologyMod.Settings.enableDebugLogging) return;

            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string safePawnName = pawn != null ? (pawn.Name?.ToStringShort ?? "UnknownPawn") : "System";
                safePawnName = string.Join("_", safePawnName.Split(Path.GetInvalidFileNameChars()));
                
                string filePath = Path.Combine(LogDirectory, $"{safePawnName}_PerformanceMetrics.txt");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                string logLine = $"[{timestamp}] [{processName}] Execution Time: {elapsedMilliseconds}ms\n";
                
                File.AppendAllText(filePath, logLine);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimSynapse-Psychology] Failed to write metric log: {ex.Message}");
            }
        }
    }
}
