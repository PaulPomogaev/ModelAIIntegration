using ModelAIIntegrationCore.GigaChat;
using ModelAIIntegrationCore.Knowledge.Models;
using ModelAIIntegrationCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Knowledge
{
    public class KnowledgeBase
    {
        private List<Chunk> _chunks = new();
        public int Count => _chunks.Count;

        public async Task BuildFromFolderAsync(string folder, ILanguageModel llm)
        {
            var files = Directory.GetFiles(folder, "*.md")
                .Concat(Directory.GetFiles(folder, "*.txt"))
                .OrderBy(f => f)
                .ToList();

            var pending = new List<(string Source, string Text)>();
            foreach (var file in files)
            {
                string name = Path.GetFileName(file);
                string text = File.ReadAllText(file);
                foreach (var piece in SplitIntoChunks(text))
                    pending.Add((name, piece));
            }

            _chunks = new List<Chunk>();
            foreach (var batch in Batch(pending, 10))
            {

                float[][] vectors = await llm.EmbedAsync(batch.Select(p => p.Text).ToList());
                for (int i = 0; i < batch.Count; i++)
                    _chunks.Add(new Chunk(batch[i].Source, batch[i].Text, vectors[i]));
            }
        }

        public void Save(string path) =>
            File.WriteAllText(path, JsonSerializer.Serialize(_chunks));

        public void Load(string path) =>
            _chunks = JsonSerializer.Deserialize<List<Chunk>>(File.ReadAllText(path)) ?? new();

        public async Task<List<Scored>> SearchAsync(string query, ILanguageModel llm, int topK)
        {
            float[][] vectors = await llm.EmbedAsync(new List<string> { query });
            float[] q = vectors[0];
            return _chunks
                .Select(c => new Scored(c, Cosine(q, c.Vector)))
                .OrderByDescending(s => s.Score)
                .Take(topK)
                .ToList();
        }

        private static double Cosine(float[] a, float[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
        }

        private static IEnumerable<string> SplitIntoChunks(string text)
        {
            var paragraphs = text
                .Replace("\r\n", "\n")
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);

            string? carry = null;
            foreach (var p in paragraphs)
            {
                string piece = carry is null ? p : carry + "\n" + p;
                if (piece.Length < 80)   // если кусок слишком короткий, присоединяем к следующему
                {
                    carry = piece;
                    continue;
                }
                //  если кусок слишком длинный (>1500 символов), режем его
                if (piece.Length > 1500)
                {
                    // Разбиваем на части по ~1000 символов (можно настроить)
                    const int chunkSize = 1000;
                    for (int i = 0; i < piece.Length; i += chunkSize)
                    {
                        int len = Math.Min(chunkSize, piece.Length - i);
                        yield return piece.Substring(i, len);
                    }
                    carry = null;
                    continue;
                }
                carry = null;
                yield return piece;
            }
            if (carry is not null)
                yield return carry;
        }

        private static IEnumerable<List<T>> Batch<T>(List<T> items, int size)
        {
            for (int i = 0; i < items.Count; i += size)
                yield return items.GetRange(i, Math.Min(size, items.Count - i));
        }
    }
}
