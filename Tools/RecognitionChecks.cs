using System;
using Marubatsu;

public static class RecognitionChecks
{
    static int checks;
    static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        checks++;
    }
    public static string Run()
    {
        const int n = 96;
        var processor = new CellImageProcessing(n);
        var image = new byte[n*n];
        Action<Func<int,int,bool>> paint = shape =>
        {
            for (int y=0; y<n; y++) for (int x=0; x<n; x++) image[y*n+x] = shape(x-48,y-48) ? (byte)15 : (byte)240;
        };
        paint((x,y) => false);
        Check(processor.Classify(image).Mark == CellMark.Empty, "blank sheet");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25) < 3);
        Check(processor.Classify(image).Mark == CellMark.Circle, "closed circle");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<27 && (Math.Abs(x-y)<4 || Math.Abs(x+y)<4));
        Check(processor.Classify(image).Mark == CellMark.Cross, "cross");
        paint((x,y) => x*x+y*y < 25*25);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "filled blob is not a mark");
        paint((x,y) => Math.Abs(x-y)<3 && Math.Abs(x)<26);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "single diagonal is not cross");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3 && !(x>12 && Math.Abs(y)<15));
        Check(processor.Classify(image).Mark == CellMark.Unknown, "open arc remains uncertain");
        paint((x,y) => true);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "fully dark occlusion");
        paint((x,y) => Math.Abs(x)<26 && Math.Abs(y)<26 && (Math.Abs(x)<4 || Math.Abs(y)<4));
        Check(processor.Classify(image).Mark == CellMark.Unknown, "plus sign is not cross");

        var stabilizer = new CellDetectionStabilizer();
        var circle = new CellClassification(CellMark.Circle,0.9f);
        for (int i=0; i<6; i++) stabilizer.Observe(circle,i,i*0.13);
        Check(stabilizer.Stable == CellMark.Circle, "six distinct frames and elapsed time");
        stabilizer.Observe(circle,6,2);
        Check(stabilizer.Stable == CellMark.Unknown, "gap invalidates old consensus");
        for (int i=0; i<10; i++) stabilizer.Observe(circle,6,2+i*0.1);
        Check(stabilizer.Stable == CellMark.Unknown, "duplicate frames cannot stabilize");
        stabilizer.Reset();
        Check(stabilizer.Stable == CellMark.Unknown, "tracking loss resets consensus");
        for (int i=0; i<8; i++) stabilizer.Observe(new CellClassification(CellMark.Cross,0.5f),i,3+i*0.13);
        Check(stabilizer.Stable == CellMark.Unknown, "low confidence is never stable");

        var source = new byte[32*32];
        for (int y=0; y<32; y++) for (int x=0; x<32; x++) source[y*32+x]=(byte)(x+3*y);
        var output = new byte[4*4];
        var rectangle = new double[] {2,2,26,2,26,26,2,26};
        Check(PerspectiveCellSampler.Sample(source,32,32,rectangle,output,4), "sample rectangle");
        Check(output[0]==8 && output[3]==32 && output[12]==80 && output[15]==104, "corner orientation and values");
        Check(PerspectiveCellSampler.Sample(source,32,32,new double[]{4,3,26,6,23,27,2,24},output,4), "perspective quadrilateral");
        Check(output[0]==13 && output[3]==44 && output[12]==74 && output[15]==104, "projective corner correspondence");
        var interior = new byte[9];
        Check(PerspectiveCellSampler.Sample(source,32,32,new double[]{4,4,28,4,20,28,12,28},interior,3) &&
            interior[4] == 82, "projective center differs from bilinear corner interpolation");
        Check(!PerspectiveCellSampler.Sample(source,32,32,new double[]{-1,2,26,2,26,26,2,26},output,4), "out of image rejected");
        Check(!PerspectiveCellSampler.Sample(source,32,32,new double[]{2,2,26,26,26,2,2,26},output,4), "self crossing rejected");
        Check(!PerspectiveCellSampler.Sample(source,32,32,new double[]{2,2,3,3,4,4,5,5},output,4), "degenerate quad rejected");
        return "PASS: " + checks + " recognition, stabilization, perspective checks. Synthetic images only.";
    }
}
