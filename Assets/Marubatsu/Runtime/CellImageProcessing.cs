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
        readonly byte[] normalized, scratch;
        readonly float[] inkX, inkY;
        static readonly float[] Slopes = { 0.55f, 0.8f, 1f, 1.25f, 1.8f };
        public byte[] Binary { get; }
        public float InkRatio { get; private set; }
        public string DiagnosticReason { get; private set; } = "Waiting for image";
        public int MinimumContrast = 25;
        public float EmptyInkRatio = 0.015f;
        public CellImageProcessing(int size)
        {
            if (size < 16 || size > 512) throw new ArgumentOutOfRangeException(nameof(size));
            this.size = size;
            Binary = new byte[size * size]; component = new byte[Binary.Length];
            visited = new bool[Binary.Length]; queue = new int[Binary.Length];
            normalized = new byte[Binary.Length]; scratch = new byte[Binary.Length];
            inkX = new float[Binary.Length]; inkY = new float[Binary.Length];
        }
        public CellClassification Classify(byte[] gray)
        {
            if (gray == null || gray.Length != Binary.Length) throw new ArgumentException("Wrong image dimensions.");
            int rawMax = 0;
            foreach (byte v in gray) rawMax = Math.Max(rawMax, v);
            if (rawMax < 120)
            {
                Array.Copy(gray, Binary, gray.Length); InkRatio = 0;
                return Result(CellMark.Unknown, 0, "Dark or covered cell");
            }
            NormalizePaperLighting(gray);
            Array.Clear(histogram, 0, histogram.Length);
            int min = 255, max = 0;
            foreach (byte v in normalized) { histogram[v]++; min = Math.Min(min, v); max = Math.Max(max, v); }
            // Paper is normalized to 220; retain soft gray stroke edges down to an 8-level difference.
            int threshold = Math.Min(212, Otsu(histogram, gray.Length));
            for (int i = 0; i < gray.Length; i++)
            {
                bool black = max - min >= MinimumContrast && normalized[i] <= threshold;
                Binary[i] = black ? (byte)0 : (byte)255;
            }
            CloseSmallGaps();
            int ink = 0;
            foreach (byte v in Binary) if (v == 0) ink++;
            InkRatio = (float)ink / gray.Length;
            if (InkRatio < EmptyInkRatio) return Result(CellMark.Empty, 0.95f, "No substantial ink");
            if (InkRatio > 0.45f) return Result(CellMark.Unknown, 0, "Too much ink or shadow");
            Array.Clear(visited, 0, visited.Length); Array.Clear(component, 0, component.Length);
            int largest = 0;
            // Drop isolated specks (up to 7 pixels in a 96 x 96 crop).
            int speckLimit = Math.Max(1, Binary.Length / 1250);
            for (int i = 0; i < Binary.Length; i++)
            {
                if (Binary[i] != 0 || visited[i]) continue;
                int count = Flood(i, true);
                if (count <= speckLimit)
                {
                    ink -= count;
                    for (int j = 0; j < count; j++) Binary[queue[j]] = 255;
                    continue;
                }
                if (count <= largest) continue;
                largest = count;
                Array.Clear(component, 0, component.Length);
                for (int j = 0; j < count; j++) component[queue[j]] = 1;
            }
            InkRatio = (float)ink / gray.Length;
            if (InkRatio < EmptyInkRatio) return Result(CellMark.Empty, 0.95f, "Only isolated specks");
            if (largest < 20) return Result(CellMark.Unknown, 0, "Ink is too small");
            if (largest < ink * 0.85f) return Result(CellMark.Unknown, 0, "Several disconnected strokes");
            int left = size, right = -1, top = size, bottom = -1;
            for (int i = 0; i < component.Length; i++)
                if (component[i] != 0)
                {
                    left = Math.Min(left, i % size); right = Math.Max(right, i % size);
                    top = Math.Min(top, i / size); bottom = Math.Max(bottom, i / size);
                }
            int width = right-left+1, height = bottom-top+1;
            if (width < size*0.25f || height < size*0.25f)
                return Result(CellMark.Unknown, 0, "Symbol is too small or narrow");
            if (left < 2 || top < 2 || right >= size-2 || bottom >= size-2)
                return Result(CellMark.Unknown, 0, "Ink touches crop edge");
            float aspect = (float)width / height;
            if (aspect < 0.45f || aspect > 2.2f) return Result(CellMark.Unknown, 0, "Symbol is too elongated");
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
            if (hole > width*height*0.12f) return Result(CellMark.Circle, 0.90f, "Closed loop");
            int points = 0;
            for (int i = 0; i < component.Length; i++)
                if (component[i] != 0)
                {
                    inkX[points] = 2f*(i % size-left)/Math.Max(1, width-1)-1;
                    inkY[points++] = 2f*(i / size-top)/Math.Max(1, height-1)-1;
                }
            if (HasFourDiagonalArms(points)) return Result(CellMark.Cross, 0.85f, "Four diagonal arms");
            return Result(CellMark.Unknown, 0, "No loop or four diagonal arms");
        }

        CellClassification Result(CellMark mark, float confidence, string reason)
        {
            DiagnosticReason = reason;
            return new CellClassification(mark, confidence);
        }

        void NormalizePaperLighting(byte[] gray)
        {
            // The inset cell border is expected to be blank paper. Estimate only a smooth plane;
            // local thresholding around thick ink could otherwise turn a filled blob into a ring.
            double left = 0, right = 0, top = 0, bottom = 0;
            for (int i = 0; i < size; i++)
            {
                left += gray[i*size]; right += gray[i*size+size-1];
                top += gray[i]; bottom += gray[(size-1)*size+i];
            }
            left /= size; right /= size; top /= size; bottom /= size;
            double center = (left+right+top+bottom)/4;
            double dx = (right-left)/(size-1), dy = (bottom-top)/(size-1), middle = (size-1)*0.5;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double background = center+dx*(x-middle)+dy*(y-middle);
                    normalized[y*size+x] = (byte)Math.Max(0, Math.Min(255, Math.Round(gray[y*size+x]+220-background)));
                }
        }

        void CloseSmallGaps()
        {
            // 3x3 closing repairs tiny resampling breaks; large gaps and occlusion remain uncertain.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte value = 255;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (x+dx >= 0 && x+dx < size && y+dy >= 0 && y+dy < size)
                                value = Math.Min(value, Binary[(y+dy)*size+x+dx]);
                    scratch[y*size+x] = value;
                }
            for (int y = 1; y < size-1; y++)
                for (int x = 1; x < size-1; x++)
                {
                    byte value = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            value = Math.Max(value, scratch[(y+dy)*size+x+dx]);
                    Binary[y*size+x] = value;
                }
        }

        bool HasFourDiagonalArms(int count)
        {
            for (int shiftY = -1; shiftY <= 1; shiftY++)
                for (int shiftX = -1; shiftX <= 1; shiftX++)
                    foreach (float positive in Slopes)
                        foreach (float negative in Slopes)
                        {
                            int matched = 0, a = 0, b = 0, c = 0, d = 0;
                            for (int i = 0; i < count; i++)
                            {
                                float x = inkX[i]-shiftX*0.15f, y = inkY[i]-shiftY*0.15f;
                                float p = y-positive*x, n = y+negative*x;
                                bool onPositive = p*p < 0.0324f*(1+positive*positive);
                                bool onNegative = n*n < 0.0324f*(1+negative*negative);
                                if (onPositive || onNegative) matched++;
                                if (Math.Abs(x) < 0.4f || Math.Abs(y) < 0.25f) continue;
                                if (onPositive) { if (x > 0) a++; else b++; }
                                if (onNegative) { if (x > 0) c++; else d++; }
                            }
                            if (matched > count*0.82f && a > count*0.035f && b > count*0.035f &&
                                c > count*0.035f && d > count*0.035f) return true;
                        }
            return false;
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
