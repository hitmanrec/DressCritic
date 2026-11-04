using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace DressmakerCritic.Diagnostics
{
    [DataContract] public sealed class ModelReplyMessage { [DataMember] public string content; }
    [DataContract] public sealed class ModelReplyChoice { [DataMember] public ModelReplyMessage message; }
    [DataContract] public sealed class ModelReplyEnvelope { [DataMember] public ModelReplyChoice[] choices; }
    public static class AssessmentValidator
    {
        private static void Members(XElement value, params string[] names)
        {
            if (value == null || value.Attribute("type")?.Value != "object" || value.Elements().Count() != names.Length ||
                names.Any(n => value.Elements(n).Count() != 1) || value.Elements().Any(e => !names.Contains(e.Name.LocalName)))
                throw new InvalidDataException("Unexpected assessment members");
        }
        private static string String(XElement element)
        {
            if (element?.Attribute("type")?.Value != "string") throw new InvalidDataException("Expected string");
            return element.Value;
        }
        private static void Strings(XElement element)
        {
            if (element?.Attribute("type")?.Value != "array" || element.Elements().Count() > 32) throw new InvalidDataException("Invalid observation list");
            foreach (var item in element.Elements()) if (String(item).Length > 4096) throw new InvalidDataException("Observation too long");
        }
        public static void Validate(string json, EvaluationInput input)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > 65536) throw new InvalidDataException("Assessment size limit");
            XElement root;
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), new XmlDictionaryReaderQuotas { MaxDepth = 16, MaxStringContentLength = 65536, MaxArrayLength = 65536 }))
                root = XElement.Load(reader);
            Members(root, "schemaVersion", "visualQuality", "briefCompliance", "deterministicGameRequirements", "limitations");
            if (String(root.Element("schemaVersion")) != "1.0.0") throw new InvalidDataException("Unknown assessment version");
            var visual = root.Element("visualQuality"); Members(visual, "status", "score", "observations", "uncertainties");
            string visualStatus = String(visual.Element("status"));
            if (visualStatus != "assessed" && visualStatus != "unknown") throw new InvalidDataException("Invalid visual status");
            var score = visual.Element("score");
            if (visualStatus == "unknown") { if (score.Attribute("type")?.Value != "null") throw new InvalidDataException("Unknown visual quality must have null score"); }
            else
            {
                double number;
                if (score.Attribute("type")?.Value != "number" || !double.TryParse(score.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > 100)
                    throw new InvalidDataException("Invalid visual score");
            }
            Strings(visual.Element("observations")); Strings(visual.Element("uncertainties"));
            var brief = root.Element("briefCompliance"); Members(brief, "status", "scope", "observations", "uncertainties");
            string status = String(brief.Element("status"));
            if (!new[] {"consistent", "inconsistent", "unknown"}.Contains(status) || String(brief.Element("scope")) != "provided-summary-only") throw new InvalidDataException("Invalid brief result");
            if (input.order.brief.status != "verified-guid" && input.order.brief.status != "asset-key")
                if (status != "unknown") throw new InvalidDataException("Cannot judge an unknown brief");
            Strings(brief.Element("observations")); Strings(brief.Element("uncertainties"));
            var game = root.Element("deterministicGameRequirements"); Members(game, "status");
            if (String(game.Element("status")) != "not-evaluated") throw new InvalidDataException("Fabricated deterministic compliance");
            Strings(root.Element("limitations"));
        }
    }
    public static class LocalEvaluator
    {
        public static Uri ValidateEndpoint(string endpoint)
        {
            Uri uri;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out uri) || !uri.IsLoopback || (uri.Scheme != "http" && uri.Scheme != "https") ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("Only explicit loopback model endpoints without embedded credentials are supported");
            return uri;
        }
        public static async Task<string> Probe(HttpClient client, string endpoint, CancellationToken cancellation)
        {
            var uri = ValidateEndpoint(endpoint);
            var builder = new UriBuilder(uri);
            const string suffix = "/chat/completions";
            builder.Path = uri.AbsolutePath.EndsWith(suffix, StringComparison.Ordinal) ? uri.AbsolutePath.Substring(0, uri.AbsolutePath.Length - suffix.Length) + "/models" : "/v1/models";
            using (var response = await client.GetAsync(builder.Uri, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var bytes = new byte[8192]; int count;
                while ((count = await stream.ReadAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false)) > 0)
                { if (buffer.Length + count > 131072) throw new InvalidDataException("Model metadata exceeds limit"); buffer.Write(bytes, 0, count); }
                response.EnsureSuccessStatusCode(); return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }
        public static async Task<string> Run(HttpClient client, string endpoint, string request, EvaluationInput input, CancellationToken cancellation, Action<string> observeResponse = null)
        {
            var uri = ValidateEndpoint(endpoint);
            using (var message = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                message.Content = new StringContent(request, Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                {
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var buffer = new MemoryStream())
                    {
                        var chunk = new byte[8192]; int size;
                        while ((size = await stream.ReadAsync(chunk, 0, chunk.Length, cancellation).ConfigureAwait(false)) > 0)
                        {
                            if (buffer.Length + size > 262144) throw new InvalidDataException("Model response exceeds limit");
                            buffer.Write(chunk, 0, size);
                        }
                        observeResponse?.Invoke(Encoding.UTF8.GetString(buffer.ToArray()));
                        response.EnsureSuccessStatusCode();
                        buffer.Position = 0;
                        var envelope = (ModelReplyEnvelope)new DataContractJsonSerializer(typeof(ModelReplyEnvelope)).ReadObject(buffer);
                        string result = envelope?.choices?.FirstOrDefault()?.message?.content;
                        AssessmentValidator.Validate(result, input);
                        return result;
                    }
                }
            }
        }
    }
}
