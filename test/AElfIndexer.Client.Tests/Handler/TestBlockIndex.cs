using System;
using AElf.Indexing.Elasticsearch;
using AElfIndexer.Client;
using Nest;

namespace AElfIndexer.Handler;

public class TestBlockIndex : AElfIndexerClientEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; }
    public DateTime BlockTime { get; set; }
    [Keyword] public string SignerPubkey { get; set; }
    [Keyword] public string Signature { get; set; }
}