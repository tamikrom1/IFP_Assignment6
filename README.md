# QuoteApp - Functional Error Handling

**Assignment 6:** Exception Handling in FP 
**Group:** IT-2504
**Team Members:** 
1. Tamerlan Iskakov
2. Madina Kairbayeva

## 1. Project Overview

### Workflow
The application processes quotes for products based on a catalog loaded from a JSON file. The core workflow (`QuoteWorkflow`) searches for a requested item, validates business rules (quantity, stock, availability), resolves optional discount codes, calculates the final price, and produces a `QuoteView`. On confirmation, it safely reserves the requested quantity and returns an updated `Catalog` state without mutating the original.

### Input
- **Catalog Data:** A JSON file containing an array of products (SKU, Name, UnitPrice, Stock, Active status) and a dictionary of discount codes.
- **User Request:** A `QuoteRequest` containing a Product SKU, requested Quantity (integer), and an optional Discount Code.

### Expected Output
- **Success:** A formatted summary string showing the quote details (quantity, name, unit price, applied discount, final total, and status).
- **Failure:** A clear, human-readable error message indicating exactly why the request was rejected (domain rule) or why the application couldn't proceed (operational/file error), without crashing.

### Domain Rules (Failures)
1. **Invalid Quantity:** The requested quantity must be greater than zero.
2. **Item Not Found:** The requested SKU must exist in the catalog.
3. **Insufficient Stock:** The available stock must be greater than or equal to the requested quantity.
4. **Item Discontinued:** The product's `Active` flag must be true.
5. **Unknown Discount Code:** If a discount code is provided, it must exist in the catalog.
6. **Invalid State Transition:** A quote can only be confirmed if it is currently in a "Draft" state.

---

## 2. Demonstration Instructions

To run the application, use the following commands from the root directory:

**Normal / Expected Path:**
Run a successful quote generation applying a valid 10% discount:
`dotnet run --project src/QuoteApp quote LAPTOP-15 2 WELCOME10`

**Domain Failures (Rejections):**
- Invalid Quantity: `dotnet run --project src/QuoteApp quote MOUSE-01 0`
- Item Not Found: `dotnet run --project src/QuoteApp quote NOPE-999 1`
- Insufficient Stock: `dotnet run --project src/QuoteApp quote CABLE-USB 1`

**Operational Failures (File Problems):**
- Missing File: `dotnet run --project src/QuoteApp quote MOUSE-01 1 --catalog data/does-not-exist.json`
- Malformed JSON: `dotnet run --project src/QuoteApp quote MOUSE-01 1 --catalog data/malformed.json`

**All-in-one Demo:**
`dotnet run --project src/QuoteApp demo`

---

## 3. Requirement Mapping

| Requirement | Implemented In |
| :--- | :--- |
| **Domain Failures (Task 1)** | `Common/AppError.cs` (DomainErrors), `Domain/QuoteWorkflow.cs` |
| **Model Missing Values (Task 2)**| `Common/Maybe.cs`, `Domain/Catalog.cs` (FindProduct, FindDiscountPercent) |
| **Map and Bind (Task 3)** | `Common/Result.cs` (ResultExtensions), chained in `QuoteWorkflow.CreateQuote` |
| **Operational Exceptions (Task 4)**| `Infrastructure/CatalogLoader.cs` (Catching `FileNotFoundException`, `JsonException`) |

---

## 4. Tests and Outcomes

**Latest Test Outcome:** 19/19 Tests Passed (0 Failed).
Run tests via: `dotnet run --project tests/QuoteApp.Tests`

| Case Category | Test Method(s) | Expected Result |
| :--- | :--- | :--- |
| **Complete Success** | `CompleteSuccess_ChainProducesCorrectValue` | The Result chain calculates the correct total, updates status, and properly reduces stock in the new catalog. |
| **Domain Failures** | `DomainFailure_InvalidQuantity`, `_ItemNotFound`, `_InsufficientStock` | Returns distinct `AppError` objects with correct `ErrorKind.Domain` and contextual messages. |
| **Lookup (Present/Absent)** | `Lookup_PresentAndAbsent`, `Lookup_LegitimatePrimitiveValues` | Properly distinguishes missing values from legitimate `0` stock or `0` discount values. |
| **Map/Bind Success** | `Map_Success`, `Bind_Success` | `Map` transforms the value; `Bind` continues the operation safely. |
| **Failure Propagation** | `FailurePropagation_MapAndBind` | A counter proves that functions passed to Map/Bind are strictly skipped when an error is present. |
| **File Boundaries** | `MissingFile_Reports`, `MalformedJson_BecomesClearFailure` | Catches exceptions at the boundary, returns `ErrorKind.Operational`, and prevents partial state mutation. |

---

## 5. Team Contributions

**Tamerlan Iskakov**
- **Logic:** Implemented the core Functional programming primitives (`Result.cs`, `Maybe.cs`, `AppError.cs`). Built the domain logic in `QuoteWorkflow.cs` and `Catalog.cs`.
- **Tests:** Wrote `TestFramework.cs` and `Tests1.cs`, covering domain outcomes, Maybe/Result lookup behavior, and Map/Bind failure propagation.
- **Documentation:** Mapped requirements and documented the domain rules.

**Madina Kairbayeva**
- **Logic:** Implemented the infrastructure boundary layer (`CatalogLoader.cs`, `Program.cs` console interface), safely handling `System.IO` and `System.Text.Json` exceptions.
- **Tests:** Wrote `Tests2.cs`, focusing on file boundary failures, malformed content edge cases, and ensuring `Reserve` operations preserve immutability.
- **Documentation:** Authored the demonstration instructions and test outcomes overview.