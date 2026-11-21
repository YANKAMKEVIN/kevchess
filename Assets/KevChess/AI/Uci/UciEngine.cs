using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KevChess.Core;

namespace KevChess.AI.Uci
{
    /// <summary>
    /// Moteur UCI externe (Stockfish) derrière IChessEngine. Les appels sont sérialisés : une recherche à la fois.
    /// Protocole : « uci » → « uciok », options, « isready » → « readyok » ; par coup « position ... » puis « go ... »
    /// jusqu'à « bestmove ». Annulation : « stop », puis on attend le bestmove pour garder le dialogue synchronisé.
    /// Un moteur qui ne répond plus est marqué hors service (IsBroken) : les appels suivants échouent aussitôt et
    /// FallbackEngine reprend la main avec l'IA maison.
    /// </summary>
    public sealed class UciEngine : IChessEngine, IDisposable
    {
        /// <summary>Délai pour « uciok » et « readyok ».</summary>
        public int HandshakeTimeoutMs = 5000;
        /// <summary>Marge laissée au moteur au-delà du temps demandé avant d'envoyer « stop ».</summary>
        public int GraceMs = 1500;
        /// <summary>Délai pour recevoir le bestmove après « stop ».</summary>
        public int StopTimeoutMs = 1500;

        readonly IUciTransport transport;
        readonly object gate = new object();
        readonly int hashMb, threads;
        bool initialized, newGamePending = true;
        int currentSkill = -1;

        /// <summary>Nom annoncé par le moteur (« id name »).</summary>
        public string Name { get; private set; } = "";
        public string Author { get; private set; } = "";
        /// <summary>Options annoncées par le moteur (« option name X type ... »).</summary>
        public IReadOnlyCollection<string> Options => options;
        readonly HashSet<string> options = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Dernière ligne « info » exploitable (score exact, variante) de la dernière recherche.</summary>
        public UciInfo LastInfo { get; private set; }
        public UciBestMove LastBestMove { get; private set; }
        /// <summary>Durée réelle de la dernière recherche (envoi du « go » → bestmove).</summary>
        public long LastElapsedMs { get; private set; }
        /// <summary>Le moteur a cessé de répondre ou s'est arrêté : il n'est plus utilisé.</summary>
        public bool IsBroken { get; private set; }

        public UciEngine(IUciTransport transport, int hashMb = 16, int threads = 1)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.hashMb = Math.Max(1, hashMb);
            this.threads = Math.Max(1, threads);
        }

        /// <summary>Poignée de main UCI (faite d'office au premier coup). Lève TimeoutException si le moteur ne répond pas.</summary>
        public void Initialize()
        {
            lock (gate) EnsureInitialized();
        }

        /// <summary>À appeler en début de nouvelle partie (« ucinewgame » envoyé avec la recherche suivante).</summary>
        public void NewGame()
        {
            lock (gate) newGamePending = true;
        }

