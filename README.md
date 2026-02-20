# FileSearchTool

A desktop search engine built with C#, Lucene.NET and Avalonia UI.

This project implements a two-stage retrieval + re-ranking architecture,
including proximity scoring, phrase detection, metadata boosting, and PDF hit visualization.

---

## 🔍 What the program can do

### 📂 Indexing
- Recursive folder indexing
- Incremental indexing (skips unchanged files)
- Automatic removal of deleted files
- Supported file types:
    - `.pdf`
    - `.txt`
- Per-folder Lucene index stored in AppData

---

### 🔎 Retrieval (Stage 1 – Lucene)
- Fielded search across:
    - title
    - filename
    - content
- Exact phrase boosting
- Loose phrase boosting
- All-terms match boosting
- Token matching with minimum-should-match
- Infix n-gram search (substring matching)
- Synonym support (via synonyms.txt)
- English + Danish stopwords
- English + Danish stemming (Snowball)

---

### 🧠 Re-Ranking (Stage 2 – Custom Scoring)

Each document is re-scored using:

- Lucene score
- Proximity scoring (window-based)
- Exact phrase bonus
- Near phrase bonus
- Sub-phrase bonus (phrase inside phrase)
- Density scoring
- All-terms bonus
- IDF-based coverage score
- Early-position boost
- Freshness boost
- Filetype prior
- Folder depth prior
- Optional linear LTR-style weight model

Final results are sorted by `FinalScore`.

---

### 📄 Snippet Generation
- Multi-snippet per document
- Cluster-based snippet extraction
- Phrase highlighting
- Whole-word highlighting
- Infix highlighting

---

### 📑 PDF Preview
- Renders PDF pages
- Finds phrase + token hits
- Scores hits individually
- Sidebar sorted by best hit
- Highlight overlay with active hit emphasis
- Zoom support
- Hit navigation
- Page navigation

---

### 📊 Debug / Transparency
Each result exposes:

- LuceneScore
- ProximityScore
- PhraseBonus
- SubPhraseBonus
- DensityScore
- AllTermsBonus
- FreshnessScore
- FiletypeScore
- FolderDepthScore
- EarlyScore
- CoverageScore
- FinalScore

This makes ranking behavior explainable and tunable.
