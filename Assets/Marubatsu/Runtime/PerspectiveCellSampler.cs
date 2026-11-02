using System;

namespace Marubatsu
{
    /// <summary>Maps a unit square to a convex source quadrilateral; bilinear grayscale sampling.</summary>
    public static class PerspectiveCellSampler
    {
        // Source corners: top-left, top-right, bottom-right, bottom-left. Image origin is top-left.
        public static bool Sample(byte[] source, int width, int height, double[] corners, byte[] destination, int size)
        {
            if (source == null || source.Length != width*height || corners == null || corners.Length != 8 ||
                destination == null || destination.Length != size*size || size < 2 || width < 2 || height < 2) return false;
            for (int i = 0; i < 4; i++)
            {
                double x = corners[i*2], y = corners[i*2+1];
                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y) ||
                    x < 0 || x >= width-1 || y < 0 || y >= height-1) return false;
            }
            double sign = 0;
            for (int i = 0; i < 4; i++)
            {
                int j = (i+1)%4, k = (i+2)%4;
                double cross = (corners[j*2]-corners[i*2])*(corners[k*2+1]-corners[j*2+1])-
                    (corners[j*2+1]-corners[i*2+1])*(corners[k*2]-corners[j*2]);
                if (Math.Abs(cross) < 1 || (i > 0 && cross*sign <= 0)) return false;
                sign = cross;
            }
            double x0=corners[0], y0=corners[1], x1=corners[2], y1=corners[3];
            double x2=corners[4], y2=corners[5], x3=corners[6], y3=corners[7];
            double dx1=x1-x2, dx2=x3-x2, dx3=x0-x1+x2-x3;
            double dy1=y1-y2, dy2=y3-y2, dy3=y0-y1+y2-y3;
            double determinant=dx1*dy2-dx2*dy1;
            if (Math.Abs(determinant) < 1e-8) return false;
            double g=(dx3*dy2-dx2*dy3)/determinant, h=(dx1*dy3-dx3*dy1)/determinant;
            double a=x1-x0+g*x1, b=x3-x0+h*x3, d=y1-y0+g*y1, e=y3-y0+h*y3;
            for (int row = 0; row < size; row++)
                for (int col = 0; col < size; col++)
                {
                    double u=(double)col/(size-1), v=(double)row/(size-1), w=g*u+h*v+1;
                    if (Math.Abs(w) < 1e-8) return false;
                    double x=(a*u+b*v+x0)/w, y=(d*u+e*v+y0)/w;
                    if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || x >= width-1 || y < 0 || y >= height-1) return false;
                    int ix=(int)x, iy=(int)y;
                    double fx=x-ix, fy=y-iy;
                    double top=source[iy*width+ix]*(1-fx)+source[iy*width+ix+1]*fx;
                    double bottom=source[(iy+1)*width+ix]*(1-fx)+source[(iy+1)*width+ix+1]*fx;
                    destination[row*size+col]=(byte)Math.Round(top*(1-fy)+bottom*fy);
                }
            return true;
        }
    }
}
