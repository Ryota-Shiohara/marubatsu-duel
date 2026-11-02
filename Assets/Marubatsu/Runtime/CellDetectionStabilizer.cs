namespace Marubatsu
{
    /// <summary>Diagnostic consensus only; does not accept game moves.</summary>
    public sealed class CellDetectionStabilizer
    {
        public int RequiredFrames = 6;
        public double RequiredSeconds = 0.6;
        public double MaximumGap = 0.4;
        public CellMark Stable { get; private set; }
        CellMark candidate;
        int count;
        double started, previousTime;
        long previousSequence = -1;
        public void Reset()
        {
            Stable = candidate = CellMark.Unknown; count = 0; previousSequence = -1;
        }
        public void Observe(CellClassification result, long sequence, double time)
        {
            if (sequence <= previousSequence) return;
            if (time < previousTime || (previousSequence >= 0 && time-previousTime > MaximumGap)) Reset();
            previousSequence = sequence; previousTime = time;
            if (result.Mark == CellMark.Unknown || result.Confidence < 0.8f)
            { Stable = candidate = CellMark.Unknown; count = 0; return; }
            if (result.Mark != candidate)
            { candidate = result.Mark; count = 0; started = time; Stable = CellMark.Unknown; }
            count++;
            if (count >= RequiredFrames && time-started >= RequiredSeconds) Stable = candidate;
        }
    }
}
