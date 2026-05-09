# Technical Requirements - Transaction Audit Date

## Goal
Enable "Dual-Date" tracking for transactions to separate the **Trade Date** (when the asset was actually bought/sold) from the **Entry Date** (when the record was created in the system). This improves auditability by showing when a transaction was logged.

## Terminology
- **Buying/Selling Date** (Existing): The date of the actual financial event.
- **Adding Date** (New in UI): The date the transaction was entered into the system (Audit Date).

## Backend Changes

### Domain/Entities
- No changes needed. The `Transaction` entity already has `TransactionDate` and `CreatedAtUtc`.

### Application/DTOs
- **`TransactionResponse`**: Add `DateTime CreatedAtUtc { get; set; }`.

### Services
- **`TransactionService.cs`**: Update `MapToResponse` to include `CreatedAtUtc`.
- **`TransactionIngestService.cs`**: Update `MapToResponse` to include `CreatedAtUtc`.

## Frontend Changes (Angular)

### Core/Models
- **`Transaction` model**: Add `createdAtUtc: string`.

### UI: Transaction List (`TransactionsComponent`)
- Update the "Date" column to show both dates.
- **Primary Display**: `transactionDate` (Trade Date).
- **Secondary Display**: Show `createdAtUtc` (Adding Date) as a small, muted text below the primary date or in a tooltip (e.g., "Logged on: May 9, 2026").

### UI: Forms
- **`TransactionsComponent` (Edit/Create form)**:
  - Add a read-only field labeled "Adding Date".
  - For new transactions, it should default to the current date.
  - For existing transactions, it should show the actual `createdAtUtc`.
- **`AddInvestmentComponent` (MF, Stock, Gold, etc. forms)**:
  - Add a read-only field labeled "Adding Date" at the top of each asset form.
  - Defaults to today's date.

## Android Changes

### Types/DTOs
- **`TransactionResponse`**: Add `createdAtUtc: string`.

### UI: Transaction List (`TransactionsListScreen`)
- Add "Logged on: [Date]" to the transaction card, preferably in the subtitle area or as a new row in the content.

### UI: Edit Screen (`TransactionEditScreen`)
- Add a read-only field (using `TextInput` with `editable={false}` or a plain `Text` component) showing the "Adding Date".

## Verification Plan
1. **API**: Verify that `/api/transactions` and related endpoints return `createdAtUtc`.
2. **UI (Web/Mobile)**: Verify that when creating a new transaction, "Today" is displayed as the "Adding Date".
3. **UI (Web/Mobile)**: Verify that when viewing the transaction list, both the Trade Date and the Adding Date are visible.
4. **Data Integrity**: Ensure that the "Adding Date" remains the original creation timestamp even after updating the transaction (e.g., editing notes or quantity).
