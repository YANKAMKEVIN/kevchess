using System;
using KevChess.Core;

namespace KevChess.AI
{
    public enum Bound : byte { None = 0, Upper = 1, Lower = 2, Exact = 3 }

    /// <summary>
    /// Table de transposition à une entrée par case (16 octets par entrée), taille en Mo puissance de deux.
    /// Remplacement : profondeur supérieure ou entrée d'une recherche précédente.
    /// </summary>
    public sealed class TranspositionTable
    {
        struct Entry
        {
            public ulong Key;
            public Move Move;
            public short Score;
            public sbyte Depth;
            public byte BoundAndAge; // bits 0-1 : borne, bits 2-7 : âge
        }

        const int EntryBytes = 16;
        Entry[] entries;
        ulong mask;
        byte age;

        public int SizeMb { get; private set; }

        public TranspositionTable(int sizeMb = 16) => Resize(sizeMb);

        public void Resize(int sizeMb)
        {
            sizeMb = Math.Max(1, Math.Min(256, sizeMb));
            long count = (long)sizeMb * 1024 * 1024 / EntryBytes;
            long pow = 1;
            while (pow * 2 <= count) pow *= 2;
            entries = new Entry[pow];
            mask = (ulong)(pow - 1);
            SizeMb = sizeMb;
        }

        public void Clear() => Array.Clear(entries, 0, entries.Length);

        /// <summary>À appeler au début de chaque nouvelle recherche.</summary>
        public void NewSearch() => age = (byte)((age + 1) & 63);

        public bool Probe(ulong key, out Move move, out int score, out int depth, out Bound bound)
        {
            ref Entry e = ref entries[key & mask];
            if (e.Key == key && (e.BoundAndAge & 3) != 0)
            {
                move = e.Move;
                score = e.Score;
                depth = e.Depth;
                bound = (Bound)(e.BoundAndAge & 3);
                return true;
            }
            move = Move.Null;
            score = 0;
            depth = -1;
            bound = Bound.None;
            return false;
        }

        public void Store(ulong key, Move move, int score, int depth, Bound bound)
        {
            ref Entry e = ref entries[key & mask];
            int oldAge = e.BoundAndAge >> 2;
            bool sameKey = e.Key == key;
            if (!sameKey || bound == Bound.Exact || depth + 2 >= e.Depth || oldAge != age)
            {
                // Conserve le coup connu si la nouvelle entrée n'en a pas.
                if (move.IsNull && sameKey) move = e.Move;
                e.Key = key;
                e.Move = move;
                e.Score = (short)score;
                e.Depth = (sbyte)Math.Max(-1, Math.Min(127, depth));
                e.BoundAndAge = (byte)((int)bound | (age << 2));
            }
        }

        /// <summary>Taux de remplissage approximatif en pour mille (échantillon de 1000 entrées).</summary>
        public int HashFull()
        {
            int n = 0, sample = (int)Math.Min(1000, entries.Length);
            for (int i = 0; i < sample; i++)
                if ((entries[i].BoundAndAge & 3) != 0 && (entries[i].BoundAndAge >> 2) == age) n++;
            return n * 1000 / sample;
        }
    }
}
