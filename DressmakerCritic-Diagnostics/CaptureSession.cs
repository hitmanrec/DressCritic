using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DressmakerCritic.Diagnostics
{
    public sealed class CaptureSession
    {
        public readonly string directory;
        public readonly CaptureManifest manifest;
        public CaptureSession(string directory, string id)
        {
            this.directory = directory;
            Directory.CreateDirectory(directory);
            manifest = new CaptureManifest { captureId = id, startedAtUtc = DateTime.UtcNow.ToString("O") };
            File.WriteAllText(Path.Combine(directory, ".capture-in-progress"), id);
            Write("manifest.json", manifest);
        }
        public void Write(string file, object value) => File.WriteAllText(Path.Combine(directory, file), DiagnosticJson.Serialize(value), System.Text.Encoding.UTF8);
        public bool Stage(string name, Action action)
        {
            var timer = Stopwatch.StartNew(); var stage = new CaptureStage { name = name };
            try { action(); stage.status = "ok"; return true; }
            catch (Exception e) { stage.status = "failed"; stage.detail = e.GetType().Name + ": " + e.Message; return false; }
            finally { stage.elapsedMs = timer.Elapsed.TotalMilliseconds; manifest.stages.Add(stage); }
        }
        public async Task StageAsync(string name, Func<Task> action)
        {
            var timer = Stopwatch.StartNew(); var stage = new CaptureStage { name = name };
            try { await action(); stage.status = "ok"; }
            catch (Exception e) { stage.status = "failed"; stage.detail = e.GetType().Name + ": " + e.Message; }
            finally { stage.elapsedMs = timer.Elapsed.TotalMilliseconds; manifest.stages.Add(stage); }
        }
        public static string Hash(string file)
        { using (var hash = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        public void Finish()
        {
            manifest.finishedAtUtc = DateTime.UtcNow.ToString("O"); manifest.complete = true;
            foreach (string file in Directory.GetFiles(directory).Where(p => Path.GetFileName(p) != "manifest.json" && Path.GetFileName(p) != ".capture-in-progress").OrderBy(p => p))
                manifest.files.Add(new CaptureFile { file = Path.GetFileName(file), bytes = checked((int)new FileInfo(file).Length), sha256 = Hash(file) });
            Write("manifest.json", manifest);
            File.Delete(Path.Combine(directory, ".capture-in-progress"));
        }
    }
}
