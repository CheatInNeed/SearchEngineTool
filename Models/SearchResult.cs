namespace FileSearchTool.Models;

public record SearchResult(string FilePath, string Snippet);

// TODO: Overvej at udvide SearchResult med:
// - double Score
// - stri:contentReference[oaicite:22]{index=22}rase" vs "term hit")
// (men gør det først når du har basis i gan:contentReference[oaicite:23]{index=23}ad du ender med (så du ved “hvornår du er færdig”)

/*

Når ovenstående er gjort, er du på rette spor ift. din ønskede UX:
   
   - Brugeren skriver bare: `operating systems`
   - Systemet søger:
     - phrase “operating systems” (høj prioritet)
     - og ord-hits på “operating” og “systems”
   - Dokumenttyper:
     - PDF ✅ (via extractor)
     - DOCX ✅ (via extractor)
     - TXT ✅

*/