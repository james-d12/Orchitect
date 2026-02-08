FROM rust:slim AS builder
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        pkg-config \
        libssl-dev && \
    rm -rf /var/lib/apt/lists/*
COPY . /build
WORKDIR /build
RUN cargo build --release

FROM rust:slim AS runtime
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libssl3 && \
    rm -rf /var/lib/apt/lists/*
COPY --from=builder /build /build
WORKDIR /build
ENTRYPOINT ["cargo", "run", "--release"]
