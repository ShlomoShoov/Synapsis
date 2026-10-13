using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Synapsis.Data.Dal;
using Synapsis.Data.Models.Options;

namespace Synapsis.Setup;

/// <summary>
/// Creates the index of the search documents in Elasticsearch, with an explicit mapping: exactly the fields of
/// SearchDocument (Synapsis.Data), and "dynamic: false", so an unknown field is never added to the mapping by itself.
/// </summary>
public sealed class CreateRecordsIndexStep : ISetupStep
{
    private readonly ElasticContext _elasticContext;
    private readonly ElasticOptions _elasticOptions;
    private readonly EmbedderOptions _embedderOptions;

    /// <summary>Creates the step over the Elasticsearch connection, its index names and the embedder's vector size.</summary>
    public CreateRecordsIndexStep(ElasticContext elasticContext, ElasticOptions elasticOptions, EmbedderOptions embedderOptions)
    {
        _elasticContext = elasticContext;
        _elasticOptions = elasticOptions;
        _embedderOptions = embedderOptions;
    }

    /// <summary>Creates the index when it does not exist (an existing index keeps its mapping); throws when Elasticsearch refuses.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ElasticsearchClient client = _elasticContext.Client ?? throw new InvalidOperationException("Elastic:Url is empty: setup needs Elasticsearch.");

        // 1. Nothing to do when the index exists.
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse exists = await client.Indices.ExistsAsync(_elasticOptions.RecordsIndex, cancellationToken);
        if (exists.Exists)
        {
            return;
        }

        // 2. Create it: one shard and no copies (a single node), and the mapping below.
        CreateIndexRequest request = new CreateIndexRequest(_elasticOptions.RecordsIndex)
        {
            Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
            Mappings = RecordsMapping(_embedderOptions.Dimensions)
        };
        CreateIndexResponse created = await client.Indices.CreateAsync(request, cancellationToken);
        if (!created.IsValidResponse)
        {
            throw new InvalidOperationException("The records index could not be created: " + created.DebugInformation);
        }
    }

    /// <summary>
    /// The field types of a search document, by its JSON names. Codes are keywords (exact values for filters and
    /// counts); names and texts are full text; the vector is kept out of _source (it is most of the size; a rebuild
    /// computes it again); every measure becomes a number field by itself (the one dynamic part).
    /// </summary>
    public static TypeMapping RecordsMapping(int dimensions)
    {
        Properties fields = new Properties();
        AddIdentity(fields);
        AddText(fields, dimensions);
        AddTimeAndPlace(fields);
        fields.Add("measures", new ObjectProperty { Dynamic = DynamicMapping.True });
        fields.Add("entities", new KeywordProperty());
        return new TypeMapping
        {
            Dynamic = DynamicMapping.False,
            Source = new SourceField { Excludes = new[] { "text_vector" } },
            DynamicTemplates = MeasuresAsNumbers(),
            Properties = fields
        };
    }

    /// <summary>The identity fields, copied from the MongoDB envelope.</summary>
    private static void AddIdentity(Properties fields)
    {
        fields.Add("id", new KeywordProperty());
        fields.Add("id_key", new KeywordProperty());
        fields.Add("record_key", new KeywordProperty());
        fields.Add("state", new IntegerNumberProperty());
        fields.Add("kind", new KeywordProperty());
        fields.Add("type", new KeywordProperty());
        fields.Add("source", new KeywordProperty());
        fields.Add("known_at", new DateProperty());
    }

    /// <summary>The text fields: the name also as an exact keyword (name.raw), and the vector for semantic search.</summary>
    private static void AddText(Properties fields, int dimensions)
    {
        Properties nameFields = new Properties();
        nameFields.Add("raw", new KeywordProperty());
        fields.Add("name", new TextProperty { Fields = nameFields });
        fields.Add("title", new TextProperty());
        fields.Add("body", new TextProperty());
        fields.Add("text_vector", new DenseVectorProperty { Dims = dimensions, Index = true, Similarity = DenseVectorSimilarity.Cosine });
    }

    /// <summary>The time and place fields.</summary>
    private static void AddTimeAndPlace(Properties fields)
    {
        fields.Add("time_start", new DateProperty());
        fields.Add("time_precision", new KeywordProperty());
        fields.Add("week", new DateProperty());
        fields.Add("location", new GeoPointProperty());
        fields.Add("h3_r2", new KeywordProperty());
        fields.Add("h3_r3", new KeywordProperty());
        fields.Add("h3_r4", new KeywordProperty());
        fields.Add("h3_r5", new KeywordProperty());
        fields.Add("country", new KeywordProperty());
        fields.Add("subdivision", new KeywordProperty());
        fields.Add("logical_prefixes", new KeywordProperty());
    }

    /// <summary>
    /// Every field under "measures" is a double: without this rule, a measure first seen as 10 would become a whole-number
    /// field, and a later 5.4 would be cut to 5.
    /// </summary>
    private static List<IDictionary<string, DynamicTemplate>> MeasuresAsNumbers()
    {
        DynamicTemplate template = DynamicTemplate.Mapping(new DoubleNumberProperty());
        template.PathMatch = new[] { "measures.*" };
        Dictionary<string, DynamicTemplate> named = new Dictionary<string, DynamicTemplate>();
        named.Add("measures_as_numbers", template);
        List<IDictionary<string, DynamicTemplate>> templates = new List<IDictionary<string, DynamicTemplate>>();
        templates.Add(named);
        return templates;
    }
}
