using System;
using DressmakerCritic.Diagnostics;

static class NotebookPageGeometryTests
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        Check(NotebookPageGeometry.LeftForMarker(900, 820, 500) == 80, "facing right leaf resolves to native left Photo Mode page");
        Check(NotebookPageGeometry.LeftForMarker(80, 820, 500) == 80, "already-correct left leaf does not move twice");
        Check(NotebookPageGeometry.LeftForMarker(-400, 800, -800) == -1200, "left page selection works with centered negative local coordinates");
        Check(NotebookPageGeometry.LeftForMarker(-1200, 800, -800) == -1200, "centered left page remains stable");
        Check(double.IsNaN(NotebookPageGeometry.LeftForMarker(900, 0, 500)), "zero-sized page cannot place jury at spine");
        Check(double.IsNaN(NotebookPageGeometry.LeftForMarker(900, 820, -1000)), "distant marker is a binding conflict instead of arbitrary translation");
        Check(double.IsNaN(NotebookPageGeometry.LeftForMarker(double.NaN, 820, 500)) &&
            double.IsNaN(NotebookPageGeometry.LeftForMarker(900, double.PositiveInfinity, 500)), "non-finite geometry is rejected");
    }
}
