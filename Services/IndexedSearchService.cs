namespace FileSearchTool.Services;

public class IndexedSearchService
{
    // TODO 0: Tilføj FileCrawler dependency

// TODO 1: I BuildIndexAsync(rootFolder):
// - foreach file i crawler.GetFiles(rootFolder)
// - brug TextExtractorFactory.Create(file).ExtractText(file)
// - gem midlertidigt i en Dictionary<string, string> (path -> extractedText)
//   (det er kun for at verificere pipeline, Lucene kommer bagefter)

// TODO 2: Lav en simpel SearchAsync(query) der søger i dictionary-teksterne (midlertidigt)
// - så kan du teste end-to-end uden Lucene

}

// TODO 1: Tilføj dependency til:
// - FileCrawler
// - TextExtractorFactory (ny)
// - LuceneIndexService (ny)
// - LuceneSearchService eller LuceneQueryBuilder (ny)

// TODO 2 (BuildIndexAsync):
// - Find alle filer via _crawler.GetFiles(rootFolder)
// - For hver fil: brug TextExtractorFactory til at ExtractText(path)
// - Tilføj dokument til Lucene index (felter: path, content, evt. filename/extension)
// - Gem index på disk (ikke kun i memory)

// TODO 3 (SearchAsync):
// - Byg en Lucene query ud fra brugerens input:
// - phrase query på hele input (boost højt)
// - OR/SHOULD på hvert token i input (boost lavere)
// - Kør search i index og få top N hits
// - For hvert hit: find path + lav snippet

// TODO 4 (Snippet):
// - Hent noget tekst at lave snippet fra:
// - enten via stored field i Lucene (hvis du gemmer content i index)
// - eller ved at gen-extracte text fra filen (langsommere men simpelt)
// - Brug SnippetMaker (opdateret) til at lave et kort snippet

// TODO 5: Error handling
// - PDF/DOCX kan fejle; sørg for at du ikke crasher hele indexing pga én fil