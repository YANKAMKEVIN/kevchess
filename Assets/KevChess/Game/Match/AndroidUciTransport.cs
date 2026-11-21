using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using KevChess.AI.Uci;
using UnityEngine;

namespace KevChess.Game.Match
{
    /// <summary>
    /// Moteur UCI lancé comme processus sur Android, par java.lang.ProcessBuilder (System.Diagnostics.Process
    /// n'est pas utilisable sous IL2CPP Android). Le binaire est livré comme « bibliothèque native »
    /// (Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so) : Android l'extrait, exécutable, dans
    /// nativeLibraryDir, seul endroit d'où une app peut lancer un exécutable depuis Android 10.
    ///
    /// Tous les appels Java passent par deux fils dédiés, attachés à la JVM du début à la fin : un écrivain
    /// (démarrage du processus, commandes, arrêt) et un lecteur (sortie ligne par ligne). Les fils du pool de
    /// tâches qui appellent Send ou ReadLine ne touchent jamais à JNI.
    /// </summary>
    public sealed class AndroidUciTransport : IUciTransport
    {
        const string QuitCommand = "\u0000quit";

        readonly BlockingCollection<string> commands = new BlockingCollection<string>();
        readonly BlockingCollection<string> lines = new BlockingCollection<string>();
        readonly ManualResetEventSlim started = new ManualResetEventSlim(false);
        volatile bool ended;
        Exception startError;
        AndroidJavaObject process;
        AndroidJavaObject stdout;

        /// <param name="executablePath">Chemin absolu de l'exécutable (dans nativeLibraryDir).</param>
        public AndroidUciTransport(string executablePath, int startTimeoutMs = 5000)
        {
            if (string.IsNullOrEmpty(executablePath)) throw new ArgumentException("Chemin du moteur vide", nameof(executablePath));
            var writer = new Thread(() => WriteLoop(executablePath)) { IsBackground = true, Name = "UCI writer" };
            writer.Start();
            if (!started.Wait(startTimeoutMs)) { Dispose(); throw new IOException("Le moteur ne démarre pas : " + executablePath); }
            if (startError != null) { Dispose(); throw new IOException("Impossible de lancer " + executablePath + " : " + startError.Message, startError); }
        }

        void WriteLoop(string path)
        {
            AndroidJNI.AttachCurrentThread();
            AndroidJavaObject stdin = null;
            try
            {
                try
                {
                    using (var argv = new AndroidJavaObject("java.util.ArrayList"))
                    {
                        argv.Call<bool>("add", path);
                        using (var builder = new AndroidJavaObject("java.lang.ProcessBuilder", argv))
                        {
                            builder.Call<AndroidJavaObject>("redirectErrorStream", true)?.Dispose();
                            process = builder.Call<AndroidJavaObject>("start");
                        }
                    }
                    using (var output = process.Call<AndroidJavaObject>("getOutputStream"))
                        stdin = new AndroidJavaObject("java.io.PrintWriter", output);
                    using (var input = process.Call<AndroidJavaObject>("getInputStream"))
                    using (var reader = new AndroidJavaObject("java.io.InputStreamReader", input))
                        stdout = new AndroidJavaObject("java.io.BufferedReader", reader);
                    new Thread(ReadLoop) { IsBackground = true, Name = "UCI reader" }.Start();
                }
                catch (Exception e)
                {
                    startError = e;
                    ended = true;
                    lines.CompleteAdding();
                    return;
                }
                finally { started.Set(); }

                foreach (var command in commands.GetConsumingEnumerable())
                {
                    if (command == QuitCommand) break;
                    stdin.Call("println", command);
                    stdin.Call("flush");
                    if (stdin.Call<bool>("checkError")) break; // tube fermé : le moteur s'est arrêté
                }

                // Arrêt : « quit », un court délai, puis destroy() si le moteur traîne.
                try { stdin?.Call("println", "quit"); stdin?.Call("flush"); } catch (Exception) { }
                Thread.Sleep(300);
                try { process?.Call("destroy"); } catch (Exception) { }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KevChess] Stockfish (Android) : " + e.Message);
                try { process?.Call("destroy"); } catch (Exception) { }
            }
            finally
            {
                ended = true;
                stdin?.Dispose();
                process?.Dispose();
                process = null;
                AndroidJNI.DetachCurrentThread();
            }
        }

        void ReadLoop()
        {
            AndroidJNI.AttachCurrentThread();
            try
            {
                string line;
                while ((line = stdout.Call<string>("readLine")) != null) lines.Add(line);
            }
            catch (Exception) { /* processus détruit */ }
            finally
            {
                ended = true;
                lines.CompleteAdding();
                stdout?.Dispose();
                stdout = null;
                AndroidJNI.DetachCurrentThread();
            }
        }

        public void Send(string command)
        {
            if (ended || commands.IsAddingCompleted) throw new EndOfStreamException("Le moteur UCI s'est arrêté");
            try { commands.Add(command); }
            catch (InvalidOperationException) { throw new EndOfStreamException("Le moteur UCI s'est arrêté"); }
        }

        public string ReadLine(int timeoutMs)
        {
            try
            {
                if (lines.TryTake(out var line, Math.Max(0, timeoutMs))) return line;
            }
            catch (InvalidOperationException) { /* collection terminée */ }
            if (lines.IsCompleted) throw new EndOfStreamException("Le moteur UCI s'est arrêté");
            return null;
        }

        public void Dispose()
        {
            if (commands.IsAddingCompleted) return;
            try { commands.Add(QuitCommand); } catch (InvalidOperationException) { }
            commands.CompleteAdding();
        }

        /// <summary>
        /// Dossier des bibliothèques natives de l'app (ApplicationInfo.nativeLibraryDir), ou null hors Android.
        /// À appeler depuis le fil principal.
        /// </summary>
        public static string NativeLibraryDir()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var info = activity.Call<AndroidJavaObject>("getApplicationInfo"))
                    return info.Get<string>("nativeLibraryDir");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KevChess] nativeLibraryDir introuvable : " + e.Message);
                return null;
            }
        }
    }
}
