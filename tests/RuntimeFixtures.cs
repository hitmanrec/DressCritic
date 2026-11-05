using System;
using System.IO;
using System.IO.Compression;

static class RuntimeFixtures
{
    public static string Read(string capture, string file)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        using (var input = File.OpenRead(Path.Combine(root, "tests/fixtures/runtime", capture, file + ".gz")))
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        using (var reader = new StreamReader(gzip)) return reader.ReadToEnd();
    }
}
