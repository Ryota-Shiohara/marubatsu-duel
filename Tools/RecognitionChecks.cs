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

        // Exposure gradients, resampling breaks, and asymmetric strokes seen after rectification.
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3);
        AddLightingGradient(image,n);
        Check(processor.Classify(image).Mark == CellMark.Circle, "circle under smooth lighting gradient");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<27 && (Math.Abs(x-y)<4 || Math.Abs(x+y)<4));
        AddLightingGradient(image,n);
        Check(processor.Classify(image).Mark == CellMark.Cross, "cross under smooth lighting gradient");
        paint((x,y) => false);
        AddLightingGradient(image,n);
        Check(processor.Classify(image).Mark == CellMark.Empty, "blank sheet under lighting gradient");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3 && !(x>0 && y==0));
        Check(processor.Classify(image).Mark == CellMark.Circle, "one pixel break in circle");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<27 && (Math.Abs(x-y)<4 || Math.Abs(x+y)<4) && y!=0);
        Check(processor.Classify(image).Mark == CellMark.Cross, "one pixel break at cross intersection");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<29 &&
            (Math.Abs(y-0.65*x-3)<3 || Math.Abs(y+1.45*x-3)<3));
        Check(processor.Classify(image).Mark == CellMark.Cross, "asymmetric diagonal cross");
        paint((x,y) => Math.Abs(y-0.65*x-3)<3 && Math.Abs(x)<26);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "slanted single line remains uncertain");
        paint((x,y) => Math.Abs(x)<3 && y>-28 && y<26 || Math.Abs(y+28)<3 && Math.Abs(x)<26);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "T shape remains uncertain");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3);
        BlurByDownsampling(image,n,24,16);
        Check(processor.Classify(image).Mark == CellMark.Circle, "circle after anisotropic low resolution blur");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<27 && (Math.Abs(x-y)<4 || Math.Abs(x+y)<4));
        BlurByDownsampling(image,n,24,16);
        Check(processor.Classify(image).Mark == CellMark.Cross, "cross after anisotropic low resolution blur");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3);
        BlurByDownsampling(image,n,24,16);
        AddLightingGradient(image,n);
        Check(processor.Classify(image).Mark == CellMark.Circle, "blurred circle with exposure gradient");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<27 && (Math.Abs(x-y)<4 || Math.Abs(x+y)<4));
        for (int i=0; i<12; i++) image[(5+i*7)*n+7+(i*17)%80]=15;
        Check(processor.Classify(image).Mark == CellMark.Cross, "cross with scattered sensor noise");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3 && Math.Abs(y)>5);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "large separated arcs stay unknown");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-47)<4);
        Check(processor.Classify(image).Mark == CellMark.Unknown && processor.DiagnosticReason == "Ink touches crop edge",
            "cropped symbol remains unknown with explicit reason");
        for (int y=0; y<n; y++) for (int x=0; x<n; x++) image[y*n+x]=(byte)(x<n/2 ? 130 : 240);
        Check(processor.Classify(image).Mark == CellMark.Unknown, "sharp shadow is not a mark");
        paint((x,y) => true);
        Check(processor.Classify(image).Mark == CellMark.Unknown && processor.DiagnosticReason == "Dark or covered cell",
            "covered cell has diagnostic reason");
        paint((x,y) => Math.Abs(Math.Sqrt(x*x+y*y)-25)<3);
        for (int i=0; i<image.Length; i++) if (image[i]<100) image[i]=215;
        Check(processor.Classify(image).Mark == CellMark.Circle, "faint but coherent circle");
        paint((x,y) => Math.Abs(x)<27 && Math.Abs(y)<27 && (Math.Abs(x-y)<4 || Math.Abs(x+y)<4));
        for (int i=0; i<image.Length; i++) if (image[i]<100) image[i]=215;
        Check(processor.Classify(image).Mark == CellMark.Cross, "faint but coherent cross");
        for (int y=0; y<n; y++) for (int x=0; x<n; x++) image[y*n+x]=(byte)(232+(x*19+y*23)%17);
        Check(processor.Classify(image).Mark == CellMark.Empty, "mild paper noise is empty");

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
        CheckNineCells();
        return "PASS: " + checks + " recognition, stabilization, perspective checks. Synthetic images only.";
    }

    static void CheckNineCells()
    {
        const int cellSize = 96, width = cellSize * 3 + 1;
        var frame = new byte[width * width];
        var board = new BoardCellRecognition(cellSize);
        var expected = new[] { CellMark.Circle, CellMark.Empty, CellMark.Cross,
            CellMark.Cross, CellMark.Circle, CellMark.Empty,
            CellMark.Empty, CellMark.Cross, CellMark.Circle };
        var quads = new double[expected.Length][];
        for (int index = 0; index < expected.Length; index++)
        {
            int left = index % 3 * cellSize, top = index / 3 * cellSize;
            quads[index] = new double[] { left, top, left + 95, top,
                left + 95, top + 95, left, top + 95 };
            for (int row = 0; row < cellSize; row++)
                for (int col = 0; col < cellSize; col++)
                {
                    int x = col - 48, y = row - 48;
                    bool ink = expected[index] == CellMark.Circle
                        ? Math.Abs(Math.Sqrt(x * x + y * y) - 25) < 3
                        : expected[index] == CellMark.Cross && Math.Abs(x) < 27 && Math.Abs(y) < 27 &&
                          (Math.Abs(x - y) < 4 || Math.Abs(x + y) < 4);
                    frame[(top + row) * width + left + col] = ink ? (byte)15 : (byte)240;
                }
        }
        for (int sample = 0; sample < 6; sample++)
            for (int index = 0; index < expected.Length; index++)
                if (!board.TryObserve(index, frame, width, width, quads[index], sample,
                    sample * 0.13, out var result) || result.Mark != expected[index])
                    throw new Exception($"FAIL: classification of cell {index} on sample {sample}");
        Check(true, "one frame can contain independent observations for all nine cells");
        for (int index = 0; index < expected.Length; index++)
            Check(board.StableAt(index) == expected[index], $"stable mark at cell {index}");
        var offscreen = new double[] {-1,0,95,0,95,95,0,95};
        Check(!board.TryObserve(0, frame, width, width, offscreen, 6, 0.8, out _) &&
            board.StableAt(0) == CellMark.Unknown &&
            board.StableAt(2) == CellMark.Cross, "one invalid crop clears only its cell");
        board.Reset();
        for (int index = 0; index < expected.Length; index++)
            if (board.StableAt(index) != CellMark.Unknown)
                throw new Exception($"FAIL: stale recognition at cell {index} after reset");
        Check(true, "tracking reset clears all nine cells");
    }

    static void AddLightingGradient(byte[] image, int n)
    {
        for (int y=0; y<n; y++) for (int x=0; x<n; x++)
        {
            int paper=130+110*x/(n-1);
            double inkOpacity=Math.Max(0,Math.Min(1,(240-image[y*n+x])/225.0));
            image[y*n+x]=(byte)Math.Round(paper-55*inkOpacity);
        }
    }

    static void BlurByDownsampling(byte[] image, int n, int width, int height)
    {
        var small = new double[width*height];
        int sx=n/width, sy=n/height;
        for (int y=0; y<height; y++) for (int x=0; x<width; x++)
        {
            double sum=0;
            for (int by=0; by<sy; by++) for (int bx=0; bx<sx; bx++) sum+=image[(y*sy+by)*n+x*sx+bx];
            small[y*width+x]=sum/(sx*sy);
        }
        for (int y=0; y<n; y++) for (int x=0; x<n; x++)
        {
            double u=Math.Max(0,Math.Min(width-1,(x+0.5)*width/n-0.5));
            double v=Math.Max(0,Math.Min(height-1,(y+0.5)*height/n-0.5));
            int x0=(int)u, y0=(int)v, x1=Math.Min(x0+1,width-1), y1=Math.Min(y0+1,height-1);
            double fx=u-x0, fy=v-y0;
            double top=small[y0*width+x0]*(1-fx)+small[y0*width+x1]*fx;
            double bottom=small[y1*width+x0]*(1-fx)+small[y1*width+x1]*fx;
            image[y*n+x]=(byte)Math.Round(top*(1-fy)+bottom*fy);
        }
    }
}
