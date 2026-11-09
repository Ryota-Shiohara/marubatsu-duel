using System;

namespace Marubatsu
{
    /// <summary>Recognition evidence for nine cells. The image buffers describe the last successful sample.</summary>
    public sealed class BoardCellRecognition
    {
        public const int Count = 9;

        readonly int sampleSize;
        readonly CellImageProcessing classifier;
        readonly CellDetectionStabilizer[] cells = new CellDetectionStabilizer[Count];

        public byte[] Crop { get; }
        public byte[] Binary => classifier.Binary;
        public float InkRatio => classifier.InkRatio;
        public string DiagnosticReason => classifier.DiagnosticReason;

        public BoardCellRecognition(int sampleSize)
        {
            classifier = new CellImageProcessing(sampleSize);
            this.sampleSize = sampleSize;
            Crop = new byte[sampleSize * sampleSize];
            for (int index = 0; index < Count; index++)
                cells[index] = new CellDetectionStabilizer();
        }

        public CellMark StableAt(int index) => cells[index].Stable;

        public void InvalidateCell(int index) => cells[index].Reset();

        public void Reset()
        {
            foreach (var cell in cells)
                cell.Reset();
        }

        public bool TryObserve(int index, byte[] frame, int width, int height, double[] corners,
            long sequence, double time, out CellClassification classification)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (!PerspectiveCellSampler.Sample(frame, width, height, corners, Crop, sampleSize))
            {
                InvalidateCell(index);
                classification = default;
                return false;
            }

            classification = classifier.Classify(Crop);
            cells[index].Observe(classification, sequence, time);
            return true;
        }
    }
}
