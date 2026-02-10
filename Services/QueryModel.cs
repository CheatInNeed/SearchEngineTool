using System;
using System.Collections.Generic;

namespace FileSearchTool.Services;

// TODO: QueryModel er din "AST" for søgeforespørgsler.
//
// Designmål:
// - QueryParser laver QueryNode
// - QueryEvaluator evaluerer QueryNode mod InvertedIndex

public abstract record QueryNode
{
    // TODO: (valgfrit) helper til debugging, fx ToString override eller NodeType
}

// TODO 1: Repræsenter et enkelt søgeord (term)
public sealed record TermNode(string Term) : QueryNode
{
    // TODO: overvej at normalisere Term (lowercase) enten her eller i parseren
}

// TODO 2: Repræsenter AND mellem to queries
public sealed record AndNode(QueryNode Left, QueryNode Right) : QueryNode
{
}

// TODO 3: Repræsenter OR mellem to queries
public sealed record OrNode(QueryNode Left, QueryNode Right) : QueryNode
{
}

public sealed record PhraseNode(string Phrase) : QueryNode
{
}

// TODO 4 (valgfrit):
// Understøt parenteser / grouping ved at parseren bygger træet rigtigt
// (du behøver ikke en specifik node for parenteser — de styrer kun parse-strukturen)