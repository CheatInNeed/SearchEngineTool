# Changelog

All notable changes to this project will be documented in this file.

---

## [0.9.0] - Advanced Ranking & PDF Preview

### Added
- Two-stage search architecture (Lucene + ReRankScorer)
- Proximity window-based scoring
- Density-based scoring
- Exact phrase + near phrase bonuses
- Sub-phrase bonus (bigrams + trigrams inside query)
- IDF-based coverage scoring
- Early-position scoring
- Freshness metadata boost
- Filetype and folder-depth priors
- Optional LTR-style linear model (ltr_weights.json)
- PDF preview with:
    - Phrase detection
    - Token detection
    - Hit scoring
    - Sidebar sorted by best hit
    - Highlight overlays
    - Zoom support

### Improved
- Incremental indexing
- Hash-based per-folder index isolation
- Infix n-gram search field
- Stopword support (EN + DA)
- Synonym expansion
- Snowball stemming (EN + DA)

### Fixed
- Snippet clustering overlapping issues
- Hit ordering in PDF preview
- Result ordering based on FinalScore

---

## [0.8.0] - Retrieval Improvements

### Added
- Phrase query boosting
- Loose phrase slop queries
- All-terms BooleanQuery
- Minimum-should-match logic
- Infix search support
- Analyzer factory abstraction

---

## [0.7.0] - Basic Lucene Integration

### Added
- Lucene indexing service
- Lucene search service
- Basic UI integration
- Folder crawler
- Text extraction abstraction

---

## [0.5.0] - Initial Prototype

### Added
- Naive search implementation
- Simple snippet generation
- Basic Avalonia UI
