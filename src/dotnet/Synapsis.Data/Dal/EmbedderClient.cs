using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Synapsis.Data.Models.Options;
using Synapsis.Data.Models.Requests;
using Synapsis.Data.Models.Results;

namespace Synapsis.Data.Dal;

/// <summary>
/// The connection to the embedder service (Python, POST /embed), which turns a text into a vector of numbers that
/// stands for its meaning. The embedder cleans the text itself, the same way for records and for search questions.
/// One HTTP client per process. The indexer uses it now; the search will use it with the same settings.
/// </summary>
public sealed class EmbedderClient
{
    private readonly HttpClient _httpClient;
    private readonly EmbedderOptions _embedderOptions;

    /// <summary>Creates the HTTP client for the embedder's address in the settings.</summary>
    public EmbedderClient(EmbedderOptions embedderOptions)
    {
        _embedderOptions = embedderOptions;
        _httpClient = new HttpClient { BaseAddress = new Uri(embedderOptions.Url) };
    }

    /// <summary>
    /// The text's vector. Answers (vector, null); (null, null) when the text has no known word (the embedder gives a
    /// vector of zeros, which is left out); or (null, reason) when the embedder refuses the text. Throws when the
    /// embedder does not answer or fails (5xx), so the caller tries again later.
    /// </summary>
    public async Task<(float[]? Vector, string? Refusal)> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        // 1. Ask for the vector of the text, cut to the embedder's limit.
        EmbedRequest request = new EmbedRequest { Texts = new List<string> { CutToLimit(text, _embedderOptions.MaxTextLength) }, Model = _embedderOptions.Model };
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/embed", request, cancellationToken);

        // 2. A failure of the embedder itself is retried; a refusal of this text is answered.
        int status = (int)response.StatusCode;
        if (status >= 500)
        {
            throw new HttpRequestException("The embedder failed with " + status + ".");
        }
        if (!response.IsSuccessStatusCode)
        {
            string reason = await response.Content.ReadAsStringAsync(cancellationToken);
            return (null, "The embedder refused the text (" + status + "): " + reason);
        }

        // 3. A vector of zeros means no known word: no vector.
        EmbedAnswer? answer = await response.Content.ReadFromJsonAsync<EmbedAnswer>(cancellationToken);
        if (answer == null || answer.Vectors.Count == 0 || IsZero(answer.Vectors[0]))
        {
            return (null, null);
        }
        return (answer.Vectors[0], null);
    }

    /// <summary>
    /// The beginning of the text, at most the given number of characters. A character made of two parts (such as an
    /// emoji) is never split: the cut moves one place back.
    /// </summary>
    public static string CutToLimit(string text, int maxTextLength)
    {
        if (text.Length <= maxTextLength)
        {
            return text;
        }
        int length = maxTextLength;
        if (char.IsHighSurrogate(text[length - 1]))
        {
            length = length - 1;
        }
        return text.Substring(0, length);
    }

    /// <summary>True when every number of the vector is zero.</summary>
    public static bool IsZero(float[] vector)
    {
        foreach (float number in vector)
        {
            if (number != 0)
            {
                return false;
            }
        }
        return true;
    }
}
