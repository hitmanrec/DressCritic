using System;
using DressmakerCritic.Diagnostics;

static class EvaluationSelectionTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run()
    {
        var selection = new EvaluationSelection();
        object book = new object(), a = new object(), b = new object();
        Check(!selection.Accepts(selection.Revision, null), "hidden UI cannot accept an assessment");
        selection.Observe(book, a, 0);
        long ticket = selection.Revision;
        Check(selection.Accepts(ticket, a) && !selection.Accepts(ticket, b), "assessment must match captured garment state");
        Check(!selection.Observe(book, a, 0) && selection.Accepts(ticket, a), "unchanged selection keeps pending assessment");
        selection.Observe(book, b, 1);
        Check(!selection.Accepts(ticket, a), "switching dress discards late display update");
        selection.Observe(book, a, 0);
        Check(!selection.Accepts(ticket, a), "switching back does not resurrect an old request");
        ticket = selection.Revision;
        selection.Observe(null, null, -1);
        selection.Observe(book, a, 0);
        Check(!selection.Accepts(ticket, a), "leaving and reopening history invalidates pending display result");
        ticket = selection.Revision;
        selection.Observe(book, b, 0);
        Check(!selection.Accepts(ticket, a), "state replacement at the same index invalidates result");
    }
}
