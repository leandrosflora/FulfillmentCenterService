CREATE TABLE fulfillment_centers (
    id UUID PRIMARY KEY,
    code VARCHAR(30) NOT NULL,
    name VARCHAR(200) NOT NULL,
    region VARCHAR(100) NOT NULL,
    time_zone_id VARCHAR(100) NOT NULL,
    status VARCHAR(40) NOT NULL,
    maximum_weight_kg NUMERIC(10,3) NULL,
    maximum_cubic_weight_kg NUMERIC(10,3) NULL,
    CONSTRAINT uq_fulfillment_centers_code UNIQUE (code)
);

CREATE TABLE capacity_slots (
    id UUID PRIMARY KEY,
    fulfillment_center_id UUID NOT NULL,
    operation_date DATE NOT NULL,
    mode VARCHAR(40) NOT NULL,
    "TotalCapacityUnits" INTEGER NOT NULL,
    "ReservedCapacityUnits" INTEGER NOT NULL DEFAULT 0,
    "ConsumedCapacityUnits" INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT uq_capacity_slots_key UNIQUE (fulfillment_center_id, operation_date, mode),
    CONSTRAINT ck_capacity_slots_allocated_capacity CHECK (
        "TotalCapacityUnits" >= 0
        AND "ReservedCapacityUnits" >= 0
        AND "ConsumedCapacityUnits" >= 0
        AND "ReservedCapacityUnits" + "ConsumedCapacityUnits" <= "TotalCapacityUnits"
    )
);

CREATE TABLE capacity_reservations (
    id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    fulfillment_center_id UUID NOT NULL,
    operation_date DATE NOT NULL,
    mode VARCHAR(40) NOT NULL,
    reserved_capacity_units INTEGER NOT NULL,
    status VARCHAR(30) NOT NULL,
    idempotency_key VARCHAR(200) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    confirmed_at TIMESTAMPTZ NULL,
    released_at TIMESTAMPTZ NULL,
    CONSTRAINT uq_capacity_reservations_idempotency UNIQUE (idempotency_key)
);

CREATE INDEX idx_capacity_reservations_expiry ON capacity_reservations (status, expires_at);

CREATE TABLE center_coverages (
    id UUID PRIMARY KEY,
    fulfillment_center_id UUID NOT NULL,
    postal_code_from VARCHAR(20) NOT NULL,
    postal_code_to VARCHAR(20) NOT NULL,
    mode VARCHAR(40) NOT NULL
);

CREATE INDEX idx_center_coverages_postal ON center_coverages (postal_code_from, postal_code_to, mode);

CREATE TABLE seller_center_enrollments (
    id UUID PRIMARY KEY,
    seller_id UUID NOT NULL,
    fulfillment_center_id UUID NOT NULL,
    mode VARCHAR(40) NOT NULL,
    CONSTRAINT uq_seller_center_enrollments_key UNIQUE (seller_id, fulfillment_center_id, mode)
);

CREATE TABLE center_operation_schedules (
    id UUID PRIMARY KEY,
    fulfillment_center_id UUID NOT NULL,
    operation_date DATE NOT NULL,
    mode VARCHAR(40) NOT NULL,
    CONSTRAINT uq_center_operation_schedules_key UNIQUE (fulfillment_center_id, operation_date, mode)
);

CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    event_type VARCHAR(200) NOT NULL,
    payload_json TEXT NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NULL
);

CREATE INDEX idx_outbox_messages_dispatch ON outbox_messages (processed_at, occurred_at);
