CREATE TABLE app_instance (
    id TEXT PRIMARY KEY, created_at TEXT NOT NULL
) STRICT;
CREATE TABLE parties (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 200),
    country_code TEXT NOT NULL CHECK(length(country_code) = 2),
    default_terms_days INTEGER NOT NULL CHECK(default_terms_days BETWEEN 0 AND 3650)
) STRICT;
CREATE TABLE invoices (
    id TEXT PRIMARY KEY,
    party_id TEXT NOT NULL REFERENCES parties(id),
    number TEXT NOT NULL COLLATE NOCASE CHECK(length(trim(number)) BETWEEN 1 AND 100),
    invoice_date TEXT NOT NULL,
    original_cents INTEGER NOT NULL CHECK(original_cents >= 0),
    bank_contract_cents INTEGER NOT NULL CHECK(bank_contract_cents >= 0),
    final_leg_bl_date TEXT,
    terms_days INTEGER NOT NULL CHECK(terms_days BETWEEN 0 AND 3650),
    version INTEGER NOT NULL DEFAULT 1 CHECK(version > 0),
    UNIQUE(party_id, number)
) STRICT;
CREATE TABLE receipts (
    id TEXT PRIMARY KEY,
    party_id TEXT NOT NULL REFERENCES parties(id),
    receipt_date TEXT NOT NULL,
    amount_cents INTEGER NOT NULL CHECK(amount_cents > 0),
    reference TEXT NOT NULL CHECK(length(reference) <= 200)
) STRICT;
CREATE TABLE receipt_allocations (
    receipt_id TEXT NOT NULL REFERENCES receipts(id),
    invoice_id TEXT NOT NULL REFERENCES invoices(id),
    amount_cents INTEGER NOT NULL CHECK(amount_cents > 0),
    PRIMARY KEY(receipt_id, invoice_id)
) STRICT;
CREATE INDEX ix_invoices_party_date ON invoices(party_id, invoice_date);
CREATE INDEX ix_receipts_party_date ON receipts(party_id, receipt_date);
CREATE INDEX ix_allocations_invoice ON receipt_allocations(invoice_id);
CREATE TABLE audit_events (
    id TEXT PRIMARY KEY, entity_id TEXT NOT NULL, action TEXT NOT NULL, created_at TEXT NOT NULL
) STRICT;
-- Service transactions validate allocations; these guards also protect alternate SQL callers.
CREATE TRIGGER allocation_insert_guard BEFORE INSERT ON receipt_allocations BEGIN
    SELECT RAISE(ABORT, 'Allocation party mismatch')
    WHERE (SELECT party_id FROM receipts WHERE id = NEW.receipt_id)
       <> (SELECT party_id FROM invoices WHERE id = NEW.invoice_id);
    SELECT RAISE(ABORT, 'Receipt over-allocation')
    WHERE NEW.amount_cents > (SELECT amount_cents FROM receipts WHERE id = NEW.receipt_id)
       - COALESCE((SELECT SUM(amount_cents) FROM receipt_allocations WHERE receipt_id = NEW.receipt_id), 0);
    SELECT RAISE(ABORT, 'Invoice over-allocation')
    WHERE NEW.amount_cents > (SELECT bank_contract_cents FROM invoices WHERE id = NEW.invoice_id)
       - COALESCE((SELECT SUM(amount_cents) FROM receipt_allocations WHERE invoice_id = NEW.invoice_id), 0);
END;
