# Recovery

Startup validates WAL database identity and timeline before inspecting records. Analysis classifies transactions as committed, rolled back, or incomplete and records the first change LSN for each dirty page. Only a record envelope extending beyond final EOF is an incomplete tail; it is ignored and the WAL is truncated to the preceding checksum-valid boundary. Invalid framing, values, or checksums before that boundary are corruption and stop open.
