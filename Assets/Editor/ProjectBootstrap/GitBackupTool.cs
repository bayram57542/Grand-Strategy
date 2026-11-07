using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProjectBootstrap
{
    /// <summary>
    /// Layihənin dəyişikliklərini avtomatik olaraq yadda saxlamaq (Save Scenes/Assets),
    /// lokal Git repozitoriyasına commit etmək və GitHub-a (origin) göndərmək (push) üçün alət.
    /// </summary>
    public static class GitBackupTool
    {
        [MenuItem("Grand Strategy/Git Backup/Layihəni Yadda Saxla (Commit & Push)")]
        public static void BackupProjectMenu()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            BackupProject($"Avtomatik Ehtiyat Nüsxə (Backup): {timestamp}", pushToRemote: true);
        }

        [MenuItem("Grand Strategy/Git Backup/Yalnız Lokal Commit Et")]
        public static void CommitLocallyMenu()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            BackupProject($"Lokal Yaddaş (Commit): {timestamp}", pushToRemote: false);
        }

        [CliCommand("git_backup", "Save Unity scenes/assets, run git add ., git commit, and optionally git push", MainThreadRequired = true)]
        public static string BackupProject(
            [CliArg("message", "Git commit message")] string message = "Avtomatik Ehtiyat Nüsxə (Backup)",
            [CliArg("push", "Push to remote origin if configured")] bool pushToRemote = true)
        {
            var report = new StringBuilder();
            try
            {
                // 1. Öncə açıq səhnələri və aktivləri yadda saxlayırıq
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;

                // 2. git add .
                string addOut = RunGitCommand(projectRoot, "add .", out int addExit);
                report.AppendLine($"[git add .] (Exit: {addExit}) {addOut}".Trim());

                // 3. Dəyişiklik olub-olmadığını yoxlayırıq
                string statusOut = RunGitCommand(projectRoot, "status --porcelain", out _);
                if (string.IsNullOrWhiteSpace(statusOut))
                {
                    string noChangesMsg = "[GitBackupTool] Yadda saxlanılacaq yeni dəyişiklik yoxdur (Working tree clean).";
                    Debug.Log(noChangesMsg);
                    report.AppendLine(noChangesMsg);
                }
                else
                {
                    string safeMessage = message.Replace("\"", "'");
                    string commitOut = RunGitCommand(projectRoot, $"commit -m \"{safeMessage}\"", out int commitExit);
                    report.AppendLine($"[git commit] (Exit: {commitExit}) {commitOut}".Trim());
                    Debug.Log($"[GitBackupTool] Lokal commit tamamlandı:\n{commitOut}");
                }

                // 4. Əgər push istənilibsə və remote 'origin' mövcuddursa, git push icra edirik
                if (pushToRemote)
                {
                    string remotes = RunGitCommand(projectRoot, "remote", out _);
                    if (!string.IsNullOrWhiteSpace(remotes) && remotes.Contains("origin"))
                    {
                        string pushOut = RunGitCommand(projectRoot, "push -u origin main", out int pushExit);
                        report.AppendLine($"[git push] (Exit: {pushExit}) {pushOut}".Trim());
                        Debug.Log($"[GitBackupTool] GitHub push nəticəsi:\n{pushOut}");
                    }
                    else
                    {
                        string noRemoteMsg = "[GitBackupTool] Lokal commit uğurludur. Hələ 'origin' (GitHub) remote əlavə edilmədiyi üçün 'git push' ötürüldü.";
                        Debug.Log(noRemoteMsg);
                        report.AppendLine(noRemoteMsg);
                    }
                }

                return report.ToString();
            }
            catch (Exception ex)
            {
                string err = $"[GitBackupTool] Xəta: {ex.Message}";
                Debug.LogError(err);
                return err;
            }
        }

        private static string RunGitCommand(string workingDirectory, string arguments, out int exitCode)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            exitCode = process.ExitCode;

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                return string.IsNullOrWhiteSpace(stdout) ? stderr : $"{stdout}\n{stderr}";
            }

            return stdout;
        }
    }
}
