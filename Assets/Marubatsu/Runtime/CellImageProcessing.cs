using System;

namespace Marubatsu
{
    public enum CellMark { Unknown, Empty, Circle, Cross }
    public readonly struct CellClassification
    {
        public readonly CellMark Mark;
        public readonly float Confidence;
        public CellClassification(CellMark mark, float confidence) { Mark = mark; Confidence = confidence; }
    }

    /// <summary>Conservative prototype with no Unity, Vuforia, or native library dependency.</summary>
    public sealed class CellImageProcessing
    {
        readonly int size;
        readonly int[] histogram = new int[256];
        readonly int[] queue;
        readonly bool[] visited;
        readonly byte[] component;
        public byte[] Binary { get; }
        public float InkRatio { get; private set; }
        public int MinimumContrast = 35;
        public float EmptyInkRatio = 0.015f;
        public CellImageProcessing(int size)
        {
            if (size < 16 || size > 512) throw new ArgumentOutOfRangeException(nameof(size));
            this.size = size;
            Binary = new byte[size * size]; component = new byte[Binary.Length];
            visited = new bool[Binary.Length]; queue = new int[Binary.Length];
        }
        public CellClassification Classify(byte[] gray)
        {
            if (gray == null || gray.Length != Binary.Length) throw new ArgumentException("Wrong image dimensions.");
            Array.Clear(histogram, 0, histogram.Length);
            int min = 255, max = 0;
            foreach (byte v in gray) { histogram[v]++; min = Math.Min(min, v); max = Math.Max(max, v); }
            int threshold = Math.Min(200, Otsu(histogram, gray.Length));
            int ink = 0;
            for (int i = 0; i < gray.Length; i++)
            {
                bool black = max - min >= MinimumContrast && gray[i] <= threshold;
                Binary[i] = black ? (byte)0 : (byte)255;
                if (black) ink++;
            }
            InkRatio = (float)ink / gray.Length;
            if (max - min < MinimumContrast && max < 120) return new CellClassification(CellMark.Unknown, 0);
            if (InkRatio < EmptyInkRatio) return new CellClassification(CellMark.Empty, 0.95f);
            if (InkRatio > 0.45f) return new CellClassification(CellMark.Unknown, 0);
            Array.Clear(visited, 0, visited.Length); Array.Clear(component, 0, component.Length);
            int largest = 0;
            for (int i = 0; i < Binary.Length; i++)
            {
                if (Binary[i] != 0 || visited[i]) continue;
                int count = Flood(i, true);
                if (count <= largest) continue;
                largest = count;
                Array.Clear(component, 0, component.Length);
                for (int j = 0; j < count; j++) component[queue[j]] = 1;
            }
            if (largest < 20 || largest < ink * 0.85f) return new CellClassification(CellMark.Unknown, 0);
            int left = size, right = -1, top = size, bottom = -1;
            for (int i = 0; i < component.Length; i++)
                if (component[i] != 0)
                {
                    left = Math.Min(left, i % size); right = Math.Max(right, i % size);
                    top = Math.Min(top, i / size); bottom = Math.Max(bottom, i / size);
                }
            int width = right-left+1, height = bottom-top+1;
            if (width < size*0.25f || height < size*0.25f || left < 2 || top < 2 || right >= size-2 || bottom >= size-2)
                return new CellClassification(CellMark.Unknown, 0);
            float aspect = (float)width / height;
            if (aspect < 0.45f || aspect > 2.2f) return new CellClassification(CellMark.Unknown, 0);
            // 4-connected background regions enclosed by the dominant 8-connected ink component.
            Array.Clear(visited, 0, visited.Length);
            int hole = 0;
            for (int i = 0; i < component.Length; i++)
            {
                if (component[i] != 0 || visited[i]) continue;
                int count = Flood(i, false);
                bool edge = false;
                for (int j = 0; j < count; j++)
                {
                    int p = queue[j];
                    if (p % size == 0 || p % size == size-1 || p < size || p >= size*(size-1)) { edge = true; break; }
                }
                if (!edge) hole = Math.Max(hole, count);
            }
            if (hole > width*height*0.12f) return new CellClassification(CellMark.Circle, 0.90f);
            int diagonal = 0;
            int[] arms = new int[4];
            for (int i = 0; i < component.Length; i++)
                if (component[i] != 0)
                {
                    float x = 2f*(i % size-left)/Math.Max(1, width-1)-1;
                    float y = 2f*(i / size-top)/Math.Max(1, height-1)-1;
                    if (Math.Min(Math.Abs(x-y), Math.Abs(x+y)) < 0.25f)
                    {
                        diagonal++;
                        if (Math.Abs(x) > 0.40f && Math.Abs(y) > 0.40f)
                            arms[(x > 0 ? 1 : 0)+(y > 0 ? 2 : 0)]++;
                    }
                }
            if ((float)diagonal/largest > 0.82f && Array.TrueForAll(arms, a => a > largest*0.035f))
                return new CellClassification(CellMark.Cross, 0.85f);
            return new CellClassification(CellMark.Unknown, 0);
        }
        int Flood(int start, bool ink)
        {
            int head = 0, tail = 1;
            queue[0] = start; visited[start] = true;
            while (head < tail)
            {
                int p = queue[head++], x = p % size, y = p / size;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || (!ink && dx != 0 && dy != 0)) continue;
                        int nx = x+dx, ny = y+dy;
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                        int n = ny*size+nx;
                        bool matches = ink ? Binary[n] == 0 : component[n] == 0;
                        if (visited[n] || !matches) continue;
                        visited[n] = true; queue[tail++] = n;
                    }
            }
            return tail;
        }
        static int Otsu(int[] hist, int count)
        {
            double sum = 0, backgroundSum = 0, best = -1;
            for (int i = 0; i < 256; i++) sum += (double)i*hist[i];
            int background = 0, threshold = 0;
            for (int i = 0; i < 256; i++)
            {
                background += hist[i];
                if (background == 0) continue;
                int foreground = count-background;
                if (foreground == 0) break;
                backgroundSum += (double)i*hist[i];
                double delta = backgroundSum/background-(sum-backgroundSum)/foreground;
                double variance = (double)background*foreground*delta*delta;
                if (variance > best) { best = variance; threshold = i; }
            }
            return threshold;
        }
    }
}