        public Task<Move> FindMoveAsync(Position position, EngineOptions options, CancellationToken ct)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            var copy = position.Clone();
            string command = UciProtocol.PositionCommand(copy);
            return Task.Run(() => FindMove(copy, command, options, ct), ct);
        }

        /// <summary>Avec l'historique de la partie : le moteur voit les répétitions.</summary>
        public Task<Move> FindMoveAsync(ChessGame game, EngineOptions options, CancellationToken ct)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var copy = game.Position.Clone();
            string command = UciProtocol.PositionCommand(game.StartFen, new List<Move>(game.Moves));
            return Task.Run(() => FindMove(copy, command, options, ct), ct);
        }

        /// <summary>
        /// Recherche synchrone. current sert à valider le bestmove (coup légal) ; positionCommand est envoyé tel quel.
        /// Renvoie Move.Null si le moteur répond « (none) » (partie finie). Lève OperationCanceledException si ct est annulé,
        /// TimeoutException / EndOfStreamException si le moteur ne répond plus, InvalidDataException si son coup est illégal.
        /// </summary>
        public Move FindMove(Position current, string positionCommand, EngineOptions options, CancellationToken ct)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            options = options ?? new EngineOptions();
            lock (gate)
            {
                ct.ThrowIfCancellationRequested();
                EnsureInitialized();
                var level = UciProtocol.GetLevel(options.Level);
                if (level.SkillLevel != currentSkill && HasOption("Skill Level"))
                {
                    Send(UciProtocol.SetOption("Skill Level", level.SkillLevel));
                    currentSkill = level.SkillLevel;
                }
                if (newGamePending)
                {
                    Send("ucinewgame");
                    WaitReady();
                    newGamePending = false;
                }

                int moveTime = options.MoveTimeMs > 0 ? options.MoveTimeMs : level.MoveTimeMs;
                int depth = options.MaxDepth > 0 ? options.MaxDepth : level.MaxDepth;
                Send(positionCommand);
                var best = Search(UciProtocol.GoMoveTime(moveTime, depth), moveTime, ct);
                LastBestMove = best;
                if (best.IsNone) return Move.Null;
                var move = Notation.ParseUci(current, best.Move);
                if (move.IsNull) throw new InvalidDataException("Coup illégal du moteur UCI : " + best.Move);
                return move;
            }
        }

        UciBestMove Search(string goCommand, int moveTimeMs, CancellationToken ct)
        {
            LastInfo = null;
            var watch = Stopwatch.StartNew();
            Send(goCommand);
            bool stopSent = false, cancelled = false;
            long stopDeadline = 0;
            while (true)
            {
                if (!stopSent && ct.IsCancellationRequested)
                {
                    cancelled = true;
                    stopSent = true;
                    Send("stop");
                    stopDeadline = watch.ElapsedMilliseconds + StopTimeoutMs;
                }
                else if (!stopSent && watch.ElapsedMilliseconds > moveTimeMs + GraceMs)
                {
                    stopSent = true;
                    Send("stop");
                    stopDeadline = watch.ElapsedMilliseconds + StopTimeoutMs;
                }
                else if (stopSent && watch.ElapsedMilliseconds > stopDeadline)
                {
                    IsBroken = true;
                    throw new TimeoutException("Le moteur UCI ne répond plus après « stop »");
                }

                string line = Read(20);
                if (line == null) continue;
                if (UciProtocol.TryParseInfo(line, out var info))
                {
                    if (info.HasScore && !info.IsBound && info.MultiPv <= 1 && info.Pv.Count > 0) LastInfo = info;
                    continue;
                }
                if (UciProtocol.TryParseBestMove(line, out var best))
                {
                    LastElapsedMs = watch.ElapsedMilliseconds;
                    if (cancelled) throw new OperationCanceledException(ct);
                    return best;
                }
            }
        }

        void EnsureInitialized()
        {
            if (IsBroken) throw new InvalidOperationException("Moteur UCI hors service");
            if (initialized) return;
            Send("uci");
            var watch = Stopwatch.StartNew();
            while (true)
            {
                if (watch.ElapsedMilliseconds > HandshakeTimeoutMs)
                {
                    IsBroken = true;
                    throw new TimeoutException("Pas de « uciok » du moteur");
                }
                string line = Read(20);
                if (line == null) continue;
                var t = line.Trim();
                if (t == "uciok") break;
                if (t.StartsWith("id name ", StringComparison.Ordinal)) Name = t.Substring(8).Trim();
                else if (t.StartsWith("id author ", StringComparison.Ordinal)) Author = t.Substring(10).Trim();
                else if (t.StartsWith("option name ", StringComparison.Ordinal))
                {
                    int end = t.IndexOf(" type ", StringComparison.Ordinal);
                    options.Add((end > 12 ? t.Substring(12, end - 12) : t.Substring(12)).Trim());
                }
            }
            if (HasOption("Threads")) Send(UciProtocol.SetOption("Threads", threads));
            if (HasOption("Hash")) Send(UciProtocol.SetOption("Hash", hashMb));
            WaitReady();
            initialized = true;
        }

        bool HasOption(string name) => options.Contains(name);

        void WaitReady()
        {
            Send("isready");
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds <= HandshakeTimeoutMs)
            {
                string line = Read(20);
                if (line != null && line.Trim() == "readyok") return;
            }
            IsBroken = true;
            throw new TimeoutException("Pas de « readyok » du moteur");
        }

        void Send(string command)
        {
            try { transport.Send(command); }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
            {
                IsBroken = true;
                throw new EndOfStreamException("Le moteur UCI s'est arrêté", e);
            }
        }

        string Read(int timeoutMs)
        {
            try { return transport.ReadLine(timeoutMs); }
            catch (EndOfStreamException) { IsBroken = true; throw; }
        }

        /// <summary>Résumé de la dernière recherche, pour le journal.</summary>
        public string LastSummary()
        {
            var i = LastInfo;
            string name = Name.Length > 0 ? Name : "moteur UCI";
            if (i == null) return name + ", " + LastElapsedMs + " ms";
            string score = i.IsMate ? "mat en " + i.MateIn : i.ScoreCp + " cp";
            return name + " : profondeur " + i.Depth + ", " + score + ", " + i.Nodes + " nœuds, " + LastElapsedMs + " ms";
        }

        public void Dispose()
        {
            lock (gate)
            {
                try { transport.Send("quit"); } catch (Exception) { }
                transport.Dispose();
                IsBroken = true;
            }
        }
    }

    /// <summary>Moteur au choix du joueur (réglage « Moteur de l'IA »). L'IA maison reste la valeur par défaut.</summary>
    public enum EngineKind { KevChess = 0, Stockfish = 1 }

    /// <summary>
    /// Essaie le moteur principal (Stockfish) et se rabat sur le secours (IA maison) s'il échoue, s'il manque ou
    /// s'il rend un coup nul alors que la partie continue. L'annulation demandée par le jeu n'est jamais rattrapée.
    /// </summary>
    public sealed class FallbackEngine : IChessEngine
    {
        readonly Func<IChessEngine> primary;
        readonly IChessEngine fallback;

        /// <summary>Vrai si le dernier coup vient du moteur de secours.</summary>
        public bool LastUsedFallback { get; private set; }
        /// <summary>Dernière erreur du moteur principal (diagnostic), null sinon.</summary>
        public Exception LastError { get; private set; }

        /// <param name="primary">Fournit le moteur principal ; peut renvoyer null (absent, pas encore prêt).</param>
        public FallbackEngine(Func<IChessEngine> primary, IChessEngine fallback)
        {
            this.primary = primary ?? throw new ArgumentNullException(nameof(primary));
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public Task<Move> FindMoveAsync(Position position, EngineOptions options, CancellationToken ct) =>
            Run(e => e.FindMoveAsync(position, options, ct), position, options, ct);

        /// <summary>Variante avec un appel principal personnalisé (par ex. avec l'historique de la partie).</summary>
        public async Task<Move> Run(Func<IChessEngine, Task<Move>> call, Position position, EngineOptions options, CancellationToken ct)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            position = position.Clone(); // la partie peut changer pendant la réflexion
            var engine = primary();
            if (engine != null)
            {
                try
                {
                    var move = await call(engine).ConfigureAwait(false);
                    if (!move.IsNull || !MoveGenerator.HasLegalMove(position))
                    {
                        LastUsedFallback = false;
                        LastError = null;
                        return move;
                    }
                    LastError = new InvalidDataException("Coup nul du moteur alors que la partie continue");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception e) { LastError = e; }
            }
            LastUsedFallback = true;
            return await fallback.FindMoveAsync(position, options, ct).ConfigureAwait(false);
        }

        /// <summary>Moteur à utiliser selon le réglage et la présence d'un binaire Stockfish.</summary>
        public static EngineKind Resolve(EngineKind wanted, bool stockfishAvailable) =>
            wanted == EngineKind.Stockfish && stockfishAvailable ? EngineKind.Stockfish : EngineKind.KevChess;
    }
}
