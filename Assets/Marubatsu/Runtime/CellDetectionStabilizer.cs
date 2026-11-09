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
        bool missedSample;
        double started, previousTime;
        long previousSequence = -1;
        public void Reset()
        {
            Stable = candidate = CellMark.Unknown; count = 0; previousSequence = -1;
            missedSample = false;
        }
        public void Observe(CellClassification result, long sequence, double time)
        {
            if (sequence <= previousSequence) return;
            if (time < previousTime || (previousSequence >= 0 && time-previousTime > MaximumGap)) Reset();
            previousSequence = sequence; previousTime = time;
            if (result.Mark == CellMark.Unknown || result.Confidence < 0.8f)
            {
                // Keep the evidence across one missed sample, but require a readable current frame.
                Stable = CellMark.Unknown;
                if (missedSample)
                {
                    candidate = CellMark.Unknown;
                    count = 0;
                }
                missedSample = true;
                return;
            }
            missedSample = false;
            if (result.Mark != candidate)
            { candidate = result.Mark; count = 0; started = time; Stable = CellMark.Unknown; }
            count++;
            if (count >= RequiredFrames && time-started >= RequiredSeconds) Stable = candidate;
        }
    }
}
