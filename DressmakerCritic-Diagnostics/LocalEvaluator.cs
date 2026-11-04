using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DressmakerCritic.Diagnostics
{
    [DataContract] public sealed class ModelReplyMessage { [DataMember] public string content; }
    [DataContract] public sealed class ModelReplyChoice { [DataMember] public ModelReplyMessage message; [DataMember] public string finish_reason; }
    [DataContract] public sealed class ModelReplyEnvelope { [DataMember] public ModelReplyChoice[] choices; }
    public static class AssessmentValidator
    {
        public static void Validate(string json, EvaluationInput input, EvaluationTask task = EvaluationTask.Jury)
        {
            if (task == EvaluationTask.Jury) JuryContract.ReadJury(json, input.captureId);
            else JuryContract.ApplyContext(json, input, new JuryAssessment());
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
        public static async Task<string> Run(HttpClient client, string endpoint, string request, EvaluationInput input, CancellationToken cancellation, Action<string> observeResponse = null, EvaluationTask task = EvaluationTask.Jury)
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
                        var choice = envelope?.choices?.FirstOrDefault();
                        if (choice?.finish_reason == "length") throw new InvalidDataException("Model output token limit reached; no complete assessment accepted. Use reasoning_effort=none for Ollama.");
                        string result = choice?.message?.content;
                        if (string.IsNullOrWhiteSpace(result)) throw new InvalidDataException("Model returned no final assessment content; reasoning is not an assessment.");
                        AssessmentValidator.Validate(result, input, task);
                        return result;
                    }
                }
            }
        }
    }
}
