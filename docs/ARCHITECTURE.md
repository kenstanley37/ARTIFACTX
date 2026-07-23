# Architectural Specification

## 1. Stateless Parsing Layer (Core)
To process the massive raw JSON payloads found in NMS saves (`.hg` files), the Core engine treats operations as entirely stateless. Using `System.Text.Json`, it reads streams linearly to parse specific blocks (e.g., Inventory, Player State, Discoveries) without keeping the entire tree duplicated across volatile memory.

## 2. Relational Mapping Data Layer (Data)
Instead of holding complex, nested JSON structural blobs in memory during an editing session, the data layer normalizes specific substructures into a local transient SQLite instance via EF Core. This allows fast queries, item filtering, and safe transactional boundaries.

## 3. Patching Engine
When saving changes, the application calculates a structural delta (diff) against the original file, modifying only targeted lines/nodes. This prevents full document corruption risks.