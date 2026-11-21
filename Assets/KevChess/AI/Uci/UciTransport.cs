using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace KevChess.AI.Uci
{
    /// <summary>Canal texte vers un moteur UCI (processus, bibliothèque native, faux moteur des tests).</summary>
    public interface IUciTransport : IDisposable
    {
        /// <summary>Envoie une commande (une ligne, sans retour chariot).</summary>
        void Send(string command);

        /// <summary>
        /// Ligne suivante du moteur, ou null si rien n'arrive dans le délai.
        /// Lève EndOfStreamException si le moteur s'est arrêté.
        /// </summary>
        string ReadLine(int timeoutMs);
    }

    /// <summary>
    /// Moteur UCI lancé comme un processus (Mac, Linux, Windows, tests). Une tâche de fond lit la sortie standard
    /// ligne par ligne. Sur Android, lancer un exécutable demande un binaire placé dans les bibliothèques natives
    /// de l'APK (voir docs/tech/etat-projet.md, « Stockfish ») : ce transport n'y est pas utilisé tel quel.
    /// </summary>
    public sealed class ProcessUciTransport : IUciTransport
    {
        readonly Process process;
        readonly BlockingCollection<string> lines = new BlockingCollection<string>();
        readonly Thread reader;
        volatile bool ended;

        public ProcessUciTransport(string executablePath, string arguments = "")
        {
            if (string.IsNullOrEmpty(executablePath)) throw new ArgumentException("Chemin du moteur vide", nameof(executablePath));
            var psi = new ProcessStartInfo(executablePath, arguments ?? "")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true,
            };
            process = Process.Start(psi) ?? throw new IOException("Impossible de lancer " + executablePath);
            process.StandardInput.AutoFlush = true;
            reader = new Thread(ReadLoop) { IsBackground = true, Name = "UCI reader" };
            reader.Start();
        }

        void ReadLoop()
        {
            try
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null) lines.Add(line);
            }
            catch (Exception) { /* processus tué */ }
            finally
            {
                ended = true;
                lines.CompleteAdding();
            }
        }

        public void Send(string command)
        {
            if (ended) throw new EndOfStreamException("Le moteur UCI s'est arrêté");
            process.StandardInput.WriteLine(command);
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
            try
            {
                if (!process.HasExited)
                {
                    try { process.StandardInput.WriteLine("quit"); } catch (Exception) { }
                    if (!process.WaitForExit(500)) process.Kill();
                }
            }
            catch (Exception) { }
            process.Dispose();
        }
    }
}
